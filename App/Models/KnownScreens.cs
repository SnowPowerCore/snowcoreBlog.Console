using MinimalStepifiedSystem.Utils;

namespace snowcoreBlog.Console.App.Models;

public record KnownScreens
{
    public DictionaryWithDefault<string, Type> KnownScreenTypes { get; init; } =
        new(defaultValue: typeof(ScreenBase));

    public Type RootScreenType { get; set; } = typeof(ScreenBase);
}