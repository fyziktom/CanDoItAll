using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class PartyMergeSessionTests {
    [Fact]
    public async Task A_retired_preview_cannot_replace_a_new_merge_opening() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var manager = services.GetRequiredService<PartyDirectoryManagementService>();
        var (retained, first, second) = await CreateCandidatesAsync(services);
        var access = new ControlledAccess { HoldNextOpen = true };
        var session = new PartyMergeSession(manager, services.GetRequiredService<CrmHrChanges>(), access, NullLogger<PartyMergeSession>.Instance);
        var oldOpening = session.OpenAsync(retained, first);
        await access.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        session.Reset();
        await session.OpenAsync(retained, second);
        var current = session.Presentation;
        access.Release.TrySetResult();
        await oldOpening;

        Assert.Same(current, session.Presentation);
        Assert.Equal(second.Id, session.Presentation.Preview!.Duplicate.Id);
        Assert.True(session.Presentation.CanConfirm);
    }

    [Fact]
    public async Task An_unknown_result_keeps_note_status_and_blocks_commands_until_a_current_read_succeeds() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var (retained, first, _) = await CreateCandidatesAsync(services);
        var access = new ControlledAccess();
        var session = new PartyMergeSession(services.GetRequiredService<PartyDirectoryManagementService>(),
            services.GetRequiredService<CrmHrChanges>(), access, NullLogger<PartyMergeSession>.Instance);
        await session.OpenAsync(retained, first);
        session.Note = new(8, true);
        access.FailValidation = true;

        Assert.False(await session.ConfirmAsync("Keep this note"));

        Assert.True(session.Presentation.IsUnknown);
        Assert.Equal(8, session.Note.Revision);
        Assert.True(session.HasLocalChanges);
        Assert.False(await session.ConfirmAsync("No replay"));
        await session.RefreshAsync();
        Assert.True(session.Presentation.IsUnknown);
        access.FailValidation = false;
        await session.RefreshAsync();
        Assert.True(session.Presentation.CanConfirm);
        Assert.Equal(8, session.Note.Revision);
        Assert.NotNull(await services.GetRequiredService<PartyDirectoryService>().GetPartyAsync(first.Id));
    }

    private static async Task<(Guid Retained, PartyDuplicateCandidateModel First, PartyDuplicateCandidateModel Second)> CreateCandidatesAsync(IServiceProvider services) {
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var name = $"Session candidates {Guid.NewGuid():N}";
        var ids = new List<Guid>();
        for (var index = 0; index < 3; index++) {
            var result = await parties.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Person, DisplayName = name });
            Assert.True(result.IsSuccess);
            ids.Add(result.Value);
        }
        var candidates = await services.GetRequiredService<PartyDirectoryManagementService>().FindPotentialDuplicatesAsync(ids[0]);
        return (ids[0], Assert.Single(candidates, row => row.Id == ids[1]), Assert.Single(candidates, row => row.Id == ids[2]));
    }

    private sealed class ControlledAccess : IEditingUiAccess {
        public bool HoldNextOpen { get; set; }
        public bool FailValidation { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) {
            if (HoldNextOpen) {
                HoldNextOpen = false;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return new(topic, "session-test", "session-test");
        }
        public ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) => FailValidation
            ? ValueTask.FromException(new IOException("Controlled access transport failure.")) : ValueTask.CompletedTask;
    }
}
