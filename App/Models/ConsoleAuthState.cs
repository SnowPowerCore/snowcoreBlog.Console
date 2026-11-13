using System.Security.Claims;

namespace snowcoreBlog.Console.App.Models;

/// <summary>
/// Terminal counterpart of the Blazor AuthenticationState: resolved from the
/// readers service "check/me/v1" endpoint (AuthenticationStateDto claims) and
/// shared with the layout and gated pages through DI.
/// </summary>
public sealed record ConsoleAuthState(IReadOnlyDictionary<string, string> Claims)
{
    public static readonly ConsoleAuthState Anonymous = new(new Dictionary<string, string>());

    public bool IsAuthenticated => Claims.Count > 0;

    public Guid? UserId => Claims.TryGetValue("sub", out var sub) && Guid.TryParse(sub, out var id) ? id :
        Claims.TryGetValue("ReaderAccountUserId", out var readerId) && Guid.TryParse(readerId, out var rid) ? rid : null;

    public bool IsAuthor => Claims.TryGetValue("authorAccount", out var value) && value is "true" or "True";

    public string? Email => Claims.TryGetValue("AdminEmail", out var adminEmail) ? adminEmail :
        Claims.TryGetValue("email", out var email) ? email : null;

    public string? NickName => Claims.TryGetValue("NickName", out var nickName) ? nickName : null;

    public ClaimsPrincipal ToPrincipal()
    {
        // The identity authentication type MUST be non-empty (mirrors the frontend
        // UserService): an empty one reports IsAuthenticated == false.
        var identity = new ClaimsIdentity(
            Claims.Select(x => new Claim(x.Key, x.Value)),
            authenticationType: IsAuthenticated ? "Cookies" : string.Empty);
        return new ClaimsPrincipal(identity);
    }
}
