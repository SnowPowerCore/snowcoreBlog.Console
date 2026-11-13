using Apizr;
using snowcoreBlog.LocalStorage.Interfaces;
using snowcoreBlog.PublicApi.Api;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Extensions;

namespace snowcoreBlog.Console.App.Services;

/// <summary>
/// Active technical notifications for the layout top bar (console counterpart of
/// the ServiceNotifications NotificationBar). Dismissed ids persist in local
/// storage, mirroring the frontend NotificationBarState dismissal behaviour.
/// </summary>
public sealed class NotificationsConsoleService(
    IApizrManager<INotificationsApi> notificationsApi,
    ILocalStorageService localStorage)
{
    private const string DismissedKey = "console-dismissed-notification-ids";

    public async Task<List<NotificationDto>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await notificationsApi.ExecuteAsync(
                static (opt, api) => api.GetActiveNotifications(new GetActiveNotificationsDto { MaxCount = 5 }, opt),
                o => o.WithCancellation(cancellationToken));
            var data = response.ToData<List<NotificationDto>>(out _);
            return data ?? [];
        }
        catch
        {
            // Notifications are auxiliary; never block the UI on their failure.
            return [];
        }
    }

    public async Task<IReadOnlySet<Guid>> GetDismissedAsync()
    {
        // The local storage implementation THROWS on a missing key, so probe first.
        if (!localStorage.HasKey(DismissedKey))
        {
            return new HashSet<Guid>();
        }

        var dismissed = localStorage.Get<List<Guid>>(DismissedKey);
        return dismissed is default(List<Guid>) ? new HashSet<Guid>() : new HashSet<Guid>(dismissed);
    }

    public void Dismiss(Guid id)
    {
        var dismissed = localStorage.HasKey(DismissedKey)
            ? localStorage.Get<List<Guid>>(DismissedKey) ?? []
            : [];
        if (dismissed.Contains(id))
        {
            return;
        }

        dismissed.Add(id);
        localStorage.Set(DismissedKey, dismissed);
    }
}
