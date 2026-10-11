using System.Text.Json;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringSemanticTests {
    [Fact]
    public async Task Every_family_preserves_materialized_hidden_semantics_in_a_rich_nonfirst_step() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<ProcessAuthoringWorkspace>();
        ProcessDefinitionCatalogItemKey key = new("simple-app-delivery");
        var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.Global, key, default);
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        var role = content.Definition.RoleUsages[1];
        role.Notes = "Hidden staffing constraint";
        role.CanvasX = 719;
        role.CanvasY = 311;
        role.WorkflowBinding = new(new(Guid.NewGuid()), new(Guid.NewGuid()));
        role.PreferredExecutorKind = ProcessLaunchExecutorKinds.Workflow;
        var step = content.Definition.Steps[1];
        step.DecisionRoleKey = role.Key;
        step.AllowsCompletedOutcomeWithOpenIssues = true;
        step.ExecutorPreferredSpecializationTags = ["retained-specialization"];
        step.ExecutionContract = new() {
            ExecutionClass = ProcessTemplateStepExecutionClasses.AgentWithToolPlanGuard,
            RuntimeOwnedExecutorKey = "retained-executor",
            RequiredHostCapabilities = ["retained-host"], RequiredRuntimeToolNames = ["retained-tool"],
            RequiredReceipts = [new() { Key = "receipt", ToolName = "retained-tool", Predicate = "verified" }],
            ProducedArtifactSlots = [new() { ArtifactExpectationKey = step.ArtifactExpectations[0].Key, MaterializationMode = "retained-mode" }],
            DeterministicToolPlan = new() { PlanKey = "retained-plan", PlanKind = "retained-kind", ScriptRef = "retained-script",
                RequiresReadbackChecks = true, ReadbackChecks = [new() { PathCandidates = ["artifact.txt"], RequiredTextAnyGroups = [["verified"]] }] }
        };
        step.CompletionPolicy = new() { RequiresProductSourceInspection = true, RequiresProductMutationBeforeManagedOutput = true,
            ProductMutationToolNames = ["retained-tool"], RuntimeRoutedBranchOutcomeKeys = ["retained-outcome"],
            RequiredProductToolReceipts = [new() { Key = "proof", ToolName = "retained-tool", Purpose = "Proof", Reason = "Must survive editing" }],
            CompletionIssueRoutes = [new() { IssueCode = "retained-issue", TargetBranchOutcomeKey = "retained-outcome", RequiresDefectEvidence = true }] };
        step.CapabilityScope.RequiredReceipts = [new() { Key = "retained-capability", ToolName = "retained-tool" }];
        step.SubprocessContract = new() { DefinitionKey = "retained-child", ParentProducedArtifactExpectationKey = step.ArtifactExpectations[0].Key,
            AcceptedChildOutputs = [new() { StepKey = "child-step", ArtifactExpectationKey = "child-output", Description = "Retained accepted mapping" }],
            NoGoChildOutputs = [new() { StepKey = "no-go", ArtifactExpectationKey = "reason" }],
            AlreadySatisfiedOutput = new() { StepKey = "already", ArtifactExpectationKey = "existing" },
            RequiredChildReceipts = [new() { ToolName = "retained-tool", RuntimeToolProviderKey = "retained-provider" }],
            ForwardedChildContextArtifacts = [new() { BindingKey = "context", SourceStepKey = "child-step", ArtifactExpectationKey = "child-output", PayloadSchema = "retained-schema" }] };
        step.ArtifactExpectations[0].PayloadSchema = "retained-payload-schema";
        step.ArtifactInputs = [new() { SourceStepKey = content.Definition.Steps[0].Key, ArtifactExpectationKey = content.Definition.Steps[0].ArtifactExpectations[0].Key }];
        step.Dependencies = [new() { DependsOnStepKey = content.Definition.Steps[0].Key }];
        var guidance = new ProcessTemplateExecutionGuidanceDocument("owned-guidance", "Immutable guidance body", ProcessAuthoringCodec.Hash("Immutable guidance body"));
        step.ExecutionGuidanceRefs = [guidance.Reference];
        var materialized = content.Guidance.ToDictionary(pair => pair.Key, pair => pair.Value);
        materialized[step.Key] = [guidance];
        content = content with { Guidance = materialized };
        content.Definition.LaunchDriverActivations = [new() { DriverKey = "retained-driver", Settings = new() { ["retained-setting"] = "retained-value" },
            InputArtifactBindings = [new() { BindingKey = "input", SourceStepKey = step.Key, ArtifactExpectationKey = step.ArtifactExpectations[0].Key, PayloadSchema = "retained-schema" }] }];
        await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("rich semantic fixture"), content, ProcessAuthoringLifecycle.Draft, false, null, default);
        var client = services.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var hidden = Hidden(content, step.Key, role.Key);
        var untouched = JsonSerializer.Serialize(content.Definition.Steps[0]);
        foreach (var family in Enum.GetValues<EditorFamily>()) {
            var editor = (await client.GetShellAsync(Request(key))).DefinitionCatalog.SelectedEditor!;
            switch (family) {
                case EditorFamily.Definition:
                    Assert.Equal(ProcessDefinitionEditorCommandStatus.Accepted, (await client.ExecuteDefinitionEditorCommandAsync(new(
                        ProcessWorkspaceShellScope.Global, key, ProcessDefinitionEditorCommandKind.SaveDraft, editor.VersionToken,
                        new(key, editor.Identity with { Name = "Rich saved definition" }, editor.Governance, editor.Contracts, editor.Simulation)))).Receipt.Status);
                    break;
                case EditorFamily.Role:
                    var draft = editor.RoleEditor!.Roles.Single(item => item.RoleKey.Value == role.Key).Draft;
                    Assert.Equal(ProcessDefinitionRoleCommandStatus.Accepted, (await client.ExecuteDefinitionRoleEditorCommandAsync(new(
                        ProcessWorkspaceShellScope.Global, key, ProcessDefinitionRoleCommandKind.SaveRole, editor.RoleEditor.VersionToken,
                        draft with { Purpose = "Edited visible purpose" }, null))).Receipt.Status);
                    break;
                case EditorFamily.Step:
                    var selected = editor.StepEditor!.StepDrafts.Single(item => item.Basic.StepKey.Value == step.Key);
                    Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, (await client.ExecuteDefinitionStepEditorCommandAsync(new(
                        ProcessWorkspaceShellScope.Global, key, ProcessDefinitionStepCommandKind.SaveStep, editor.StepEditor.VersionToken,
                        selected with { Basic = selected.Basic with { Title = "Edited nonfirst step" } }))).Receipt.Status);
                    break;
                case EditorFamily.Canvas:
                    var node = editor.Canvas!.Nodes.First(item => item.StepKey?.Value == step.Key && item.Kind == ProcessDefinitionCanvasNodeKind.Step);
                    Assert.Equal(ProcessDefinitionCanvasCommandStatus.Accepted, (await client.ExecuteDefinitionCanvasCommandAsync(new(
                        ProcessWorkspaceShellScope.Global, key, ProcessDefinitionCanvasCommandKind.MoveNodes, editor.Canvas.VersionToken,
                        null, node.NodeKey, null, ProcessDefinitionCanvasRecompositionMode.PreserveProjection, [new(node.NodeKey, 812, 419)]))).Receipt.Status);
                    break;
                case EditorFamily.Import:
                    var catalog = editor.TemplateCatalog!;
                    var source = catalog.Items.First(item => item.Kind == ProcessTemplateCatalogItemKind.Role);
                    Assert.Equal(ProcessTemplateImportCommandStatus.Accepted, (await client.ExecuteTemplateImportCommandAsync(new(
                        ProcessWorkspaceShellScope.Global, key, ProcessTemplateImportCommandKind.ImportRole, source.Key, catalog.VersionToken,
                        catalog.Query, editor.StepEditor!.SelectedStepKey))).Receipt.Status);
                    break;
            }
            await using var fresh = app.Services.CreateAsyncScope();
            var saved = (await fresh.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>().ReadAsync(ProcessWorkspaceShellScope.Global, key, default)).Content;
            Assert.Equal(hidden, Hidden(saved, step.Key, role.Key));
            Assert.Equal(untouched, JsonSerializer.Serialize(saved.Definition.Steps[0]));
            Assert.Equal(role.Key, saved.Definition.Steps[1].DecisionRoleKey);
            Assert.Equal(guidance, Assert.Single(saved.Definition.Steps[1].ResolvedExecutionGuidance));
        }
    }

    private static string Hidden(ProcessAuthoringContent content, string stepKey, string roleKey) {
        var step = content.Definition.Steps.Single(item => item.Key == stepKey);
        var role = content.Definition.RoleUsages.Single(item => item.Key == roleKey);
        return JsonSerializer.Serialize(new {
            content.Base, content.Guidance, content.Definition.LaunchDriverActivations,
            role.RoleResourceKey, role.Notes, role.CanvasX, role.CanvasY, role.WorkflowBinding,
            step.ExecutionContract, step.CompletionPolicy, step.CapabilityScope, step.AllowsCompletedOutcomeWithOpenIssues,
            step.ExecutionGuidanceRefs, step.ExecutorPreferredSpecializationTags, step.Dependencies, step.DependsOnStepKey,
            step.DependsOnBranchOutcomeKey, step.SubprocessContract, step.ArtifactInputs,
            Payloads = step.ArtifactExpectations.Select(item => new { item.Key, item.PayloadSchema }),
            OpenIssues = step.BranchOutcomes.Select(item => new { item.Key, item.AllowsCompletedOutcomeWithOpenIssues })
        });
    }

    private static ProcessWorkspaceShellRequest Request(ProcessDefinitionCatalogItemKey key)
        => new(ProcessWorkspaceShellScope.Global, new(null, null, null), new(null, key, ProcessDefinitionCatalogScopeKind.All, 50),
            new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 150), false);

    private enum EditorFamily { Definition, Role, Step, Canvas, Import }
}
