using Microsoft.Extensions.Hosting;
using snowcoreBlog.Console.App.Interfaces;
using snowcoreBlog.TelemetryHandling.Interfaces;
using snowcoreBlog.VersionTracking.Interfaces;

namespace snowcoreBlog.Console.App.Services;

public class ConsoleApplicationService(IHostApplicationLifetime hostLifetime,
                                       IConsoleApplicationInfrastructureService applicationInfrastructure,
                                       ITelemetryService telemetry,
                                       IVersionTrackingService versionTracking) : IConsoleApplicationService
{
    public IConsoleApplicationInfrastructureService Infrastructure { get; } = applicationInfrastructure;

    public ITelemetryService Logging { get; } = telemetry;

    public Version AppVersion { get; } = new Version(versionTracking.CurrentVersion);

    public void Stop() =>
        hostLifetime.StopApplication();
}