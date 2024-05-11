using MinimalStepifiedSystem.Utils;
using snowcoreBlog.Console.App.Constants;
using snowcoreBlog.Console.App.Interfaces;

namespace snowcoreBlog.Console.App.Models;

public class ScreenBase : IConsoleScreen
{
    protected DictionaryWithDefault<string, object> CurrentArguments { get; set; } = new(defaultValue: new());

    public DictionaryWithDefault<string, ScreenCommand> Commands { get; private set; }

    public virtual string WelcomeMessage { get; }

    protected IConsoleApplicationService Application { get; }

    public ScreenBase(IConsoleApplicationService application)
    {
        WelcomeMessage = application.Infrastructure.Resources[AppTranslations.WelcomeMessage];
        Commands = new(defaultValue: new(AskAgainAsync))
        {
            ["help"] = new(HelpAsync, "Displays a list of available commands with their descriptions."),
            ["exit"] = new(ExitAsync, "Exits the app.")
        };
        Application = application;
    }

    public async Task InitScreenAsync()
    {
        if (!string.IsNullOrEmpty(WelcomeMessage))
            Application.Infrastructure.Console.PrintLine(WelcomeMessage);

        await InitAsync();

        await ReturnToWaitForCommandInputAsync();
    }

    protected virtual async Task InitAsync() { }

    protected virtual async Task HelpAsync()
    {
        await OnScreenAppearingAsync();
        PrintCommands();
    }

    protected async Task ExitAsync() => Application.Stop();

    private async Task AskAgainAsync()
    {
        await OnScreenAppearingAsync();
    }

    private async Task ReturnToWaitForCommandInputAsync()
    {
        string? input;
        do
        {
            Application.Infrastructure.Console.PrintLine();
            input = Application.Infrastructure.Console.ReadLine();
        }
        while (string.IsNullOrEmpty(input));
        await Commands[input.ToLower()].ExecuteAsync();
        _ = ReturnToWaitForCommandInputAsync();
    }

    public virtual async Task OnScreenAppearingAsync(DictionaryWithDefault<string, object>? args = null)
    {
        if (args is not default(DictionaryWithDefault<string, object>))
            CurrentArguments = args;

        Application.Infrastructure.Console.New();
    }

    public virtual async Task OnScreenDisappearingAsync() { }

    public virtual void PrintCommands()
    {
        foreach (var command in Commands)
        {
            Application.Infrastructure.Console.PrintLine($"{command.Key}: {command.Value.Description}");
        }
    }
}