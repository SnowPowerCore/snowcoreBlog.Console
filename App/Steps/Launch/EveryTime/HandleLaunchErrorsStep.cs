using MinimalStepifiedSystem.Interfaces;
using snowcoreBlog.ApplicationLaunch.Context;
using snowcoreBlog.ApplicationLaunch.Delegates;
using snowcoreBlog.ApplicationLaunch.Models;
using snowcoreBlog.Console.App.Interfaces;

namespace snowcoreBlog.Console.App.Steps.Launch.EveryTime;

public class HandleLaunchErrorsStep(IConsoleApplicationService application) : IStep<LaunchDelegate, LaunchContext, ApplicationLaunchResult>
{
    public async Task<ApplicationLaunchResult> InvokeAsync(LaunchContext context, LaunchDelegate next, CancellationToken token = default)
    {
        try
        {
            return await next(context, token);
        }
        catch (Exception ex)
        {
            application.Logging.TrackException(ex);
            return new();
        }
    }
}