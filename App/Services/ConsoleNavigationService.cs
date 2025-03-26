using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MinimalStepifiedSystem.Utils;
using snowcoreBlog.Console.App.Interfaces;
using snowcoreBlog.Console.App.Options;

namespace snowcoreBlog.Console.App.Services;

public class ConsoleNavigationService(IOptions<KnownScreenOptions> knownScreens,
                                      IServiceProvider serviceProvider) : IConsoleNavigationService
{
    private readonly IList<IConsoleScreen> _screenStack = [];

    public Task NavigateToRootAsync(DictionaryWithDefault<string, object>? additionalData = null)
    {
        _screenStack.Clear();
        return NavigateToScreenAsync("_default", additionalData);
    }

    public async Task NavigateToScreenAsync(string screenName, DictionaryWithDefault<string, object>? additionalData = null)
    {
        var screenType = knownScreens.Value.KnownScreenTypes[screenName];
        var screen = (IConsoleScreen)serviceProvider.GetRequiredService(screenType);
        await screen.OnScreenAppearingAsync(additionalData);
        await screen.InitScreenAsync();
        _screenStack.Add(screen);
    }

    public async Task NavigateBackAsync()
    {
        var lastScreen = _screenStack.LastOrDefault();
        if (lastScreen is default(IConsoleScreen))
            return;

        await lastScreen.OnScreenDisappearingAsync();
        _screenStack.Remove(lastScreen);

        var newLastScreen = _screenStack.LastOrDefault();
        if (newLastScreen is default(IConsoleScreen))
            return;

        await newLastScreen.OnScreenAppearingAsync();
        await newLastScreen.InitScreenAsync();
    }
}