using snowcoreBlog.ConsoleHandling.Interfaces;

namespace snowcoreBlog.Console.App.Interfaces;

public interface IConsoleApplicationInfrastructureService
{
    IConsoleNavigationService Navigation { get; }

    IConsoleService Console { get; }
}