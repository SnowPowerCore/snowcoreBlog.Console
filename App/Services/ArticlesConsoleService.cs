using Apizr;
using snowcoreBlog.Console.App.Models;
using snowcoreBlog.PublicApi.Api;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Extensions;

namespace snowcoreBlog.Console.App.Services;

/// <summary>
/// Articles reads/writes for the terminal home page, article details and the
/// author-only create form (console counterpart of the Articles module pages).
/// </summary>
public sealed class ArticlesConsoleService(
    IApizrManager<IArticlesApi> articlesApi,
    ConsoleAntiforgeryService antiforgery)
{
    public async Task<(List<ArticleDto>? Articles, List<string> Errors)> GetArticlesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await articlesApi.ExecuteAsync(
                static (opt, api) => api.GetArticlesCached(opt), o => o.WithCancellation(cancellationToken));
            var data = response.ToData<List<ArticleDto>>(out var errors);
            if (errors.Count > 0)
            {
                antiforgery.Invalidate(ConsoleAntiforgeryService.AntiforgeryService.Articles);
            }

            return (data, errors);
        }
        catch (Exception ex)
        {
            return (null, [Describe(ex)]);
        }
    }

    public async Task<(ArticleDto? Article, List<string> Errors)> GetArticleBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await articlesApi.ExecuteAsync(
                (opt, api) => api.GetArticleBySlug(slug, opt), o => o.WithCancellation(cancellationToken));
            var data = response.ToData<ArticleDto>(out var errors);
            if (errors.Count > 0)
            {
                antiforgery.Invalidate(ConsoleAntiforgeryService.AntiforgeryService.Articles);
            }

            return (data, errors);
        }
        catch (Exception ex)
        {
            return (null, [Describe(ex)]);
        }
    }

    public async Task<(bool Success, CreateArticleResultDto? Result, List<string> Errors)> CreateArticleAsync(
        CreateArticleDto article, CancellationToken cancellationToken = default)
    {
        string antiforgeryToken;
        try
        {
            antiforgeryToken = await antiforgery.GetTokenAsync(
                ConsoleAntiforgeryService.AntiforgeryService.Articles, cancellationToken);
        }
        catch (Exception ex)
        {
            return (false, null, [Describe(ex)]);
        }

        try
        {
            using var response = await articlesApi.ExecuteAsync(
                (opt, api) => api.CreateArticle(article, antiforgeryToken, opt), o => o.WithCancellation(cancellationToken));
            var data = response.ToData<CreateArticleResultDto>(out var errors);
            if (errors.Count > 0)
            {
                antiforgery.Invalidate(ConsoleAntiforgeryService.AntiforgeryService.Articles);
                return (false, null, errors);
            }

            return (data is not default(CreateArticleResultDto), data, errors);
        }
        catch (Exception ex)
        {
            return (false, null, [Describe(ex)]);
        }
    }

    private static string Describe(Exception ex) =>
        ex.InnerException is not null ? $"{ex.Message}: {ex.InnerException.Message}" : ex.Message;
}
