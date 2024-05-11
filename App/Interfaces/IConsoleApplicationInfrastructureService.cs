using snowcoreBlog.ConsoleHandling.Interfaces;
using snowcoreBlog.ResourceLoading.Interfaces;

namespace snowcoreBlog.Console.App.Interfaces;

public interface IConsoleApplicationInfrastructureService
{
    IConsoleNavigationService Navigation { get; }

    IResourceService Resources { get; }

    IConsoleService Console { get; }
}