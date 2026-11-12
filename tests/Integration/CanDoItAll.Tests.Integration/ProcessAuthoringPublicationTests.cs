using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessAuthoringPublicationTests {
    [Fact]
    public Task Mapped_child_contract_and_captured_child_publication_survive_later_republish()
        => VerifyCapturedChildAsync(false);

    [Fact]
    public Task Native_project_child_contract_keeps_its_captured_scope_and_publication()
        => VerifyCapturedChildAsync(true);

    private static async Task VerifyCapturedChildAsync(bool nativeProject) {
        await using var app = await TestApplication.CreateAsync(new());
        var parentKey = await SeedAsync(app.Services);
        string childKey;
        await using (var seed = app.Services.CreateAsyncScope()) {
            var workspace = seed.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>();
            childKey = seed.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>().Load().Definitions.First(item => item.Key != parentKey).Key;
            var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.Global, new(childKey), default);
            var content = workspace.ReadTemplate(childKey);
            content.Definition.Steps = [new() { Key = "child-output", Title = "Child output", StepKind = "Work", OperationTargetScope = "ManagedProcessArtifactsOnly",
                ArtifactExpectations = [new() { Key = "result", Title = "Child result", ArtifactKind = "Artifact", IsRequired = true }] }];
            content.Definition.LaunchDriverActivations.Clear();
            content = content with { Guidance = new Dictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>>() };
            await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("child fixture"), content, ProcessAuthoringLifecycle.Draft, false, null, default);
        }
        await ChangeAsync(app.Services, childKey, ProcessDefinitionEditorCommandKind.Publish, "Child first");
        await using (var mapping = app.Services.CreateAsyncScope()) {
            var client = mapping.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
            var steps = await mapping.ServiceProvider.GetRequiredService<ProcessStepAuthoringAdapter>().ReadAsync(ProcessWorkspaceShellScope.Global, new(parentKey), default);
            var draft = steps.SelectedStep!;
            var result = await client.ExecuteDefinitionStepEditorCommandAsync(new(ProcessWorkspaceShellScope.Global, new(parentKey),
                ProcessDefinitionStepCommandKind.MapSubprocess, steps.VersionToken, draft with {
                    Basic = draft.Basic with { StepKind = ProcessDefinitionStepKind.Subprocess },
                    SubprocessMapping = draft.SubprocessMapping with { ProcessKey = childKey }
                }));
            Assert.True(result.Receipt.Status == ProcessDefinitionStepCommandStatus.Accepted, result.Receipt.Summary);
        }
        await ChangeAsync(app.Services, parentKey, ProcessDefinitionEditorCommandKind.Publish, "Parent publication");
        await using var scope = app.Services.CreateAsyncScope();
        var authority = await scope.ServiceProvider.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface);
        Guid? projectId = null;
        if (nativeProject) {
            projectId = Guid.NewGuid();
            Assert.True((await scope.ServiceProvider.GetRequiredService<ProjectsService>()
                .CreateAsync(projectId.Value, new() { Name = "Native captured child project" })).IsSuccess);
        }
        var launch = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();
        ProcessLaunchRequest request = new(parentKey, null, null, projectId, null, "child-pinning", new Dictionary<string, string>(), false, false);
        if (!nativeProject) {
            request = request with { Authority = authority, CallerIntentId = new(Guid.NewGuid()) };
        }
        var started = await launch.LaunchAsync(request);
        Assert.NotNull(started.RunId);
        var assignment = Assert.Single(await scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>().LoadByRunAsync(started.RunId.Value));
        var before = (await launch.ResolveChildForPreparationAsync(assignment, childKey))!;
        Assert.Equal(projectId ?? Guid.Empty, before.ProjectId);
        var contract = (await ResolveAsync(app.Services, parentKey)).Definition.Steps[0].SubprocessContract!;
        Assert.Equal(childKey, contract.DefinitionKey);
        Assert.Equal("result", Assert.Single(contract.AcceptedChildOutputs).ArtifactExpectationKey);
        Assert.Equal("subprocess-handoff", contract.ParentProducedArtifactExpectationKey);
        await ChangeAsync(app.Services, childKey, ProcessDefinitionEditorCommandKind.Publish, "Child second");
        var after = (await launch.ResolveChildForPreparationAsync(assignment, childKey))!;
        Assert.Equal(before.Definitions[childKey], after.Definitions[childKey]);
        Assert.Equal("Child first", ProcessExecutableDefinitionResolver.Decode(after, childKey).Definition.DisplayName);
        Assert.Equal("Child second", (await ResolveAsync(app.Services, childKey)).Definition.DisplayName);
        await Assert.ThrowsAsync<InvalidOperationException>(() => launch.ResolveChildForPreparationAsync(assignment, parentKey));
    }

    [Fact]
    public async Task Prepared_review_accepts_original_publication_after_draft_and_republish_and_survives_archive() {
        await using var app = await TestApplication.CreateAsync(new());
        var key = await SeedAsync(app.Services);
        await ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Publish, "Published first");
        ProcessLaunchRequest request;
        ProcessLaunchResult preview;
        await using (var scope = app.Services.CreateAsyncScope()) {
            var authority = await scope.ServiceProvider.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
                .CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface);
            request = new(key, null, null, null, null, "publication-test", new Dictionary<string, string>(), false, false) {
                Authority = authority, CallerIntentId = new(Guid.NewGuid())
            };
            preview = await scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>().PreviewAsync(request);
            Assert.NotNull(preview.Observation);
            Assert.Equal("Published first", preview.LaunchPlan.DefinitionName);
        }
        await ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.SaveDraft, "Unpublished second");
        Assert.Equal("Published first", (await ResolveAsync(app.Services, key)).Definition.DisplayName);
        await using (var edit = app.Services.CreateAsyncScope()) {
            var editor = await edit.ServiceProvider.GetRequiredService<ProcessStepAuthoringAdapter>().ReadAsync(ProcessWorkspaceShellScope.Global, new(key), default);
            var added = await edit.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>().ExecuteDefinitionStepEditorCommandAsync(
                new(ProcessWorkspaceShellScope.Global, new(key), ProcessDefinitionStepCommandKind.AddArtifactExpectation, editor.VersionToken, editor.SelectedStep!));
            Assert.Equal(ProcessDefinitionStepCommandStatus.Accepted, added.Receipt.Status);
        }
        await ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Publish, "Published second");
        Assert.Equal("Published second", (await ResolveAsync(app.Services, key)).Definition.DisplayName);
        await using var accept = app.Services.CreateAsyncScope();
        var launch = accept.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();
        var newReview = await launch.PreviewAsync(request with { CallerIntentId = new(Guid.NewGuid()) });
        var newPreparation = await accept.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>().GetAsync(newReview.Observation!.AdmissionId);
        Assert.Single(Assert.Single(newPreparation!.Preparation.InitialCommit.InitialAssignments!).ProducedArtifactSlotIds);
        Assert.NotEqual(preview.LaunchPlan.DefinitionVersionId, newReview.LaunchPlan.DefinitionVersionId);
        var accepted = await launch.LaunchAsync(request with { PreparedAdmissionId = preview.Observation!.AdmissionId });
        Assert.NotNull(accepted.RunId);
        Assert.Equal(preview.LaunchPlan.PlanHash, accepted.LaunchPlan.PlanHash);
        Assert.Empty(Assert.Single(await accept.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>().LoadByRunAsync(accepted.RunId.Value)).ProducedArtifactSlotIds);
        var plan = await accept.ServiceProvider.GetRequiredService<IProcessInstancePlanStore>().LoadAsync(accepted.LaunchPlanId);
        Assert.Equal("Published first", ProcessExecutableDefinitionResolver.Decode(plan!.ExecutableDefinitions!, key).Definition.DisplayName);
        await ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Archive, "Archived second");
        var recovered = await launch.LaunchAsync(request with { PreparedAdmissionId = preview.Observation.AdmissionId });
        Assert.Equal(accepted.RunId, recovered.RunId);
        Assert.Equal(preview.LaunchPlan.PlanHash, recovered.LaunchPlan.PlanHash);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ResolveAsync(app.Services, key));
    }

    [Fact]
    public async Task Archive_blocks_unaccepted_review_and_delete_restores_inherited_launch_content() {
        await using var app = await TestApplication.CreateAsync(new());
        var key = await SeedAsync(app.Services);
        await ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Publish, "Reviewed publication");
        await using var scope = app.Services.CreateAsyncScope();
        var authority = await scope.ServiceProvider.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface);
        ProcessLaunchRequest request = new(key, null, null, null, null, "archive-test", new Dictionary<string, string>(), false, false) {
            Authority = authority, CallerIntentId = new(Guid.NewGuid())
        };
        var launch = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();
        var preview = await launch.PreviewAsync(request);
        Assert.NotNull(preview.Observation);
        await ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Archive, "Archived publication");
        await Assert.ThrowsAsync<InvalidOperationException>(() => launch.LaunchAsync(request with { PreparedAdmissionId = preview.Observation.AdmissionId }));
        var saved = await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>().GetAsync(preview.Observation.AdmissionId);
        Assert.Null(saved!.AcceptedAtUtc);
        await ChangeAsync(app.Services, key, ProcessDefinitionEditorCommandKind.Delete, "Archived publication");
        var inherited = await ResolveAsync(app.Services, key);
        Assert.Equal(scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>().LoadDefinition(key).DisplayName, inherited.Definition.DisplayName);
        Assert.Equal("Reviewed publication", ProcessExecutableDefinitionResolver.Decode(saved.Preparation.InitialCommit.InitialPlan!.ExecutableDefinitions!, key).Definition.DisplayName);
    }

    [Fact]
    public async Task Explicit_unknown_identity_and_corrupted_captured_content_are_rejected() {
        await using var app = await TestApplication.CreateAsync(new());
        await using var scope = app.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResolveForPreparationAsync(null, new(Guid.NewGuid()), null));
        var key = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>().Load().Definitions[0].Key;
        var closure = (await service.ResolveForPreparationAsync(key, null, null))!;
        var sources = closure.Definitions.ToDictionary(pair => pair.Key, pair => pair.Value);
        sources[key] = sources[key] with { ContentHash = ProcessAuthoringCodec.Hash("corrupted") };
        Assert.Throws<InvalidOperationException>(() => ProcessExecutableDefinitionResolver.Decode(closure with { Definitions = sources }, key));
    }

    [Fact]
    public void Resolved_empty_driver_activations_do_not_reload_default_drivers() {
        var templates = new ProcessTemplatePackLoader();
        var definition = templates.Load().Definitions.Select(item => templates.LoadDefinition(item.Key)).First(item => item.LaunchDriverActivations.Count > 0);
        var observer = new DriverObserver();
        var service = new ProcessLaunchVariablePreparationService([observer], templates);
        ProcessLaunchPreparationContext context = new(definition.Key, false, new(Guid.Empty, "Fixture", new("node", "Node", "", "", "", "", [], ProcessLaunchSourceItemKind.Other, false), [], ""));
        service.Enrich(context, new Dictionary<string, string>());
        Assert.NotEmpty(observer.Observed!);
        definition.LaunchDriverActivations.Clear();
        service.Enrich(context.WithDefinition(definition), new Dictionary<string, string>());
        Assert.Empty(observer.Observed!);
    }

    internal static async Task<string> SeedAsync(IServiceProvider services) {
        await using var scope = services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>();
        var key = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>().Load().Definitions[0].Key;
        var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.Global, new(key), default);
        var content = workspace.ReadTemplate(key);
        content.Definition.Steps = [new() { Key = "work", Title = "Published work", StepKind = "Work", OperationTargetScope = "ManagedProcessArtifactsOnly" }];
        content.Definition.LaunchDriverActivations.Clear();
        content = content with { Guidance = new Dictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>>() };
        var saved = await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("publication fixture"), content, ProcessAuthoringLifecycle.Draft, false, null, default);
        Assert.Equal(ProcessAuthoringOutcome.Accepted, saved.Outcome);
        return key;
    }

    internal static async Task ChangeAsync(IServiceProvider services, string key, ProcessDefinitionEditorCommandKind kind, string name) {
        await using var scope = services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IProcessWorkspaceProjectionClient>();
        var editor = (await client.GetShellAsync(new(ProcessWorkspaceShellScope.Global, new(null, null, null),
            new(null, new(key), ProcessDefinitionCatalogScopeKind.All, 50),
            new(null, ProcessTemplateCatalogCategoryKind.All, null, ProcessTemplateCatalogPreviewTabKind.Overview, 50), false))).DefinitionCatalog.SelectedEditor!;
        var result = await client.ExecuteDefinitionEditorCommandAsync(new(ProcessWorkspaceShellScope.Global, editor.DefinitionKey, kind, editor.VersionToken,
            new(editor.DefinitionKey, editor.Identity with { Name = name }, editor.Governance, editor.Contracts, editor.Simulation)));
        Assert.True(result.Receipt.Status == ProcessDefinitionEditorCommandStatus.Accepted, result.Receipt.Summary);
    }

    private static async Task<ProcessAuthoringContent> ResolveAsync(IServiceProvider services, string key) {
        await using var scope = services.CreateAsyncScope();
        var closure = await scope.ServiceProvider.GetRequiredService<ProcessExecutableDefinitionResolver>().ResolveAsync(ProcessWorkspaceShellScope.Global, key);
        return ProcessExecutableDefinitionResolver.Decode(closure, key);
    }

    private sealed class DriverObserver : IProcessLaunchVariableContributor {
        public IReadOnlyList<ProcessLaunchDriverActivation>? Observed { get; private set; }
        public void Enrich(ProcessLaunchPreparationContext context, IDictionary<string, string> variables) => Observed = context.DriverActivations;
    }
}
