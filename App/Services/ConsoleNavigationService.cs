using Microsoft.Extensions.Options;
using MinimalStepifiedSystem.Utils;
using snowcoreBlog.Console.App.Interfaces;
using snowcoreBlog.Console.App.Options;

namespace snowcoreBlog.Console.App.Services;

/// <summary>
/// Route-stack navigation over the RazorConsole router. The screen-name → route
/// registry (KnownScreenOptions) and the navigation stack semantics are kept from
/// the original ScreenBase-based navigation, while the actual page switch is
/// performed by the Router through NavigationManager.
/// </summary>
public class ConsoleNavigationService(IOptions<KnownScreenOptions> knownScreens) : IConsoleNavigationService
{
    private readonly IList<string> _screenStack = [];

    /// <summary>
    /// Raised for every requested navigation. The root component subscribes and
    /// forwards the route to NavigationManager on the renderer's dispatcher
    /// thread, so the stepified launch pipeline can navigate safely from a
    /// background worker.
    /// </summary>
    public event Func<string, Task>? NavigationRequested;

    public async Task NavigateToRootAsync(DictionaryWithDefault<string, object>? additionalData = null)
    {
        _screenStack.Clear();
        await RaiseNavigationAsync(knownScreens.Value.RootRoute);
    }

    public async Task NavigateToScreenAsync(string screenName, DictionaryWithDefault<string, object>? additionalData = null)
    {
        var route = knownScreens.Value.KnownScreenRoutes[screenName];
        if (additionalData is not default(DictionaryWithDefault<string, object>) &&
            additionalData.TryGetValue("route", out var customRoute) && customRoute is string custom)
        {
            route = custom;
        }

        Track(route);
        await RaiseNavigationAsync(route);
    }

    public async Task NavigateBackAsync()
    {
        if (_screenStack.Count > 0)
        {
            _screenStack.RemoveAt(_screenStack.Count - 1);
        }

        var previous = _screenStack.LastOrDefault();
        if (!string.IsNullOrWhiteSpace(previous))
        {
            await RaiseNavigationAsync(previous);
        }
    }

    private void Track(string route)
    {
        if (_screenStack.Count == 0 || _screenStack[^1] != route)
        {
            _screenStack.Add(route);
        }
    }

    private async Task RaiseNavigationAsync(string route)
    {
        var handler = NavigationRequested;
        if (handler is not default(Func<string, Task>))
        {
            await handler(route);
        }
    }
}