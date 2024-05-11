using snowcoreBlog.TelemetryHandling.Interfaces;

namespace snowcoreBlog.Console.App.Interfaces;

public interface IConsoleApplicationService
{
    IConsoleApplicationInfrastructureService Infrastructure { get; }

    ITelemetryService Logging { get; }

    Version AppVersion { get; }

    void Stop();
}