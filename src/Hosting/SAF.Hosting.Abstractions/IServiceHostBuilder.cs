using Microsoft.Extensions.DependencyInjection;

namespace SAF.Hosting.Abstractions;

/// <summary>
/// An interface for configuring SAF service host services.
/// </summary>
public interface IServiceHostBuilder
{
    /// <summary>
    /// Gets the IServiceCollection where SAF service host services are configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets an IServiceCollection of common services that will be redirected to the SAF plug-in DI containers.
    /// </summary>
    IServiceCollection CommonServices { get; }

    /// <summary>
    /// Configures the <see cref="IServiceHostInfo"/> used by the SAF service host.
    /// </summary>
    /// <param name="setupAction">The configuration action.</param>
    /// <returns>The <see cref="IServiceHostBuilder"></see></returns>
    IServiceHostBuilder ConfigureServiceHostInfo(Action<ServiceHostInfoOptions> setupAction);
}