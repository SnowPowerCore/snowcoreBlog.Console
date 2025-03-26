using MinimalStepifiedSystem.Utils;
using snowcoreBlog.Console.App.Models;

namespace snowcoreBlog.Console.App.Options;

public record KnownScreenOptions
{
    public DictionaryWithDefault<string, Type> KnownScreenTypes { get; init; } =
        new(defaultValue: typeof(ScreenBase));

    public Type RootScreenType { get; set; } = typeof(ScreenBase);
}