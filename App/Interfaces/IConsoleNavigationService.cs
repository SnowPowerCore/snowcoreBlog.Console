using MinimalStepifiedSystem.Utils;

namespace snowcoreBlog.Console.App.Interfaces;

public interface IConsoleNavigationService
{
    Task NavigateToRootAsync(DictionaryWithDefault<string, object>? additionalData = null);

    Task NavigateToScreenAsync(string screenName, DictionaryWithDefault<string, object>? additionalData = null);

    Task NavigateBackAsync();
}