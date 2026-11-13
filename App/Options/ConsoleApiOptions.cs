namespace snowcoreBlog.Console.App.Options;

/// <summary>
/// Base address of the YARP gateway the console client talks to. All backend
/// services are reached under /api/&lt;service&gt; exactly like the Blazor frontend.
/// </summary>
public sealed class ConsoleApiOptions
{
    public const string SectionName = "ConsoleApi";

    public string GatewayBaseUrl { get; set; } = "https://localhost";
}
