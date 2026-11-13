using Apizr;
using snowcoreBlog.PublicApi.Api;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;

namespace snowcoreBlog.Console.App.Services;

/// <summary>
/// Authors management for the terminal Become Author page (console counterpart
/// of the AuthorsManagement module).
/// </summary>
public sealed class AuthorsConsoleService(
    IApizrManager<IAuthorsManagementApi> authorsApi,
    ConsoleAntiforgeryService antiforgery)
{
    public async Task<(bool Success, List<string> Errors)> BecomeAuthorAsync(
        Guid userId, string displayName, CancellationToken cancellationToken = default)
    {
        string antiforgeryToken;
        try
        {
            antiforgeryToken = await antiforgery.GetTokenAsync(
                ConsoleAntiforgeryService.AntiforgeryService.Authors, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, [ex.Message]);
        }

        try
        {
            using var response = await authorsApi.ExecuteAsync(
                (opt, api) => api.BecomeAuthor(new BecomeAuthorAccountRequestDto(userId, displayName), antiforgeryToken, opt),
                o => o.WithCancellation(cancellationToken));

            if (!response.IsSuccess)
            {
                antiforgery.Invalidate(ConsoleAntiforgeryService.AntiforgeryService.Authors);
                return (false, new List<string> { response.Exception?.Message ?? "Becoming an author failed." });
            }

            return (true, []);
        }
        catch (Exception ex)
        {
            return (false, [ex.Message]);
        }
    }
}
