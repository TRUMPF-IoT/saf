// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.IO;
using System.Reflection;

[Collection("BaseDirectoryFileSystem")]
public class PublicServiceTypeRegistryTests
{
    [Fact]
    public void GetAssemblyNames_ReturnsEmpty_WhenPatternIsEmpty()
    {
        var options = Options.Create(new PluginSystemOptions { PluginContractsSearchPattern = string.Empty });
        var registry = new PublicServiceTypeRegistry(NullLogger<PublicServiceTypeRegistry>.Instance, options);

        var result = registry.GetAssemblyNames().ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void GetAssemblyNames_ReturnsMatchingAssemblyFullName()
    {
        var assemblyPath = typeof(PublicServiceTypeRegistry).Assembly.Location;
        var assemblyFileName = Path.GetFileName(assemblyPath);
        var expectedFullName = AssemblyName.GetAssemblyName(assemblyPath).FullName;

        var options = Options.Create(new PluginSystemOptions { PluginContractsSearchPattern = assemblyFileName });
        var registry = new PublicServiceTypeRegistry(NullLogger<PublicServiceTypeRegistry>.Instance, options);

        var result = registry.GetAssemblyNames().ToList();

        Assert.Contains(expectedFullName, result);
    }

    [Fact]
    public void GetAssemblyNames_RetriesOnNextCall_WhenReadingAnAssemblyNamePreviouslyFailed()
    {
        var testId = Guid.NewGuid().ToString("N");
        var fileName = $"malformed-{testId}.dll";
        var filePath = Path.Combine(AppContext.BaseDirectory, fileName);
        File.WriteAllBytes(filePath, [0x00, 0x01, 0x02, 0x03]);

        var options = Options.Create(new PluginSystemOptions { PluginContractsSearchPattern = fileName });
        var registry = new PublicServiceTypeRegistry(NullLogger<PublicServiceTypeRegistry>.Instance, options);

        try
        {
            Assert.Throws<BadImageFormatException>(() => registry.GetAssemblyNames().ToList());

            var validAssemblyPath = typeof(PublicServiceTypeRegistry).Assembly.Location;
            File.Copy(validAssemblyPath, filePath, overwrite: true);
            var expectedFullName = AssemblyName.GetAssemblyName(filePath).FullName;

            var result = registry.GetAssemblyNames().ToList();

            Assert.Contains(expectedFullName, result);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void GetAssemblyNames_DoesNotRescan_AfterInitialization()
    {
        var assemblyPath = typeof(PublicServiceTypeRegistry).Assembly.Location;
        var assemblyFileName = Path.GetFileName(assemblyPath);
        var copyFileName = Path.GetFileNameWithoutExtension(assemblyFileName) + ".Copy.dll";
        var copyPath = Path.Combine(AppContext.BaseDirectory, copyFileName);

        if (File.Exists(copyPath))
        {
            File.Delete(copyPath);
        }

        var options = Options.Create(new PluginSystemOptions { PluginContractsSearchPattern = $"{assemblyFileName};{copyFileName}" });
        var registry = new PublicServiceTypeRegistry(NullLogger<PublicServiceTypeRegistry>.Instance, options);

        var firstResult = registry.GetAssemblyNames().ToList();

        try
        {
            File.Copy(assemblyPath, copyPath, true);

            var secondResult = registry.GetAssemblyNames().ToList();

            Assert.Single(firstResult);
            Assert.Single(secondResult);
            Assert.Equal(firstResult[0], secondResult[0]);
        }
        finally
        {
            if (File.Exists(copyPath))
            {
                File.Delete(copyPath);
            }
        }
    }
}
