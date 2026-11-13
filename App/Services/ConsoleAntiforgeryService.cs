using Apizr;
using snowcoreBlog.PublicApi.Api;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Extensions;

namespace snowcoreBlog.Console.App.Services;

/// <summary>
/// The backend services (articles / readers / authors) require a per-service
/// antiforgery token, obtained from that service's ITokensApi and replayed in the
/// RequestVerificationToken header together with the matching cookie. This service
/// mirrors the three AntiforgeryState copies of the Blazor frontend with a simple
/// cached-per-service dictionary and invalidation on a stale-token rejection.
/// </summary>
public sealed class ConsoleAntiforgeryService(
    IApizrManager<IArticlesTokensApi> articlesTokensApi,
    IApizrManager<IReaderAccountTokensApi> readersTokensApi,
    IApizrManager<IAuthorsManagementTokensApi> authorsTokensApi)
{
    public enum AntiforgeryService
    {
        Articles,
        Readers,
        Authors
    }

    private readonly Dictionary<AntiforgeryService, string> _tokens = [];

    public async Task<string> GetTokenAsync(AntiforgeryService service, CancellationToken cancellationToken = default)
    {
        if (_tokens.TryGetValue(service, out var cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        using var response = service switch
        {
            AntiforgeryService.Articles => await articlesTokensApi.ExecuteAsync(
                static (opt, api) => api.GetAntiforgeryToken(opt), o => o.WithCancellation(cancellationToken)),
            AntiforgeryService.Authors => await authorsTokensApi.ExecuteAsync(
                static (opt, api) => api.GetAntiforgeryToken(opt), o => o.WithCancellation(cancellationToken)),
            _ => await readersTokensApi.ExecuteAsync(
                static (opt, api) => api.GetAntiforgeryToken(opt), o => o.WithCancellation(cancellationToken))
        };

        var data = response.ToData<AntiforgeryResultDto>(out _);
        var token = data?.RequestToken ?? string.Empty;
        _tokens[service] = token;
        return token;
    }

    public void Invalidate(AntiforgeryService service) => _tokens.Remove(service);

    public void InvalidateAll() => _tokens.Clear();
}
