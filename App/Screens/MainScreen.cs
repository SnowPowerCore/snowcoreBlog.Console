using snowcoreBlog.Console.App.Interfaces;
using snowcoreBlog.Console.App.Models;

namespace snowcoreBlog.Console.App.Screens;

public class MainScreen : ScreenBase
{
    public MainScreen(IConsoleApplicationService application) : base(application)
    {
        Commands.Add("q", new(ExitAsync, "Exits the app."));
    }
}