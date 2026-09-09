// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using System.Reflection;

/// <summary>
/// Shares the assembly that declares <typeparamref name="T"/>, and, if <typeparamref name="T"/> is a
/// constructed generic type, the assemblies of its type arguments as well.
/// </summary>
/// <typeparam name="T">The type whose declaring assembly must be shared.</typeparam>
public sealed class SharedAssemblySource<T> : ISharedAssemblySource
{
    /// <inheritdoc />
    public IEnumerable<AssemblyName> GetSharedAssemblyNames()
        => CollectAssemblies(typeof(T)).DistinctBy(a => a.FullName).Select(a => a.GetName());

    // Type.Assembly for a constructed generic type (e.g. IRepo<Customer>) is only the assembly of the
    // generic definition (IRepo<>); a type argument from a different assembly (Customer) would otherwise
    // load isolated per plugin, closing over a different type identity than the host's registration.
    private static IEnumerable<Assembly> CollectAssemblies(Type type)
    {
        yield return type.Assembly;

        foreach (var typeArgument in type.GetGenericArguments())
        {
            foreach (var assembly in CollectAssemblies(typeArgument))
            {
                yield return assembly;
            }
        }
    }
}
