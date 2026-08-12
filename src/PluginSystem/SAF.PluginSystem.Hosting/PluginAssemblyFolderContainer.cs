// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.IO.Abstractions;

/// <inheritdoc />
public class PluginAssemblyFolderContainer(
    ILoggerFactory loggerFactory,
    IPluginManifestLoader manifestLoader,
    PluginAssemblyFolderSearchOptions options,
    IFileSystem fileSystem,
    IEnumerable<IPluginAssemblyValidator> assemblyValidators)
    : IPluginAssemblyContainer
{
    private const int CompareBufferSize = 64 * 1024;

    private readonly ILogger _logger = loggerFactory.CreateLogger<PluginAssemblyFolderContainer>();
    private readonly IPluginManifestLoader _manifestLoader = manifestLoader;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IReadOnlyList<IPluginAssemblyValidator> _assemblyValidators = assemblyValidators.ToList();
    private IReadOnlyList<IPluginManifest>? _cachedManifests;
    private readonly Lock _cacheLock = new();

    private PluginAssemblyFolderSearchOptions SearchOptions { get; } = options;

    /// <summary>
    /// Returns all <see cref="IPluginManifest"/> instances discovered in the configured folder.
    /// Results are cached after the first call so that assemblies are loaded only once.
    /// </summary>
    public IEnumerable<IPluginManifest> GetPluginManifests()
    {
        lock (_cacheLock)
        {
            if (_cachedManifests is not null)
            {
                return _cachedManifests;
            }

            var pluginAssemblyPaths = GetPluginAssemblyPaths();
            _cachedManifests = [.. LoadManifests(pluginAssemblyPaths)];
            return _cachedManifests;
        }
    }

    // Absolute paths: LoadFromAssemblyPath rejects relative ones, and validators see a canonical path.
    private List<string> GetPluginAssemblyPaths()
        => [.. SearchDirectoryForMatchingFiles(SearchOptions.SearchRootPath).Select(_fileSystem.Path.GetFullPath)];

    private List<string> SearchDirectoryForMatchingFiles(string directory)
    {
        if (!_fileSystem.Directory.Exists(directory))
        {
            _logger.LogWarning("Configured plugin directory {SearchDirectory} not found. No plugins will be loaded.", directory);
            return [];
        }

        _logger.LogDebug("Searching for matching plugin assemblies in directory {SearchDirectory}{Recursive}",
            directory, SearchOptions.Recursive ? " recursive" : string.Empty);

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddIncludePatterns(SearchOptions.IncludePatterns.Split(';'));
        matcher.AddExcludePatterns(SearchOptions.ExcludePatterns.Split(';'));

        var result = matcher.GetResultsInFullPath(directory).ToList();
        _logger.LogDebug("Found {MatchingAssemblyCount} matching assemblies", result.Count);

        if (SearchOptions.Recursive)
        {
            foreach (var subDir in _fileSystem.Directory.GetDirectories(directory))
            {
                result.AddRange(SearchDirectoryForMatchingFiles(subDir));
            }
        }

        return result;
    }

    private List<IPluginManifest> LoadManifests(List<string> pluginAssemblyPaths)
    {
        List<IPluginManifest> manifests = [];

        foreach (var pluginAssemblyPath in pluginAssemblyPaths)
        {
            try
            {
                // FileShare.Read denies every subsequent open that asks for write or delete access, so on
                // Windows this handle pins the candidate: it cannot be modified or swapped between the
                // validation below and the load further down. The handle is kept open until both are done.
                using var assemblyFile = _fileSystem.FileInfo.New(pluginAssemblyPath)
                    .Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                using var assemblyBuffer = new MemoryStream();
                assemblyFile.CopyTo(assemblyBuffer);
                var assemblyBytes = assemblyBuffer.ToArray();

                if (!TryValidateAssembly(pluginAssemblyPath, assemblyBytes, out var rejectionReason))
                {
                    _logger.LogWarning("Skip plugin assembly {PluginAssemblyPath}: {Reason}", pluginAssemblyPath, rejectionReason);
                    continue;
                }

                _logger.LogDebug("Create AssemblyLoadContext for {PluginAssemblyPath}", pluginAssemblyPath);

                var isInBaseDirectory = string.Compare(
                    _fileSystem.Path.GetDirectoryName(AppContext.BaseDirectory),
                    _fileSystem.Path.GetDirectoryName(pluginAssemblyPath),
                    StringComparison.OrdinalIgnoreCase) == 0;

                if (!IsUnchangedOnDisk(pluginAssemblyPath, assemblyBytes))
                {
                    _logger.LogWarning("Skip plugin assembly {PluginAssemblyPath}: the file changed after it was validated", pluginAssemblyPath);
                    continue;
                }

                var pluginLoadContext = isInBaseDirectory
                    ? AssemblyLoadContext.Default
                    : new PluginAssemblyLoadContext(loggerFactory, pluginAssemblyPath, _fileSystem);

                var assembly = pluginLoadContext.LoadFromAssemblyPath(pluginAssemblyPath);
                var manifest = _manifestLoader.LoadPluginManifest(assembly);

                if (manifest == null)
                {
                    _logger.LogWarning("Can't find manifest in {Assembly} from {AssemblyLocation}, skipping assembly.", assembly, assembly.Location);
                    continue;
                }

                _logger.LogDebug("Found manifest in {Assembly} from {AssemblyLocation}", assembly, assembly.Location);
                manifests.Add(manifest);
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException
                                          or ReflectionTypeLoadException or TypeLoadException
                                          or IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Failed to load plugin manifest from {PluginAssemblyPath}, skipping assembly.", pluginAssemblyPath);
            }
        }

        return manifests;
    }

    /// <summary>
    /// Confirms that the file still holds the content that was just validated.
    /// </summary>
    /// <remarks>
    /// POSIX file locks are advisory, so the open handle cannot keep the path stable: the file can still
    /// be modified or replaced after validation. Re-reading it immediately before the load does not close
    /// that window, but shrinks it from the duration of validation - certificate chain building, possibly
    /// including network revocation checks - down to the load call itself.
    /// </remarks>
    private bool IsUnchangedOnDisk(string pluginAssemblyPath, byte[] validatedBytes)
    {
        // Windows pins the file with the FileShare.Read handle held around validation and load, which is
        // a stronger guarantee than re-reading. Without validators there is no result a swap could
        // invalidate, so neither check buys anything.
        if (OperatingSystem.IsWindows() || _assemblyValidators.Count == 0)
        {
            return true;
        }

        try
        {
            return MatchesFileContent(_fileSystem, pluginAssemblyPath, validatedBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Cannot re-read plugin assembly {PluginAssemblyPath} before loading it", pluginAssemblyPath);
            return false;
        }
    }

    internal static bool MatchesFileContent(IFileSystem fileSystem, string path, ReadOnlySpan<byte> expected)
    {
        using var stream = fileSystem.FileInfo.New(path).Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != expected.Length)
        {
            return false;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(CompareBufferSize);
        try
        {
            var offset = 0;
            while (offset < expected.Length)
            {
                var read = stream.Read(buffer, 0, Math.Min(buffer.Length, expected.Length - offset));
                if (read <= 0 || !buffer.AsSpan(0, read).SequenceEqual(expected.Slice(offset, read)))
                {
                    return false;
                }

                offset += read;
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private bool TryValidateAssembly(
        string pluginAssemblyPath,
        ReadOnlyMemory<byte> assemblyBytes,
        out string rejectionReason)
    {
        rejectionReason = string.Empty;

        AssemblyName assemblyName;
        try
        {
            assemblyName = GetAssemblyName(assemblyBytes);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException)
        {
            rejectionReason = $"metadata could not be read ({ex.GetType().Name})";
            return false;
        }

        var validationContext = new PluginAssemblyValidationContext(pluginAssemblyPath, assemblyName, assemblyBytes);

        foreach (var validator in _assemblyValidators)
        {
            PluginAssemblyValidationResult result;
            try
            {
                result = validator.Validate(validationContext);
            }
            catch (Exception ex)
            {
                // IPluginAssemblyValidator is a public extension point: a throwing implementation
                // must not abort the host, and the assembly is rejected (fail closed).
                _logger.LogError(ex, "Validator {ValidatorType} threw while validating {PluginAssemblyPath}", validator.GetType().Name, pluginAssemblyPath);
                rejectionReason = $"validator {validator.GetType().Name} threw {ex.GetType().Name}";
                return false;
            }

            if (result.IsAccepted)
            {
                continue;
            }

            rejectionReason = string.IsNullOrWhiteSpace(result.Reason)
                ? $"rejected by validator {validator.GetType().Name}"
                : result.Reason;
            return false;
        }

        return true;
    }

    private static AssemblyName GetAssemblyName(ReadOnlyMemory<byte> assemblyBytes)
    {
        using var stream = new MemoryStream(assemblyBytes.ToArray(), writable: false);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
        {
            throw new BadImageFormatException("Assembly metadata is missing.");
        }

        var metadataReader = peReader.GetMetadataReader();
        var assemblyDefinition = metadataReader.GetAssemblyDefinition();
        var assemblyName = new AssemblyName(metadataReader.GetString(assemblyDefinition.Name))
        {
            CultureName = assemblyDefinition.Culture.IsNil
                ? null
                : metadataReader.GetString(assemblyDefinition.Culture),
            Version = assemblyDefinition.Version
        };

        if (!assemblyDefinition.PublicKey.IsNil)
        {
            assemblyName.SetPublicKey(metadataReader.GetBlobBytes(assemblyDefinition.PublicKey));
        }

        return assemblyName;
    }
}
