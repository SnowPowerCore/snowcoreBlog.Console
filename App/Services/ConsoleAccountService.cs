using Apizr;
using Ixnas.AltchaNet;
using Microsoft.Extensions.Options;
using snowcoreBlog.Console.App.Models;
using snowcoreBlog.Console.App.Options;
using snowcoreBlog.PublicApi.Api;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Extensions;

namespace snowcoreBlog.Console.App.Services;

/// <summary>
/// Terminal counterpart of the frontend BitzArt IUserService backed flows
/// (see WasmForDebugging UserService): resolves the authentication state from
/// the readers service, signs in with the emailed one-time code (passkeys and
/// external providers need a browser and are surfaced as informational hints),
/// revokes the token pair on sign-out and refreshes the JWT pair on demand.
/// </summary>
public sealed class ConsoleAccountService(
    IApizrManager<IReaderAccountManagementApi> readerAccountApi,
    IApizrManager<IReaderAccountTokensApi> tokensApi,
    ConsoleAntiforgeryService antiforgery,
    ConsoleAuthCookies authCookies,
    AltchaSolver altchaSolver,
    IOptions<ConsoleApiOptions> apiOptions)
{
    public event Func<Task>? AuthStateChanged;

    private ConsoleAuthState _state = ConsoleAuthState.Anonymous;

    public ConsoleAuthState Current => _state;

    private ConsoleApiOptions ApiOptions => apiOptions.Value;

    public async Task<ConsoleAuthState> ResolveAuthStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await readerAccountApi.ExecuteAsync(
                static (opt, api) => api.RequestAuthData(opt), o => o.WithCancellation(cancellationToken));
            var data = response.ToData<AuthenticationStateDto>(out _);
            _state = data is default(AuthenticationStateDto) || data.Claims.Count == 0
                ? ConsoleAuthState.Anonymous
                : new ConsoleAuthState(data.Claims);
        }
        catch
        {
            // Network failure, gateway down, ... — treat as anonymous instead of
            // faulting the launch or layout.
            _state = ConsoleAuthState.Anonymous;
        }

        return _state;
    }

    public void NotifyStateChanged() => _ = AuthStateChanged?.Invoke();

    public async Task<(bool Success, string? ErrorMessage)> SignInWithOneTimeCodeAsync(
        string email, string code, CancellationToken cancellationToken = default)
    {
        string antiforgeryToken;
        try
        {
            antiforgeryToken = await antiforgery.GetTokenAsync(
                ConsoleAntiforgeryService.AntiforgeryService.Readers, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        using var captchaResponse = await tokensApi.ExecuteAsync(
            static (opt, api) => api.GetAltchaChallenge(opt), o => o.WithCancellation(cancellationToken));
        if (!captchaResponse.IsSuccess)
        {
            return (false, captchaResponse.Exception?.Message);
        }

        var solverResult = altchaSolver.Solve(captchaResponse.Result);
        if (!solverResult.Success)
        {
            return (false, solverResult.Error.Message);
        }

        try
        {
            using var response = await readerAccountApi.ExecuteAsync((opt, api) =>
                api.VerifyOneTimeCode(
                    new VerifyOneTimeCodeDto { Email = email, Code = code },
                    antiforgeryToken, solverResult.Altcha, opt),
                o => o.WithCancellation(cancellationToken));

            var data = response.ToData<LoginByOneTimeCodeResultDto>(out var errors);
            if (errors.Count > 0)
            {
                // A rejected antiforgery/captcha pair poisons the stored token —
                // drop it so the next attempt fetches a fresh one.
                antiforgery.Invalidate(ConsoleAntiforgeryService.AntiforgeryService.Readers);
                return (false, errors.FirstOrDefault());
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        await ResolveAuthStateAsync(cancellationToken);
        NotifyStateChanged();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> RefreshAsync(CancellationToken cancellationToken = default)
    {
        // The refresh endpoint resolves the refresh cookie server-side; an empty
        // body mirrors the frontend RefreshJwtPairAsync call.
        try
        {
            using var response = await tokensApi.ExecuteAsync(
                (opt, api) => api.RefreshReaderJwtPair(string.Empty, opt), o => o.WithCancellation(cancellationToken));
            if (!response.IsSuccess)
            {
                return (false, response.Exception?.Message ?? "Token refresh failed");
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        await ResolveAuthStateAsync(cancellationToken);
        NotifyStateChanged();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> SignOutAsync(CancellationToken cancellationToken = default)
    {
        string antiforgeryToken;
        try
        {
            antiforgeryToken = await antiforgery.GetTokenAsync(
                ConsoleAntiforgeryService.AntiforgeryService.Readers, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        try
        {
            using var revokeResponse = await tokensApi.ExecuteAsync(
                (opt, api) => api.RevokeReaderTokens(antiforgeryToken, opt), o => o.WithCancellation(cancellationToken));
            if (!revokeResponse.IsSuccess)
            {
                antiforgery.Invalidate(ConsoleAntiforgeryService.AntiforgeryService.Readers);
                return (false, revokeResponse.Exception?.Message ?? "Sign-out failed: the backend could not revoke the token pair.");
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }

        antiforgery.InvalidateAll();
        authCookies.ClearAuthCookies(ApiOptions);
        _state = ConsoleAuthState.Anonymous;
        NotifyStateChanged();
        return (true, null);
    }
}
