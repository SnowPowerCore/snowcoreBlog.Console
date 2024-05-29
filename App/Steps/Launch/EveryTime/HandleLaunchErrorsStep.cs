using MinimalStepifiedSystem.Interfaces;
using snowcoreBlog.ApplicationLaunch.Context;
using snowcoreBlog.ApplicationLaunch.Delegates;
using snowcoreBlog.Console.App.Interfaces;

namespace snowcoreBlog.Console.App.Steps.Launch.EveryTime;

public class HandleLaunchErrorsStep(IConsoleApplicationService application) : IStep<LaunchDelegate, LaunchContext>
{
    public async Task InvokeAsync(LaunchContext context, LaunchDelegate next, CancellationToken token = default)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            application.Logging.TrackException(ex);
        }
    }
}