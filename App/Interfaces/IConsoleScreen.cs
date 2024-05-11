using MinimalStepifiedSystem.Utils;

namespace snowcoreBlog.Console.App.Interfaces;

public interface IConsoleScreen
{
    Task InitScreenAsync();

    Task OnScreenAppearingAsync(DictionaryWithDefault<string, object>? args = null);

    Task OnScreenDisappearingAsync();
}