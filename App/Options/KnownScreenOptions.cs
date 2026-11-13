using MinimalStepifiedSystem.Utils;

namespace snowcoreBlog.Console.App.Options;

/// <summary>
/// Registry of the screens the console app can navigate to. Screens are routable
/// RazorConsole pages, so a known screen maps its logical name to a route path;
/// the router (Components/App.razor) performs the actual page switch.
/// </summary>
public record KnownScreenOptions
{
    public DictionaryWithDefault<string, string> KnownScreenRoutes { get; init; } =
        new(defaultValue: "/");

    public string RootRoute { get; set; } = "/";
}