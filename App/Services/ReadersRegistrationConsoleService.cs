using System.Net;
using Apizr;
using Ixnas.AltchaNet;
using snowcoreBlog.Console.App.Protocol.Sse;
using snowcoreBlog.PublicApi.Api;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Extensions;
using snowcoreBlog.PublicApi.Utilities.Sse;

namespace snowcoreBlog.Console.App.Services;

/// <summary>
/// Reader account registration flows for the terminal app: the SSE-backed
/// account request (create/request/email/v1), the authenticator setup
/// (create/totp/setup/v1) and the SSE-backed authenticator confirmation
/// (create/confirm/totp/v1). Console counterpart of the frontend
/// ReadersManagement register wizard + complete-registration page.
/// </summary>
public sealed class ReadersRegistrationConsoleService(
    IHttpClientFactory httpClientFactory,
    IApizrManager<IReaderAccountTokensApi> tokensApi,
    IApizrManager<IReaderAccountManagementApi> readerAccountApi,
    ConsoleAntiforgeryService antiforgery,
    AltchaSolver altchaSolver)
{
    public async Task<(bool Success, List<string> Errors)> CheckNickNameNotTakenAsync(
        string nickName, CancellationToken cancellationToken = default)
    {
        try
        {
            var antiforgeryToken = await antiforgery.GetTokenAsync(
                ConsoleAntiforgeryService.AntiforgeryService.Readers, cancellationToken);

            using var response = await readerAccountApi.ExecuteAsync(
                (opt, api) => api.CheckNickNameNotTaken(new CheckNickNameNotTakenDto { NickName = nickName }, antiforgeryToken, opt),
                o => o.WithCancellation(cancellationToken));
            var data = response.ToData<NickNameNotTakenCheckResultDto>(out var errors);
            if (errors.Count > 0)
            {
                antiforgery.Invalidate(ConsoleAntiforgeryService.AntiforgeryService.Readers);
                return (false, errors);
            }

            return (data is not default(NickNameNotTakenCheckResultDto) && !data.WasTaken, errors);
        }
        catch (Exception ex)
        {
            return (false, [Describe(ex)]);
        }
    }

    public async Task<(bool Success, List<string> Errors)> RequestAccountByEmailAsync(
        RequestCreateReaderAccountDto request, Action<string> onStepProgress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var antiforgeryToken = await antiforgery.GetTokenAsync(
                ConsoleAntiforgeryService.AntiforgeryService.Readers, cancellationToken);

        using var captchaResponse = await tokensApi.ExecuteAsync(
            static (opt, api) => api.GetAltchaChallenge(opt), o => o.WithCancellation(cancellationToken));
        if (!captchaResponse.IsSuccess)
        {
            return (false, [captchaResponse.Exception?.Message ?? "Captcha challenge failed."]);
        }

        var solverResult = altchaSolver.Solve(captchaResponse.Result);
        if (!solverResult.Success)
        {
            return (false, [solverResult.Error.Message]);
        }

        var httpClient = httpClientFactory.CreateClient(SseClientNames.ReadersManagement);
        var frames = httpClient.PostServerSentEventsAsync(
            "create/request/email/v1",
            request,
            message =>
            {
                message.Headers.Add("RequestVerificationToken", antiforgeryToken);
                message.Headers.Add("RequestCaptcha", solverResult.Altcha);
            },
            cancellationToken);

        var finalEvent = await StepProgressStreamReader.ReadAsync(frames, onStepProgress, cancellationToken);
        if (finalEvent is not default(StepProgressFinalEvent) && finalEvent.StatusCode == (int)HttpStatusCode.OK)
        {
            return (true, []);
        }

        var errors = finalEvent?.Response?.Errors?.ToList() ?? [];
        if (errors.Count == 0)
        {
            errors.Add("Registration request failed.");
        }

        return (false, errors);
        }
        catch (Exception ex)
        {
            return (false, [Describe(ex)]);
        }
    }

    public async Task<(bool Success, SetupReaderAccountAuthenticatorResultDto? Setup, List<string> Errors)> SetupAuthenticatorAsync(
        string email, string verificationToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var antiforgeryToken = await antiforgery.GetTokenAsync(
                ConsoleAntiforgeryService.AntiforgeryService.Readers, cancellationToken);

            using var response = await readerAccountApi.ExecuteAsync((opt, api) =>
                api.SetupAuthenticator(
                    new SetupReaderAccountAuthenticatorDto { Email = email, VerificationToken = verificationToken },
                    antiforgeryToken, opt), o => o.WithCancellation(cancellationToken));
            var data = response.ToData<SetupReaderAccountAuthenticatorResultDto>(out var errors);
            if (errors.Count > 0)
            {
                antiforgery.Invalidate(ConsoleAntiforgeryService.AntiforgeryService.Readers);
                return (false, null, errors);
            }

            return (data is not default(SetupReaderAccountAuthenticatorResultDto), data, errors);
        }
        catch (Exception ex)
        {
            return (false, null, [Describe(ex)]);
        }
    }

    public async Task<(bool Success, List<string> Errors)> ConfirmWithAuthenticatorAsync(
        ConfirmCreateReaderAccountWithAuthenticatorDto confirm, Action<string> onStepProgress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var antiforgeryToken = await antiforgery.GetTokenAsync(
                ConsoleAntiforgeryService.AntiforgeryService.Readers, cancellationToken);

        var httpClient = httpClientFactory.CreateClient(SseClientNames.ReadersManagement);
        var frames = httpClient.PostServerSentEventsAsync(
            "create/confirm/totp/v1",
            confirm,
            message => message.Headers.Add("RequestVerificationToken", antiforgeryToken),
            cancellationToken);

        var finalEvent = await StepProgressStreamReader.ReadAsync(frames, onStepProgress, cancellationToken);
        if (finalEvent is not default(StepProgressFinalEvent) && finalEvent.StatusCode == (int)HttpStatusCode.OK)
        {
            return (true, []);
        }

        var errors = finalEvent?.Response?.Errors?.ToList() ?? [];
        if (errors.Count == 0)
        {
            errors.Add("Registration confirmation failed.");
        }

        return (false, errors);
        }
        catch (Exception ex)
        {
            return (false, [Describe(ex)]);
        }
    }

    private static string Describe(Exception ex) =>
        ex.InnerException is not null ? $"{ex.Message}: {ex.InnerException.Message}" : ex.Message;
}
