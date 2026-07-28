using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PartyPickerTests
{
    [Fact]
    public void Treats_an_empty_identifier_as_no_selection()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        var queryService = new StubPartyRecordQueryService(
            CreatePartyRecordQueryItem(Guid.NewGuid(), "Current party"),
            CreatePartyRecordQueryItem(Guid.NewGuid(), "Replacement party"));
        context.Services.AddSingleton<IPartyRecordQueryService>(queryService);

        var cut = context.Render<PartyPicker>(parameters => parameters
            .Add(component => component.SelectedPartyId, Guid.Empty));

        Assert.Contains("No party selected", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(0, queryService.GetRequestCount);
    }

    [Fact]
    public void Selects_and_clears_through_the_server_paged_directory_dialog()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        var currentId = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        var queryService = new StubPartyRecordQueryService(
            new PartyRecordQueryItem(
                currentId,
                "Current party",
                PartyType.Person,
                PartyLifecycleStatus.Active,
                "P-100",
                "Current summary",
                ["delivery"],
                false),
            new PartyRecordQueryItem(
                replacementId,
                "Replacement party",
                PartyType.Person,
                PartyLifecycleStatus.Active,
                "P-200",
                "Replacement summary",
                ["delivery"],
                false));
        context.Services.AddSingleton<IPartyRecordQueryService>(queryService);
        Guid? selectedId = currentId;

        var cut = context.Render<PartyPicker>(parameters => parameters
            .Add(component => component.Label, "Assignment party")
            .Add(component => component.TestIdPrefix, "assignment-party")
            .Add(component => component.SelectedPartyId, currentId)
            .Add(component => component.ExcludedPartyId, currentId)
            .Add(
                component => component.SelectedPartyIdChanged,
                (Guid? value) => selectedId = value));

        Assert.Contains("Current party", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(1, queryService.GetRequestCount);
        cut.Find("[data-testid='assignment-party-clear']").Click();
        Assert.Null(selectedId);

        cut.Find("[data-testid='assignment-party-select']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll($"[data-testid='crmhr-party-option-{replacementId:N}']"));
            Assert.Equal(1, queryService.RequestCount);
            Assert.Equal(currentId, queryService.LastQuery?.ExcludedPartyId);
        });

        cut.Find($"[data-testid='crmhr-party-option-{replacementId:N}']").Click();
        cut.Find("[data-testid='assignment-party-dialog-confirm']").Click();

        Assert.Equal(replacementId, selectedId);
        Assert.Empty(cut.FindAll("[data-testid='assignment-party-dialog']"));
    }

    private static PartyRecordQueryItem CreatePartyRecordQueryItem(
        Guid id,
        string displayName)
    {
        return new PartyRecordQueryItem(
            id,
            displayName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            string.Empty,
            string.Empty,
            [],
            false);
    }

    private sealed class StubPartyRecordQueryService(
        PartyRecordQueryItem currentItem,
        PartyRecordQueryItem replacementItem) : IPartyRecordQueryService
    {
        public int GetRequestCount { get; private set; }

        public int RequestCount { get; private set; }

        public PartyRecordQuery? LastQuery { get; private set; }

        public Task<PartyRecordQueryItem?> GetAsync(
            Guid partyId,
            bool includeArchived = false,
            CancellationToken cancellationToken = default)
        {
            GetRequestCount++;
            return Task.FromResult<PartyRecordQueryItem?>(
                currentItem.Id == partyId ? currentItem : null);
        }

        public Task<PartyRecordPage> SearchAsync(
            PartyRecordQuery query,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            LastQuery = query;
            return Task.FromResult(
                new PartyRecordPage(
                    [replacementItem],
                    query.PageIndex,
                    query.PageSize,
                    1));
        }
    }
}
