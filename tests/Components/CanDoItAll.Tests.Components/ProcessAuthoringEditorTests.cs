using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.UI;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Processes;

public sealed class ProcessAuthoringEditorTests {
    private static readonly ProcessDefinitionCatalogItemKey Definition = new("architecture-review");

    [Theory]
    [InlineData(ProcessDefinitionRoleCommandKind.AddRole)]
    [InlineData(ProcessDefinitionRoleCommandKind.DeleteRole)]
    public async Task Role_identity_follows_actual_owner_result(ProcessDefinitionRoleCommandKind kind) {
        using var context = Context();
        await using var environment = CanDoItAllTestEnvironment.Create("pc2-role-editor");
        var owner = new ProcessDefinitionRoleEditorProjectionService(TemplatePack(environment.RootPath), new SystemProcessProjectionClock());
        var state = new ProcessRoleEditorState { Scope = ProcessWorkspaceShellScope.Global, RoleEditor = await owner.GetEditorAsync(ProcessWorkspaceShellScope.Global, Definition) };
        state.Observe();
        var original = state.SelectedRoleKey!.Value;
        await state.OpenRoleDetailsAsync(original);
        ProcessDefinitionRoleEditorProjection? accepted = null;
        state.ExecuteCommand = EventCallback.Factory.Create<ProcessDefinitionRoleEditorCommand>(state, async command => {
            var result = await owner.ExecuteCommandAsync(command);
            Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, result.Receipt.Status);
            accepted = result.Projection;
            state.RoleDisplayName = "Deleted or previous draft";
            state.RoleDefaultAllocationPercentInput = "invalid";
            state.UpdateWorkflowDefinitionId("invalid-guid");
            state.Accept(accepted);
        });
        var cut = context.Render<ProcessDefinitionRoleEditorPanel>(p => p.Add(c => c.State, state).Add(c => c.RoleEditor, state.RoleEditor).Add(c => c.Scope, state.Scope).Add(c => c.ExecuteCommand, state.ExecuteCommand));
        await cut.InvokeAsync(() => state.ExecuteAsync(kind));
        Assert.NotNull(accepted);
        cut.Render(p => p.Add(c => c.RoleEditor, accepted));
        Assert.Equal(accepted.SelectedRoleKey, state.SelectedRoleKey);
        Assert.NotEqual(original, state.SelectedRoleKey);
        if (accepted.SelectedRole is { } selected) {
            Assert.Equal(selected.Draft.DisplayName, state.RoleDisplayName);
            Assert.Equal(selected.RoleKey, state.CreateDraft().RoleKey);
        } else {
            Assert.Null(state.ActiveDialogRole);
            Assert.False(state.RoleDetailDialogOpen);
            Assert.Empty(state.RoleDisplayName);
            Assert.False(state.IsDirty);
            Assert.False(state.HasWorkflowInputErrors);
            Assert.Empty(cut.FindAll("[data-testid='processes-role-editor-form']"));
        }
    }

    [Theory]
    [InlineData(ProcessDefinitionRoleCommandKind.AddRole)]
    [InlineData(ProcessDefinitionRoleCommandKind.DeleteRole)]
    public async Task Later_explicit_role_selection_is_not_hijacked(ProcessDefinitionRoleCommandKind kind) {
        await using var environment = CanDoItAllTestEnvironment.Create("pc2-role-selection");
        var owner = new ProcessDefinitionRoleEditorProjectionService(TemplatePack(environment.RootPath), new SystemProcessProjectionClock());
        var initial = await owner.GetEditorAsync(ProcessWorkspaceShellScope.Global, Definition);
        var seeded = await owner.ExecuteCommandAsync(new(ProcessWorkspaceShellScope.Global, Definition,
            ProcessDefinitionRoleCommandKind.AddRole, initial.VersionToken, initial.SelectedRole!.Draft, initial.TemplateActions[0].ActionKey));
        var state = new ProcessRoleEditorState { Scope = ProcessWorkspaceShellScope.Global, RoleEditor = seeded.Projection };
        state.Observe();
        var later = initial.SelectedRoleKey!.Value;
        state.ExecuteCommand = EventCallback.Factory.Create<ProcessDefinitionRoleEditorCommand>(state, async command => {
            var result = await owner.ExecuteCommandAsync(command);
            Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, result.Receipt.Status);
            await state.OpenRoleDetailsAsync(later);
            state.RolePurpose = "Later local purpose";
            state.Accept(result.Projection);
            state.RoleEditor = result.Projection;
            state.Observe();
        });
        await state.ExecuteAsync(kind);
        Assert.Equal(later, state.SelectedRoleKey);
        Assert.Equal("Later local purpose", state.RolePurpose);
        Assert.Equal(later, state.CreateDraft().RoleKey);
        Assert.NotNull(state.RoleEditor.LastCommandReceipt);
    }

    [Fact]
    public async Task Delete_selects_survivor_without_copying_deleted_fields() {
        await using var environment = CanDoItAllTestEnvironment.Create("pc2-role-delete-survivor");
        var owner = new ProcessDefinitionRoleEditorProjectionService(TemplatePack(environment.RootPath), new SystemProcessProjectionClock());
        var initial = await owner.GetEditorAsync(ProcessWorkspaceShellScope.Global, Definition);
        var added = await owner.ExecuteCommandAsync(new(ProcessWorkspaceShellScope.Global, Definition,
            ProcessDefinitionRoleCommandKind.AddRole, initial.VersionToken, initial.SelectedRole!.Draft, initial.TemplateActions[0].ActionKey));
        var state = new ProcessRoleEditorState { Scope = ProcessWorkspaceShellScope.Global, RoleEditor = added.Projection };
        state.Observe();
        await state.OpenRoleDetailsAsync(state.SelectedRoleKey!.Value);
        state.ExecuteCommand = EventCallback.Factory.Create<ProcessDefinitionRoleEditorCommand>(state, async command => {
            var result = await owner.ExecuteCommandAsync(command);
            Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, result.Receipt.Status);
            state.RolePurpose = "Must not move to survivor";
            state.Accept(result.Projection);
        });
        await state.ExecuteAsync(ProcessDefinitionRoleCommandKind.DeleteRole);
        Assert.Equal(initial.SelectedRoleKey, state.SelectedRoleKey);
        Assert.Equal(initial.SelectedRole.Draft.Purpose, state.RolePurpose);
        Assert.False(state.IsDirty);
        Assert.True(state.RoleDetailDialogOpen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refusal_then_reachable_discard_does_not_leave_an_acceptable_submission(bool step) {
        using var context = Context();
        await using var environment = CanDoItAllTestEnvironment.Create("pc2-discard");
        var pack = TemplatePack(environment.RootPath);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (step) {
            var owner = new ProcessDefinitionStepEditorProjectionService(pack, new SystemProcessProjectionClock());
            var state = new ProcessStepEditorState { Scope = ProcessWorkspaceShellScope.Global, StepEditor = await owner.GetEditorAsync(ProcessWorkspaceShellScope.Global, Definition) };
            state.Observe();
            state.StepTitle = string.Empty;
            ProcessDefinitionStepEditorProjection? rejected = null;
            state.ExecuteCommand = EventCallback.Factory.Create<ProcessDefinitionStepEditorCommand>(state, async command => {
                var result = await owner.ExecuteCommandAsync(command);
                Assert.Equal(ProcessDefinitionStepCommandStatus.Rejected, result.Receipt.Status);
                rejected = result.Projection;
                await pending.Task;
            });
            var cut = context.Render<ProcessDefinitionStepEditorPanel>(p => p.Add(c => c.State, state).Add(c => c.StepEditor, state.StepEditor).Add(c => c.Scope, state.Scope).Add(c => c.ExecuteCommand, state.ExecuteCommand));
            var running = cut.InvokeAsync(() => state.ExecuteAsync(ProcessDefinitionStepCommandKind.SaveStep));
            cut.Render(p => p.Add(c => c.Disabled, true));
            Assert.True(cut.Find("[data-testid='processes-step-discard']").HasAttribute("disabled"));
            pending.SetResult();
            await running;
            cut.Render(p => p.Add(c => c.Disabled, false).Add(c => c.StepEditor, rejected!));
            cut.Find("[data-testid='processes-step-discard']").Click();
            Assert.False(state.IsDirty);
            var version = state.SyncedVersionToken;
            state.Accept(rejected! with { VersionToken = new("retired-submission") });
            Assert.Equal(version, state.SyncedVersionToken);
        } else {
            var owner = new ProcessDefinitionRoleEditorProjectionService(pack, new SystemProcessProjectionClock());
            var state = new ProcessRoleEditorState { Scope = ProcessWorkspaceShellScope.Global, RoleEditor = await owner.GetEditorAsync(ProcessWorkspaceShellScope.Global, Definition) };
            state.Observe();
            state.RoleDisplayName = string.Empty;
            ProcessDefinitionRoleEditorProjection? rejected = null;
            state.ExecuteCommand = EventCallback.Factory.Create<ProcessDefinitionRoleEditorCommand>(state, async command => {
                var result = await owner.ExecuteCommandAsync(command);
                Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, result.Receipt.Status);
                rejected = result.Projection;
                await pending.Task;
            });
            var cut = context.Render<ProcessDefinitionRoleEditorPanel>(p => p.Add(c => c.State, state).Add(c => c.RoleEditor, state.RoleEditor).Add(c => c.Scope, state.Scope).Add(c => c.ExecuteCommand, state.ExecuteCommand));
            var running = cut.InvokeAsync(() => state.ExecuteAsync(ProcessDefinitionRoleCommandKind.SaveRole));
            cut.Render(p => p.Add(c => c.Disabled, true));
            Assert.True(cut.Find("[data-testid='processes-role-discard']").HasAttribute("disabled"));
            pending.SetResult();
            await running;
            cut.Render(p => p.Add(c => c.Disabled, false).Add(c => c.RoleEditor, rejected!));
            cut.Find("[data-testid='processes-role-discard']").Click();
            Assert.False(state.IsDirty);
            var version = state.SyncedVersionToken;
            state.Accept(rejected! with { VersionToken = new("retired-submission") });
            Assert.Equal(version, state.SyncedVersionToken);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("second-authority")]
    public async Task Saving_second_step_preserves_its_actual_decision_authority(string? role) {
        await using var environment = CanDoItAllTestEnvironment.Create("pc2-step-identity");
        var owner = new ProcessDefinitionStepEditorProjectionService(TemplatePack(environment.RootPath), new SystemProcessProjectionClock());
        var editor = await owner.GetEditorAsync(ProcessWorkspaceShellScope.Global, Definition);
        var first = editor.SelectedStep!;
        var second = first with { Basic = first.Basic with { StepKey = new("second-step"), DecisionRoleKey = role is null ? null : new(role) } };
        var added = await owner.ExecuteCommandAsync(new(ProcessWorkspaceShellScope.Global, Definition, ProcessDefinitionStepCommandKind.SaveStep, editor.VersionToken, second));
        Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, added.Receipt.Status);
        editor = added.Projection with { SelectedStepKey = first.Basic.StepKey, SelectedStep = first };
        using var context = Context();
        var state = new ProcessStepEditorState { Scope = ProcessWorkspaceShellScope.Global, StepEditor = editor };
        state.Observe();
        state.SelectStep(second.Basic.StepKey);
        ProcessDefinitionStepEditorCommand? sent = null;
        state.ExecuteCommand = EventCallback.Factory.Create<ProcessDefinitionStepEditorCommand>(state, async command => {
            sent = command;
            var result = await owner.ExecuteCommandAsync(command);
            Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, result.Receipt.Status);
            state.Accept(result.Projection);
        });
        var cut = context.Render<ProcessDefinitionStepEditorPanel>(p => p.Add(c => c.State, state).Add(c => c.StepEditor, editor).Add(c => c.Scope, state.Scope).Add(c => c.ExecuteCommand, state.ExecuteCommand));
        cut.Find("[data-testid='processes-step-title']").Input("Second edited");
        await cut.InvokeAsync(() => state.ExecuteAsync(ProcessDefinitionStepCommandKind.SaveStep));
        Assert.Equal(second.Basic.StepKey, sent!.Draft.Basic.StepKey);
        Assert.Equal(second.Basic.DecisionRoleKey, sent.Draft.Basic.DecisionRoleKey);
        var reloaded = await owner.GetEditorAsync(state.Scope, Definition);
        Assert.Equal(first.Basic, reloaded.StepDrafts.Single(d => d.Basic.StepKey == first.Basic.StepKey).Basic);
        Assert.Equal(second.Basic.DecisionRoleKey, reloaded.SelectedStep!.Basic.DecisionRoleKey);
    }

    [Fact]
    public async Task Native_row_normalization_and_later_field_and_invalid_number_all_survive() {
        await using var environment = CanDoItAllTestEnvironment.Create("pc2-step-merge");
        var owner = new ProcessDefinitionStepEditorProjectionService(TemplatePack(environment.RootPath), new SystemProcessProjectionClock());
        var state = new ProcessStepEditorState { Scope = ProcessWorkspaceShellScope.Global, StepEditor = await owner.GetEditorAsync(ProcessWorkspaceShellScope.Global, Definition) };
        state.Observe();
        Assert.NotEmpty(state.BranchOutcomes);
        Assert.NotEmpty(state.ArtifactExpectations);
        state.BranchOutcomes[0] = state.BranchOutcomes[0] with { Description = "  normalize branch  " };
        state.ArtifactExpectations[0] = state.ArtifactExpectations[0] with { TemplateKey = "  normalize-artifact  " };
        state.ExecuteCommand = EventCallback.Factory.Create<ProcessDefinitionStepEditorCommand>(state, async command => {
            var result = await owner.ExecuteCommandAsync(command);
            Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, result.Receipt.Status);
            state.UpdateBranchTitle(state.BranchOutcomes[0].OutcomeKey, new() { Value = "Later branch" });
            state.UpdateBranchLoopBudget(state.BranchOutcomes[0].OutcomeKey, new() { Value = "invalid" });
            state.UpdateArtifactTitle(state.ArtifactExpectations[0].ArtifactKey, new() { Value = "Later artifact" });
            state.UpdateArtifactRetention(state.ArtifactExpectations[0].ArtifactKey, new() { Value = "-2" });
            state.Accept(result.Projection);
        });
        await state.ExecuteAsync(ProcessDefinitionStepCommandKind.SaveStep);
        Assert.Equal("normalize branch", state.BranchOutcomes[0].Description);
        Assert.Equal("Later branch", state.BranchOutcomes[0].Title);
        Assert.Equal("invalid", state.BranchNumber(state.BranchOutcomes[0]));
        Assert.Equal("normalize-artifact", state.ArtifactExpectations[0].TemplateKey);
        Assert.Equal("Later artifact", state.ArtifactExpectations[0].Title);
        Assert.Equal("-2", state.ArtifactNumber(state.ArtifactExpectations[0]));
        Assert.True(state.HasInputErrors);
    }

    private static ProcessTemplatePackLoader TemplatePack(string root) {
        File.WriteAllText(Path.Combine(root, "manifest.json"), """
            { "PackKey": "pc2-editors", "Name": "Native editor fixture", "Version": "1.0", "Processes": [
              { "Key": "architecture-review", "RelativePath": "definition" }
            ] }
            """);
        Directory.CreateDirectory(Path.Combine(root, "definition"));
        File.WriteAllText(Path.Combine(root, "definition", "definition.json"), """
            { "Key": "architecture-review", "DisplayName": "Architecture review", "Summary": "Editor fixture",
              "RoleUsages": [{ "Key": "solution-architect", "DisplayName": "Architect", "PreferredExecutorKind": "person-or-agent", "DefaultAllocationPercent": 60 }],
              "Steps": [{ "Key": "decision", "Title": "Decision", "StepKind": "Decision", "DecisionRoleKey": "solution-architect",
                "OperationTargetScope": "ManagedProcessArtifactsOnly", "AllowedOperations": ["ReadProcessContext"],
                "BranchOutcomes": [{ "Key": "approved", "Title": "Approved", "RouteTargetKind": "NextStep" }],
                "ArtifactExpectations": [{ "Key": "record", "TemplateKey": "record", "Title": "Record", "ArtifactKind": "Deliverable", "RetentionDays": 30 }]
              }]
            }
            """);
        Directory.CreateDirectory(Path.Combine(root, "toolbox"));
        File.WriteAllText(Path.Combine(root, "toolbox", "role-templates.json"), """
            [{ "ActionId": "role-template.solution-architect", "Label": "Architect", "TemplateRoleKey": "solution-architect",
               "KeyPrefix": "solution-architect", "DisplayNameTemplate": "Architect {ordinal}", "PreferredExecutorKind": "person-or-agent", "DefaultAllocationPercent": 60 }]
            """);
        return new(root);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
