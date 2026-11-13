using System.Text.RegularExpressions;
using snowcoreBlog.Console.App.Resources;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Constants;

namespace snowcoreBlog.Console.App.Services;

/// <summary>
/// Presentation helpers shared by the article screens: step progress translation
/// and markdown excerpt derivation (console counterpart of the frontend
/// RegistrationProgressMessages and the Highlight/ArticlesList excerpt rules).
/// </summary>
public static partial class ConsolePresentation
{
    [GeneratedRegex(@"[#*`_\[\]()>~\-]")]
    private static partial Regex MarkdownCharsRegex();

    public static string ForStep(string stepCode) => stepCode switch
    {
        ReaderAccountStepProgressCodes.ValidatingEmailDomain => Resource.RegistrationStepValidatingEmailDomain,
        ReaderAccountStepProgressCodes.CheckingAccountExists => Resource.RegistrationStepCheckingAccountExists,
        ReaderAccountStepProgressCodes.CheckingPendingRegistration => Resource.RegistrationStepCheckingPendingRegistration,
        ReaderAccountStepProgressCodes.CheckingNickNameAvailability => Resource.RegistrationStepCheckingNickNameAvailability,
        ReaderAccountStepProgressCodes.CreatingTempUser => Resource.RegistrationStepCreatingTempUser,
        ReaderAccountStepProgressCodes.CreatingUserAccount => Resource.RegistrationStepCreatingUserAccount,
        ReaderAccountStepProgressCodes.CreatingReaderProfile => Resource.RegistrationStepCreatingReaderProfile,
        _ => Resource.RegistrationStepWorkingGeneric
    };

    /// <summary>
    /// Derives a plain-text excerpt from article markdown (strips markdown
    /// characters first, exactly like the frontend list cards).
    /// </summary>
    public static string ExcerptOf(ArticleDto article, int maxLength)
    {
        var source = article.Excerpt;
        if (string.IsNullOrWhiteSpace(source))
        {
            source = MarkdownCharsRegex().Replace(article.Markdown ?? string.Empty, string.Empty);
            source = source.Replace("\r", " ").Replace("\n", " ");
        }

        source = source.Trim();
        return source.Length <= maxLength ? source : source[..maxLength] + "...";
    }

    public static string DateOf(DateTimeOffset? date) =>
        date?.LocalDateTime.ToString("yyyy-MM-dd") ?? string.Empty;
}
