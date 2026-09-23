using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Prompts.Components;

public enum PromptGalleryNoticeSeverity
{
    Success,
    Warning,
    Error
}

// Typed user notice raised by a session; the owning host maps it onto the application NotificationService.
public sealed record PromptGalleryNotice(
    PromptGalleryNoticeSeverity Severity,
    string Summary,
    string Detail);

internal static class PromptGalleryNotices
{
    public static void Show(NotificationService notifications, PromptGalleryNotice notice)
    {
        switch (notice.Severity)
        {
            case PromptGalleryNoticeSeverity.Success:
                notifications.Success(notice.Summary, notice.Detail);
                break;
            case PromptGalleryNoticeSeverity.Warning:
                notifications.Warning(notice.Summary, notice.Detail);
                break;
            default:
                notifications.Error(notice.Summary, notice.Detail);
                break;
        }
    }

    public static string DescribeErrors(IReadOnlyList<Error> errors, string fallback)
        => errors.Count == 0 ? fallback : string.Join(" ", errors.Select(error => error.Message));
}
