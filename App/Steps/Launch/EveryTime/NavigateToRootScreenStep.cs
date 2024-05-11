using MinimalStepifiedSystem.Interfaces;
using snowcoreBlog.ApplicationLaunch.Context;
using snowcoreBlog.ApplicationLaunch.Delegates;
using snowcoreBlog.Console.App.Interfaces;

namespace snowcoreBlog.Console.App.Steps.Launch.EveryTime;

public class NavigateToRootScreenStep(IConsoleNavigationService navigation) : IStep<LaunchDelegate, LaunchContext>
{
    public async Task InvokeAsync(LaunchContext context, LaunchDelegate next)
    {
        await navigation.NavigateToRootAsync();
        await next(context);
    }
}