using MinimalStepifiedSystem.Interfaces;
using snowcoreBlog.ApplicationLaunch.Context;
using snowcoreBlog.ApplicationLaunch.Delegates;
using snowcoreBlog.ApplicationLaunch.Models;
using snowcoreBlog.Console.App.Services;

namespace snowcoreBlog.Console.App.Steps.Launch.EveryTime;

/// <summary>
/// Console counterpart of the frontend FrontendApplicationLaunchService: warms
/// up the client state once at startup — the per-service antiforgery tokens
/// (articles / readers / authors), the reader authentication state and the active
/// technical notifications — so the first rendered screens have their data ready.
/// </summary>
public class PrefetchClientStateStep(ConsoleAntiforgeryService antiforgery,
                                    ConsoleAccountService accountService,
                                    NotificationsConsoleService notificationsService)
    : IStep<LaunchDelegate, LaunchContext, ApplicationLaunchResult>
{
    public async Task<ApplicationLaunchResult> InvokeAsync(LaunchContext context, LaunchDelegate next, CancellationToken token = default)
    {
        var antiforgeryTasks = new Task[]
        {
            antiforgery.GetTokenAsync(ConsoleAntiforgeryService.AntiforgeryService.Articles, token),
            antiforgery.GetTokenAsync(ConsoleAntiforgeryService.AntiforgeryService.Readers, token),
            antiforgery.GetTokenAsync(ConsoleAntiforgeryService.AntiforgeryService.Authors, token)
        };

        var stateTask = accountService.ResolveAuthStateAsync(token);
        var notificationsTask = notificationsService.GetActiveAsync(token);

        try
        {
            await Task.WhenAll(antiforgeryTasks.Append(stateTask).Append(notificationsTask));
        }
        catch
        {
            // A cold backend must not block the launch: screens re-fetch lazily.
        }

        return await next(context, token);
    }
}
