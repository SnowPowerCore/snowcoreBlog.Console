using MinimalStepifiedSystem.Attributes;
using snowcoreBlog.ApplicationLaunch.Context;
using snowcoreBlog.ApplicationLaunch.Delegates;
using snowcoreBlog.ApplicationLaunch.Interfaces;
using snowcoreBlog.Console.App.Steps.Launch.EveryTime;
using snowcoreBlog.VersionTracking.Interfaces;

namespace snowcoreBlog.Console.App.Services;

public class ConsoleApplicationLaunchService : IApplicationLaunchService
{
    private readonly IVersionTrackingService _versionTracking;

    [StepifiedProcess(Steps = [
        typeof(HandleLaunchErrorsStep),
        typeof(SampleInterceptHttpClientWithHeaderStep),
        typeof(NavigateToRootScreenStep),
    ])]
    protected LaunchDelegate EveryTimeLaunch { get; }

    [ServiceProviderSupplier]
    public ConsoleApplicationLaunchService(IServiceProvider _,
                                           IVersionTrackingService versionTracking)
    {
        _versionTracking = versionTracking;

        _versionTracking.Track();
    }

    public async Task InitAsync()
    {
        var successCurrent = Version.TryParse(_versionTracking.CurrentVersion, out var currentVersion);
        var successPrevious = Version.TryParse(_versionTracking.PreviousVersion, out var previousVersion);

        var launchContext = new LaunchContext(currentVersion!);

        if (_versionTracking.IsFirstLaunchEver)
        {
            //await FirstTimeLaunch(launchContext);
        }

        if (successCurrent && successPrevious && currentVersion!.CompareTo(previousVersion) > 0)
        {
            //await AfterUpdateLaunch(launchContext);
        }

        await EveryTimeLaunch(launchContext);
    }
}