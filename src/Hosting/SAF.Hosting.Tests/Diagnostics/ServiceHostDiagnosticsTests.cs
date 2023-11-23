// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Hosting.Diagnostics;
using System.Diagnostics;
using System.Reflection;
using SAF.Hosting.Abstractions;
using Xunit;
using System.Runtime.Loader;

namespace SAF.Hosting.Tests.Diagnostics;

public class ServiceHostDiagnosticsTests
{
    [Fact]
    public void SafVersionInfoFilledOk()
    {
        var vi = new SafVersionInfo();
        var assembly = Assembly.GetExecutingAssembly();
        var version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
        var buildNr = assembly.GetName().Version!.ToString();
        Assert.Equal(version, vi.Version);
        Assert.Equal(buildNr, vi.BuildNumber);
    }

    [Fact]
    public void SafServiceInfoFilledOk()
    {
        var loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory, "SAF.Hosting.TestServices.dll"));
        var manifest = loadedAssembly.GetExportedTypes().SingleOrDefault(t => t.IsClass && typeof(IServiceAssemblyManifest).IsAssignableFrom(t));
        Assert.NotNull(manifest);

        var assembly = (IServiceAssemblyManifest)Activator.CreateInstance(manifest)!;
        var si = new SafServiceInfo(assembly);
        Assert.StartsWith("1.2.3.4", si.Version);
        Assert.Equal("1.2.3.4", si.BuildNumber);
    }
}