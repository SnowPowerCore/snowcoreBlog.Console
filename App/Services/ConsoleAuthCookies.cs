using System.Net;
using snowcoreBlog.Console.App.Options;

namespace snowcoreBlog.Console.App.Services;

/// <summary>
/// Application-wide authentication cookie jar. The gateway issues the reader
/// account token pair (".DotNet.Application.User.SystemKey" / "...SystemUpdateKey")
/// as host-scoped HTTP-only cookies; a single shared container lets every gateway
/// client (articles / readers / authors / notifications + SSE) send and store
/// them transparently, mirroring the browser behaviour of the Blazor frontend.
/// </summary>
public sealed class ConsoleAuthCookies
{
    public CookieContainer Container { get; } = new();

    public HttpClientHandler CreateHandler() => new()
    {
        CookieContainer = Container,
        UseCookies = true
    };

    public void ClearAuthCookies(ConsoleApiOptions options)
    {
        var baseUri = new Uri(options.GatewayBaseUrl);
        foreach (var cookie in Container.GetCookies(baseUri).Cast<Cookie>().ToArray())
        {
            cookie.Expired = true;
        }
    }
}
