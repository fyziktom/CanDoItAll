using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Catalog;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.UI.Tests;

public sealed class AgentDecisionPolicyStateTests {
    [Fact]
    public void Blank_fields_inherit_while_zero_and_exact_durations_are_preserved() {
        var draft = new AgentDecisionPolicyDraft();
        Assert.Equal(new DecisionDataPolicyOverride(), draft.Capture());
        draft.MaximumReplans = 0;
        draft.MaximumWorkDuration = "1.00:00:00.0000001";
        var captured = draft.Capture();
        Assert.Equal(0, captured.MaximumReplans);
        Assert.Equal(TimeSpan.FromDays(1).Add(TimeSpan.FromTicks(1)), captured.MaximumWorkDuration);
        draft.Load(captured);
        Assert.Equal(captured, draft.Capture());
        draft.MaximumWorkDuration = " 00:01:00 ";
        Assert.Equal(TimeSpan.FromMinutes(1), draft.Capture().MaximumWorkDuration);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:01:00")]
    [InlineData("one minute")]
    [InlineData("00:01:00,5")]
    public void Invalid_durations_do_not_admit_a_submission(string value) {
        var state = Ready();
        state.Draft.MaximumWorkDuration = value;
        Assert.Throws<ArgumentException>(() => state.BeginSave());
        Assert.Null(state.Submitted);
        Assert.False(state.Busy);
        Assert.Equal(value, state.Draft.MaximumWorkDuration);
    }

    [Fact]
    public void Invalid_modes_and_negative_allowances_cannot_be_captured() {
        var draft = new AgentDecisionPolicyDraft { ReadBasisMode = (ReadBasisMode)99 };
        Assert.Throws<JsonException>(() => draft.Capture());
        draft.ReadBasisMode = null;
        draft.MaximumReplans = -1;
        Assert.Throws<JsonException>(() => draft.Capture());
    }

    [Fact]
    public void Acknowledgement_advances_only_its_original_version_and_preserves_later_typing() {
        var state = Ready();
        state.Draft.ReadBasisMode = ReadBasisMode.RunSnapshot;
        var command = state.BeginSave()!;
        Assert.False(state.BeginRead());
        Assert.Null(state.BeginSave());
        state.Draft.ReadBasisMode = ReadBasisMode.StepSnapshot;
        state.Draft.MaximumWorkDuration = "invalid later draft";
        var accepted = Accepted(command);
        state.CompleteSave(command, accepted);
        Assert.Equal(accepted.Snapshot, state.Original);
        Assert.Equal(ReadBasisMode.RunSnapshot, command.Policy.ReadBasisMode);
        Assert.Equal(ReadBasisMode.StepSnapshot, state.Draft.ReadBasisMode);
        Assert.Equal("invalid later draft", state.Draft.MaximumWorkDuration);
        state.CatalogRefreshFailed();
        Assert.Equal(accepted.Snapshot, state.Original);
        Assert.Null(state.Unconfirmed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Conflict_requires_read_and_explicit_reconciliation(bool keepDraft) {
        var state = Ready();
        var original = state.Original!;
        state.Draft.ReadBasisMode = ReadBasisMode.RunSnapshot;
        var command = state.BeginSave()!;
        state.CompleteSave(command, new AgentDecisionDataPolicySaveOutcome.Rejected(AgentDecisionDataPolicyRejection.Conflict));
        Assert.Null(state.BeginSave());
        Assert.Equal(original, state.Original);
        Assert.True(state.BeginRead());
        state.CompleteRead(new(new(original.Expectation.Workspace, state.AgentId, new(8)), new(ReadBasisMode.StepSnapshot)));
        Assert.Equal(original, state.Original);
        Assert.False(state.CanSave);
        state.AdoptCurrent(keepDraft);
        Assert.Equal(new CatalogDataRevision(8), state.Original!.Expectation.CatalogRevision);
        Assert.Equal(keepDraft ? ReadBasisMode.RunSnapshot : ReadBasisMode.StepSnapshot, state.Draft.ReadBasisMode);
        Assert.True(state.CanSave);
        Assert.NotEqual(command.OperationId, state.BeginSave()!.OperationId);
    }

    [Fact]
    public void Equal_current_values_do_not_confirm_an_unknown_save_or_enable_resubmission() {
        var state = Ready();
        var command = state.BeginSave()!;
        state.CompleteSave(command, new AgentDecisionDataPolicySaveOutcome.Unconfirmed(command));
        Assert.True(state.BeginRead());
        state.CompleteRead(Accepted(command).Snapshot);
        Assert.Equal(command, state.Unconfirmed);
        Assert.False(state.CanSave);
        Assert.False(state.CanAdopt);
        state.AdoptCurrent(true);
        Assert.Equal(command.Expectation, state.Original!.Expectation);
        Assert.Null(state.BeginSave());
    }

    [Fact]
    public void Failed_read_and_foreign_observation_cannot_replace_original_authority() {
        var state = Ready();
        var original = state.Original;
        state.Draft.MaximumReplans = 2;
        Assert.True(state.BeginRead());
        var other = Ready();
        Assert.Throws<InvalidOperationException>(() => state.CompleteRead(other.Original!));
        state.ReadFailed();
        Assert.Null(state.Current);
        Assert.Equal(original, state.Original);
        Assert.Equal(2, state.Draft.MaximumReplans);
        Assert.False(state.CanSave);
        Assert.False(state.CanAdopt);
    }

    [Fact]
    public void Retired_opening_cannot_read_save_or_enable_actions_after_late_acknowledgement() {
        var state = Ready();
        var command = state.BeginSave()!;
        state.Retire();
        state.CompleteSave(command, Accepted(command));
        Assert.False(state.CanSave);
        Assert.False(state.BeginRead());
        Assert.Null(state.BeginSave());
        Assert.Equal(command.Policy, state.Original!.Policy);
    }

    private static AgentDecisionPolicyEditorState Ready() {
        var state = new AgentDecisionPolicyEditorState(Guid.NewGuid());
        Assert.True(state.BeginRead());
        state.CompleteRead(new(new(new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1)), state.AgentId, new(4)), null));
        state.CompleteRecovery(null);
        return state;
    }
    private static AgentDecisionDataPolicySaveOutcome.Accepted Accepted(ReplaceAgentDecisionDataPolicyCommand command)
        => new(command, new(new(command.Expectation.Workspace, command.Expectation.AgentId, command.Expectation.CatalogRevision.Next()), command.Policy));
}
