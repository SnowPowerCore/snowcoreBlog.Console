using MinimalStepifiedSystem.Utils;

namespace snowcoreBlog.Console.App.Interfaces;

public interface IConsoleNavigationService
{
    /// <summary>
    /// Raised for every requested navigation; the root component forwards routes
    /// to the RazorConsole router on the renderer's dispatcher thread.
    /// </summary>
    event Func<string, Task>? NavigationRequested;

    Task NavigateToRootAsync(DictionaryWithDefault<string, object>? additionalData = null);

    Task NavigateToScreenAsync(string screenName, DictionaryWithDefault<string, object>? additionalData = null);

    Task NavigateBackAsync();
}