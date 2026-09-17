using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The projection of the query owner's activity history page into the timeline presentation.
public sealed class CrmHrActivityPresentationMapperTests
{
    [Fact]
    public void Maps_page_fields_whole_history_counts_and_entries_in_the_given_order()
    {
        var first = new CrmAccountActivityTimelineItemModel(Guid.NewGuid(), "Interaction", "Quarterly review", "Confirmed scope.", "Meeting / Bram Vos / Next action due 2026-03-20", new DateTimeOffset(2026, 3, 9, 14, 30, 0, TimeSpan.Zero), "info", IsOverdue: true);
        var second = new CrmAccountActivityTimelineItemModel(Guid.NewGuid(), "Audit", "CRM account profile saved", "Updated", "crm-hr-ui", new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero), "neutral", IsOverdue: false);
        var page = new CrmActivityHistoryPage([first, second], PageIndex: 1, PageSize: 10, TotalCount: 23, ActionCount: 5, OverdueActionCount: 2);

        var presentation = CrmHrActivityPresentationMapper.ToPage(page);

        Assert.Equal(1, presentation.PageIndex);
        Assert.Equal(10, presentation.PageSize);
        Assert.Equal(23, presentation.TotalCount);
        Assert.Equal(5, presentation.ActionCount);
        Assert.Equal(2, presentation.OverdueActionCount);
        Assert.Equal(3, presentation.TotalPages);
        Assert.Equal(new[] { first.Id, second.Id }, presentation.Items.Select(item => item.Id));
        var entry = presentation.Items[0];
        Assert.Equal("Interaction", entry.Kind);
        Assert.Equal("Quarterly review", entry.Title);
        Assert.Equal("Confirmed scope.", entry.Description);
        Assert.Equal("Meeting / Bram Vos / Next action due 2026-03-20", entry.Meta);
        Assert.Equal(first.OccurredAtUtc, entry.OccurredAtUtc);
        Assert.Equal(CrmHrActivityTone.Info, entry.Tone);
        Assert.True(entry.IsOverdue);
        Assert.Equal(CrmHrActivityTone.Neutral, presentation.Items[1].Tone);
        Assert.False(presentation.Items[1].IsOverdue);
    }

    [Fact]
    public void Blank_description_becomes_absent_and_the_tone_token_is_parsed_case_insensitively()
    {
        var item = new CrmAccountActivityTimelineItemModel(Guid.NewGuid(), "Interaction", "Chat follow-up", "   ", "Message / Bram Vos", DateTimeOffset.UnixEpoch, " WARNING ", IsOverdue: false);

        var entry = CrmHrActivityPresentationMapper.ToEntry(item);

        Assert.Null(entry.Description);
        Assert.Equal(CrmHrActivityTone.Warning, entry.Tone);
    }

    [Fact]
    public void An_empty_page_maps_to_an_empty_presentation_with_no_pages()
    {
        var presentation = CrmHrActivityPresentationMapper.ToPage(CrmActivityHistoryPage.Empty());

        Assert.Empty(presentation.Items);
        Assert.Equal(0, presentation.TotalPages);
        Assert.Equal(CrmActivityHistoryQueryLimits.DefaultPageSize, presentation.PageSize);
        Assert.Equal(0, presentation.ActionCount);
        Assert.Equal(0, presentation.OverdueActionCount);
    }

    [Theory]
    [InlineData("info", CrmHrActivityTone.Info)]
    [InlineData("success", CrmHrActivityTone.Success)]
    [InlineData("warning", CrmHrActivityTone.Warning)]
    [InlineData("danger", CrmHrActivityTone.Danger)]
    [InlineData("neutral", CrmHrActivityTone.Neutral)]
    [InlineData("purple", CrmHrActivityTone.Neutral)]
    [InlineData("", CrmHrActivityTone.Neutral)]
    [InlineData(null, CrmHrActivityTone.Neutral)]
    public void Tone_tokens_map_to_typed_tones_with_a_neutral_fallback(string? token, CrmHrActivityTone expected)
        => Assert.Equal(expected, CrmHrActivityPresentationMapper.ToTone(token));
}
