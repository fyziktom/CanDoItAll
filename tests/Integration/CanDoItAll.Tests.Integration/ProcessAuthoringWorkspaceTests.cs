using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringWorkspaceTests {
    [Fact]
    public async Task Roles_steps_and_catalog_share_one_revision_and_reject_cross_family_stale_writes() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var scope = app.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var before = (await client.GetShellAsync(Request())).DefinitionCatalog.SelectedEditor!;
        var role = before.RoleEditor!.SelectedRole!.Draft with { Purpose = "Persistent authoring purpose" };
        var saved = await client.ExecuteDefinitionRoleEditorCommandAsync(new(ProcessWorkspaceShellScope.Global, before.DefinitionKey,
            ProcessDefinitionRoleCommandKind.SaveRole, before.RoleEditor.VersionToken, role, null));
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, saved.Receipt.Status);
        var step = before.StepEditor!.SelectedStep!;
        var stale = await client.ExecuteDefinitionStepEditorCommandAsync(new(ProcessWorkspaceShellScope.Global, before.DefinitionKey,
            ProcessDefinitionStepCommandKind.SaveStep, before.StepEditor.VersionToken, step with { Basic = step.Basic with { Title = "Stale title" } }));
        Assert.Equal(ProcessDefinitionStepCommandStatus.Rejected, stale.Receipt.Status);
        var observed = (await client.GetShellAsync(Request(before.DefinitionKey))).DefinitionCatalog.SelectedEditor!;
        AssertCoherent(observed);
        Assert.Equal(saved.Projection.Observation, observed.Observation);
        Assert.Equal(role.Purpose, observed.RoleEditor!.Roles.Single(item => item.RoleKey == role.RoleKey).Draft.Purpose);
        var selectedStep = observed.StepEditor!.StepDrafts.Single(item => item.Basic.StepKey == step.Basic.StepKey);
        var changed = selectedStep with { Basic = selectedStep.Basic with { Title = "Persistent step title" } };
        var stepResult = await client.ExecuteDefinitionStepEditorCommandAsync(new(ProcessWorkspaceShellScope.Global, before.DefinitionKey,
            ProcessDefinitionStepCommandKind.SaveStep, observed.StepEditor.VersionToken, changed));
        Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, stepResult.Receipt.Status);
        await using var independent = app.Services.CreateAsyncScope();
        var current = (await independent.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>().GetShellAsync(Request(before.DefinitionKey))).DefinitionCatalog.SelectedEditor!;
        AssertCoherent(current);
        Assert.Equal(2, current.Observation!.Revision);
        Assert.Equal(changed.Basic.Title, current.StepEditor!.StepDrafts.Single(item => item.Basic.StepKey == step.Basic.StepKey).Basic.Title);
        Assert.Contains(current.Canvas!.Nodes, node => node.Kind == ProcessDefinitionCanvasNodeKind.Step && node.StepKey == step.Basic.StepKey && node.Title == changed.Basic.Title);
        Assert.Equal(role.Purpose, current.RoleEditor!.Roles.Single(item => item.RoleKey == role.RoleKey).Draft.Purpose);
    }

    [Fact]
    public async Task Referenced_role_delete_is_refused_and_unreferenced_last_role_delete_clears_selection() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var scope = app.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var before = (await client.GetShellAsync(Request())).DefinitionCatalog.SelectedEditor!;
        var referenced = before.RoleEditor!.Roles.First(role => role.StepBindingCount > 0);
        var refused = await client.ExecuteDefinitionRoleEditorCommandAsync(new(ProcessWorkspaceShellScope.Global, before.DefinitionKey,
            ProcessDefinitionRoleCommandKind.DeleteRole, before.RoleEditor.VersionToken, referenced.Draft, null));
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Rejected, refused.Receipt.Status);
        Assert.Contains("before deleting", refused.Receipt.Summary);
        var seeded = await SeedCanvasAsync(scope.ServiceProvider);
        var content = seeded.Content;
        foreach (var step in content.Definition.Steps) {
            step.RoleAssignments.Clear();
            step.DecisionRoleKey = string.Empty;
        }
        var store = scope.ServiceProvider.GetRequiredService<IProcessAuthoringStore>();
        await store.CommitAsync(new(seeded.Address, ProcessAuthoringAdmissionPolicy.LocalCallerId, Guid.NewGuid(), ProcessAuthoringCodec.Hash("explicit unbind"),
            seeded.Revision, content, ProcessAuthoringLifecycle.Draft, false));
        var current = (await client.GetShellAsync(Request(new(seeded.Address.DefinitionKey)))).DefinitionCatalog.SelectedEditor!;
        var deleted = await client.ExecuteDefinitionRoleEditorCommandAsync(new(ProcessWorkspaceShellScope.Global, current.DefinitionKey,
            ProcessDefinitionRoleCommandKind.DeleteRole, current.RoleEditor!.VersionToken, Assert.Single(current.RoleEditor.Roles).Draft, null));
        Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, deleted.Receipt.Status);
        Assert.Empty(deleted.Projection.Roles);
        Assert.Null(deleted.Projection.SelectedRoleKey);
        Assert.Empty((await client.GetShellAsync(Request(current.DefinitionKey))).DefinitionCatalog.SelectedEditor!.RoleEditor!.Roles);
    }

    [Theory]
    [InlineData(ProcessDefinitionCanvasCommandKind.MoveNodes)]
    [InlineData(ProcessDefinitionCanvasCommandKind.AddStep)]
    [InlineData(ProcessDefinitionCanvasCommandKind.AddBranchRouter)]
    [InlineData(ProcessDefinitionCanvasCommandKind.AddRoleBinding)]
    [InlineData(ProcessDefinitionCanvasCommandKind.AddArtifactExpectation)]
    [InlineData(ProcessDefinitionCanvasCommandKind.AddSubprocessBoundary)]
    [InlineData(ProcessDefinitionCanvasCommandKind.CloneArtifactReference)]
    [InlineData(ProcessDefinitionCanvasCommandKind.CloneRoleReference)]
    [InlineData(ProcessDefinitionCanvasCommandKind.Recompose)]
    public async Task Every_canvas_command_persists_semantics_or_layout_through_independent_native_scope(ProcessDefinitionCanvasCommandKind kind) {
        await using var app = await TestApplication.CreateAsync(new());
        await using var scope = app.Services.CreateAsyncScope();
        var seeded = await SeedCanvasAsync(scope.ServiceProvider);
        ProcessDefinitionCatalogItemKey key = new(seeded.Address.DefinitionKey);
        var client = scope.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var canvas = (await client.GetShellAsync(Request(key))).DefinitionCatalog.SelectedEditor!.Canvas!;
        var selected = kind switch {
            ProcessDefinitionCanvasCommandKind.CloneArtifactReference => canvas.Nodes.First(node => node.Kind == ProcessDefinitionCanvasNodeKind.Artifact),
            ProcessDefinitionCanvasCommandKind.CloneRoleReference => canvas.Nodes.First(node => node.Kind == ProcessDefinitionCanvasNodeKind.Role),
            _ => canvas.Nodes.First(node => node.Kind == ProcessDefinitionCanvasNodeKind.Step)
        };
        var action = canvas.ToolboxActions.First(item => item.StepKind == ProcessDefinitionStepKind.Work);
        ProcessDefinitionCanvasCommand command = new(ProcessWorkspaceShellScope.Global, key, kind, canvas.VersionToken, action.ActionKey,
            selected.NodeKey, null, ProcessDefinitionCanvasRecompositionMode.PreserveProjection, [new(selected.NodeKey, 321, 654)]);
        var result = await client.ExecuteDefinitionCanvasCommandAsync(command);
        Assert.Equal(ProcessDefinitionCanvasCommandStatus.Accepted, result.Receipt.Status);
        await using var independent = app.Services.CreateAsyncScope();
        var current = (await independent.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>().GetShellAsync(Request(key))).DefinitionCatalog.SelectedEditor!;
        AssertCoherent(current);
        var stored = (await independent.ServiceProvider.GetRequiredService<IProcessAuthoringStore>().ReadAsync(seeded.Address))!;
        Assert.Equal(2, stored.Revision);
        var replay = await client.ExecuteDefinitionCanvasCommandAsync(command);
        Assert.Equal(result.Receipt, replay.Receipt);
        Assert.Equal(result.Projection.Selection with { Facts = replay.Projection.Selection.Facts }, replay.Projection.Selection);
        Assert.Equal(result.Projection.Selection.Facts, replay.Projection.Selection.Facts);
        Assert.Equal(2, (await independent.ServiceProvider.GetRequiredService<IProcessAuthoringStore>().ReadAsync(seeded.Address))!.Revision);
        switch (kind) {
            case ProcessDefinitionCanvasCommandKind.MoveNodes:
                var moved = current.Canvas!.Nodes.Single(node => node.NodeKey == selected.NodeKey);
                Assert.Equal((321d, 654d), (moved.X, moved.Y));
                break;
            case ProcessDefinitionCanvasCommandKind.AddStep:
                Assert.Equal(2, stored.Content.Definition.Steps.Count);
                Assert.Equal(2, current.StepEditor!.StepDrafts.Count);
                Assert.Equal("authoring-step", stored.Content.Definition.Steps.Last().DependsOnStepKey);
                break;
            case ProcessDefinitionCanvasCommandKind.AddBranchRouter:
                Assert.Single(stored.Content.Definition.Steps[0].BranchOutcomes);
                Assert.Contains(current.Canvas!.Nodes, node => node.Kind == ProcessDefinitionCanvasNodeKind.BranchRouter);
                break;
            case ProcessDefinitionCanvasCommandKind.AddRoleBinding:
                Assert.Single(stored.Content.Definition.Steps[0].RoleAssignments);
                Assert.Single(current.RoleEditor!.StepRoleBindings);
                break;
            case ProcessDefinitionCanvasCommandKind.AddArtifactExpectation:
                Assert.Equal(2, stored.Content.Definition.Steps[0].ArtifactExpectations.Count);
                Assert.Equal(2, current.StepEditor!.SelectedStep!.ArtifactExpectations.Count);
                break;
            case ProcessDefinitionCanvasCommandKind.AddSubprocessBoundary:
                Assert.Equal("Subprocess", stored.Content.Definition.Steps[0].StepKind);
                Assert.Equal(ProcessDefinitionStepKind.Subprocess, current.StepEditor!.SelectedStep!.Basic.StepKind);
                break;
            case ProcessDefinitionCanvasCommandKind.CloneArtifactReference:
            case ProcessDefinitionCanvasCommandKind.CloneRoleReference:
                Assert.Single(stored.Content.Definition.RoleUsages);
                Assert.Single(stored.Content.Definition.Steps[0].ArtifactExpectations);
                Assert.Equal(canvas.Nodes.Count + 1, current.Canvas!.Nodes.Count);
                Assert.Single(stored.Content.References, item => item.IsClone);
                break;
            case ProcessDefinitionCanvasCommandKind.Recompose:
                Assert.Equal(ProcessAuthoringCodec.Write(seeded.Content), ProcessAuthoringCodec.Write(stored.Content with { References = [] }));
                break;
        }
    }

    [Fact]
    public async Task Imports_materialize_content_merge_without_replacement_and_replay_without_allocating() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var scope = app.Services.CreateAsyncScope();
        var seeded = await SeedCanvasAsync(scope.ServiceProvider);
        var client = scope.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        ProcessDefinitionCatalogItemKey key = new(seeded.Address.DefinitionKey);
        foreach (var kind in new[] { ProcessTemplateImportCommandKind.ImportRole, ProcessTemplateImportCommandKind.ImportArtifact, ProcessTemplateImportCommandKind.ImportProcess }) {
            var request = Request(key);
            request = request with { TemplateCatalogQuery = request.TemplateCatalogQuery with { Category = kind switch {
                ProcessTemplateImportCommandKind.ImportRole => ProcessTemplateCatalogCategoryKind.Roles,
                ProcessTemplateImportCommandKind.ImportArtifact => ProcessTemplateCatalogCategoryKind.Artifacts,
                _ => ProcessTemplateCatalogCategoryKind.Processes
            } } };
            var editor = (await client.GetShellAsync(request)).DefinitionCatalog.SelectedEditor!;
            var catalog = editor.TemplateCatalog!;
            var itemKind = kind switch {
                ProcessTemplateImportCommandKind.ImportRole => ProcessTemplateCatalogItemKind.Role,
                ProcessTemplateImportCommandKind.ImportArtifact => ProcessTemplateCatalogItemKind.Artifact,
                _ => ProcessTemplateCatalogItemKind.Process
            };
            var item = catalog.Items.First(item => item.Kind == itemKind);
            ProcessTemplateImportCommand command = new(ProcessWorkspaceShellScope.Global, key, kind, item.Key, catalog.VersionToken, catalog.Query,
                editor.StepEditor!.SelectedStepKey);
            var result = await client.ExecuteTemplateImportCommandAsync(command);
            Assert.Equal(ProcessTemplateImportCommandStatus.Accepted, result.Receipt.Status);
            var replay = await client.ExecuteTemplateImportCommandAsync(command);
            Assert.Equal(result.Receipt, replay.Receipt);
        }
        await using var independent = app.Services.CreateAsyncScope();
        var stored = (await independent.ServiceProvider.GetRequiredService<IProcessAuthoringStore>().ReadAsync(seeded.Address))!;
        Assert.Equal(4, stored.Revision);
        Assert.Equal(3, stored.Content.Imports.Count);
        Assert.True(stored.Content.Definition.RoleUsages.Count > 1);
        Assert.True(stored.Content.Definition.Steps.Count > 1);
        Assert.Equal(2, stored.Content.Definition.Steps.Single(step => step.Key == "authoring-step").ArtifactExpectations.Count);
        Assert.Equal(seeded.Content.Definition.DisplayName, stored.Content.Definition.DisplayName);
        Assert.Contains(stored.Content.Definition.Steps, step => step.Key == "authoring-step");
        var current = (await client.GetShellAsync(Request(key))).DefinitionCatalog.SelectedEditor!;
        AssertCoherent(current);
        Assert.Equal(3, current.TemplateCatalog!.ImportedComponents.Count);
    }

    private static async Task<ProcessAuthoringSnapshot> SeedCanvasAsync(IServiceProvider services) {
        var workspace = services.GetRequiredService<ProcessAuthoringWorkspace>();
        var template = services.GetRequiredService<ProcessTemplatePackLoader>().Load().Definitions[0];
        var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.Global, new(template.Key), default);
        var content = workspace.ReadTemplate(template.Key);
        content.Definition.RoleUsages = [content.Definition.RoleUsages[0]];
        content.Definition.Steps = [new() { Key = "authoring-step", Title = "Authoring step", StepKind = "Work",
            OperationTargetScope = "ManagedProcessArtifactsOnly", CanvasX = 200, CanvasY = 300,
            ArtifactExpectations = [new() { Key = "evidence", Title = "Evidence", ArtifactKind = "Artifact", IsRequired = true }] }];
        content.Definition.LaunchDriverActivations.Clear();
        content = content with { Guidance = new Dictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>>() };
        var receipt = await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("native canvas fixture"), content,
            ProcessAuthoringLifecycle.Draft, false, null, default);
        return receipt.Snapshot!;
    }

    private static ProcessWorkspaceShellRequest Request(ProcessDefinitionCatalogItemKey? key = null)
        => new(ProcessWorkspaceShellScope.Global, new(null, null, null), new(null, key, ProcessDefinitionCatalogScopeKind.All, 50),
            new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 150), false);

    private static void AssertCoherent(ProcessDefinitionEditorProjection editor) {
        Assert.NotNull(editor.Observation);
        Assert.Equal(editor.Observation, editor.RoleEditor!.Observation);
        Assert.Equal(editor.Observation, editor.StepEditor!.Observation);
        Assert.Equal(editor.Observation, editor.Canvas!.Observation);
        Assert.Equal(editor.Observation, editor.TemplateCatalog!.Observation);
    }
}
