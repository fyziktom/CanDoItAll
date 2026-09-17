using CanDoItAll.CrmHr.UI.Activity;

namespace CanDoItAll.Modules.CrmHr.Components;

// Projects an activity history page from the query owner into the timeline presentation. Order, paging and the
// whole-history counts are carried over unchanged; the tone token the query owner emits becomes the typed tone.
public static class CrmHrActivityPresentationMapper
{
    public static CrmHrActivityPage ToPage(CrmActivityHistoryPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new CrmHrActivityPage(
            page.Items.Select(ToEntry).ToArray(),
            page.PageIndex,
            page.PageSize,
            page.TotalCount,
            page.ActionCount,
            page.OverdueActionCount);
    }

    public static CrmHrActivityEntry ToEntry(CrmAccountActivityTimelineItemModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new CrmHrActivityEntry(
            item.Id,
            item.Kind,
            item.Title,
            string.IsNullOrWhiteSpace(item.Description) ? null : item.Description,
            item.Meta,
            item.OccurredAtUtc,
            ToTone(item.Tone),
            item.IsOverdue);
    }

    public static CrmHrActivityTone ToTone(string? tone)
        => tone?.Trim().ToLowerInvariant() switch
        {
            "info" => CrmHrActivityTone.Info,
            "success" => CrmHrActivityTone.Success,
            "warning" => CrmHrActivityTone.Warning,
            "danger" => CrmHrActivityTone.Danger,
            _ => CrmHrActivityTone.Neutral
        };
}
