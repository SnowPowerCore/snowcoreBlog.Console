using snowcoreBlog.Console.App.Interfaces;
using snowcoreBlog.ConsoleHandling.Interfaces;

namespace snowcoreBlog.Console.App.Services;

public class ConsoleApplicationInfrastructureService(IConsoleNavigationService navigation,
                                                     IConsoleService console) : IConsoleApplicationInfrastructureService
{
    public IConsoleNavigationService Navigation { get; } = navigation;

    public IConsoleService Console { get; } = console;
}