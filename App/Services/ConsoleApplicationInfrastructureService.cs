using snowcoreBlog.Console.App.Interfaces;
using snowcoreBlog.ConsoleHandling.Interfaces;
using snowcoreBlog.ResourceLoading.Interfaces;

namespace snowcoreBlog.Console.App.Services;

public class ConsoleApplicationInfrastructureService(IConsoleNavigationService navigation,
                                                     IConsoleService console,
                                                     IResourceService resource) : IConsoleApplicationInfrastructureService
{
    public IConsoleNavigationService Navigation { get; } = navigation;

    public IResourceService Resources { get; } = resource;

    public IConsoleService Console { get; } = console;
}