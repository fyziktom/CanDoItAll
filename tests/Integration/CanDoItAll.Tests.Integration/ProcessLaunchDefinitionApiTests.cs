using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessLaunchDefinitionApiTests {
    private const string DefinitionKey = "dotnet-solution-setup";
    private const string DriverProbe = "Pc4ObservedDrivers";
    private const string ProbeDriver = "pc4.publication-probe";
    private const string RevisionSetting = "Revision";
    private const string RevisionVariable = "Pc4CapturedRevision";
    private const string BindingVariable = "Pc4CapturedBinding";

    [Theory]
    [InlineData(BoundaryChange.Publication)]
    [InlineData(BoundaryChange.ProjectDeletion)]
    [InlineData(BoundaryChange.ProjectRecreation)]
    public async Task HTTP_held_enrichment_keeps_captured_settings_and_rechecks_project_lifetime(BoundaryChange change) {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new DriverObserver { AfterObservation = () => {
            entered.TrySetResult();
            release.Task.WaitAsync(TimeSpan.FromSeconds(40)).GetAwaiter().GetResult();
        } };
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, services => services.AddSingleton<IProcessLaunchVariableContributor>(observer));
        var (project, node) = await CreateTargetAsync(host.App.Services);
        await PublishAsync(host.App.Services, null, "Global selection", content => SetProbe(content, "global"));
        await PublishAsync(host.App.Services, project, "Captured A", content => SetProbe(content, "A"));
        var request = new ProcessLaunchApiRequest(DefinitionKey: DefinitionKey, ProjectId: project, ProjectNodeId: node,
            RunReadiness: false, Execute: false, CallerIntentId: Guid.NewGuid());
        var operation = host.Client.PostAsJsonAsync("/api/processes/launch", request);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(40));
            if (change == BoundaryChange.Publication) {
                await PublishAsync(host.App.Services, project, "Later B", content => SetProbe(content, "B"));
            } else {
                await using var retirement = host.App.Services.CreateAsyncScope();
                var projects = retirement.ServiceProvider.GetRequiredService<ProjectsService>();
                await projects.DeleteAsync(project);
                if (change == BoundaryChange.ProjectRecreation) {
                    Assert.True((await projects.CreateAsync(project, new() { Name = "Successor project lifetime" })).IsSuccess);
                }
            }
        } finally {
            release.TrySetResult();
        }
        using var response = await operation;
        await using var independent = host.App.Services.CreateAsyncScope();
        await using var database = await independent.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        if (change != BoundaryChange.Publication) {
            Assert.False(response.IsSuccessStatusCode);
            Assert.Empty(await database.RuntimeStates.ToArrayAsync());
            Assert.Empty(await database.RuntimeStepAssignments.ToArrayAsync());
            Assert.DoesNotContain(await database.PreparedLaunches.ToArrayAsync(), item => item.AcceptedAtUtc != null);
            return;
        }
        using var body = await ReadAsync(response);
        var saved = (await independent.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
            .FindByIntentAsync(new(request.CallerIntentId!.Value)))!;
        Assert.NotNull(saved.AcceptedAtUtc);
        var plan = saved.Preparation.InitialCommit.InitialPlan!;
        var captured = ProcessExecutableDefinitionResolver.Decode(plan.ExecutableDefinitions!, DefinitionKey).Definition;
        Assert.Equal("Captured A", captured.DisplayName);
        Assert.Equal("A", Assert.Single(captured.LaunchDriverActivations).Settings[RevisionSetting]);
        Assert.Equal("evidence-A", Assert.Single(captured.LaunchDriverActivations[0].InputArtifactBindings).ArtifactExpectationKey);
        Assert.Equal("A", saved.Preparation.Request.Variables[RevisionVariable]);
        Assert.Equal("work/evidence-A", saved.Preparation.Request.Variables[BindingVariable]);
        var assignments = await independent.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>()
            .LoadByRunAsync(saved.Preparation.InitialCommit.Mutation.State.RunId);
        Assert.NotEmpty(assignments);
        Assert.All(assignments, assignment => Assert.Equal("A", assignment.LaunchVariables[RevisionVariable]));
        using var replayResponse = await host.Client.PostAsJsonAsync("/api/processes/launch", request);
        using var replay = await ReadAsync(replayResponse);
        Assert.Equal(body.RootElement.GetProperty("runId").GetGuid(), replay.RootElement.GetProperty("runId").GetGuid());
        Assert.Single(observer.Contexts);
        Assert.Single(await database.RuntimeStates.ToArrayAsync());
    }

    [Fact]
    public async Task HTTP_contributor_failure_creates_no_preparation_or_run_and_allows_the_original_intent_to_retry() {
        var observer = new DriverObserver { AfterObservation = () => throw new InvalidOperationException("PC4 controlled contributor failure") };
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, services => services.AddSingleton<IProcessLaunchVariableContributor>(observer));
        var (project, node) = await CreateTargetAsync(host.App.Services);
        await PublishAsync(host.App.Services, project, "Failure boundary", content => content.Definition.LaunchDriverActivations.Clear());
        var request = new ProcessLaunchApiRequest(DefinitionKey: DefinitionKey, ProjectId: project, ProjectNodeId: node,
            RunReadiness: false, Execute: false, CallerIntentId: Guid.NewGuid());
        using var failed = await host.Client.PostAsJsonAsync("/api/processes/launch", request);
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        await using (var scope = host.App.Services.CreateAsyncScope()) {
            await using var database = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
            Assert.Empty(await database.RuntimeStates.ToArrayAsync());
            Assert.Empty(await database.PreparedLaunches.ToArrayAsync());
            Assert.Empty(await database.RuntimeStepAssignments.ToArrayAsync());
        }
        observer.AfterObservation = null;
        using var retry = await host.Client.PostAsJsonAsync("/api/processes/launch", request);
        using var body = await ReadAsync(retry);
        Assert.NotEqual(Guid.Empty, body.RootElement.GetProperty("runId").GetGuid());
        Assert.Equal(2, observer.Contexts.Count);
    }

    [Fact]
    public async Task HTTP_capture_preserves_authenticated_subject_and_requires_execution_capability() {
        var observer = new DriverObserver();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, services => services.AddSingleton<IProcessLaunchVariableContributor>(observer));
        var (project, node) = await CreateTargetAsync(host.App.Services);
        await PublishAsync(host.App.Services, project, "Authenticated selection", content => content.Definition.LaunchDriverActivations.Clear());
        var request = new ProcessLaunchApiRequest(DefinitionKey: DefinitionKey, ProjectId: project, ProjectNodeId: node,
            RunReadiness: false, Execute: false, CallerIntentId: Guid.NewGuid());
        var tokens = host.App.Services.GetRequiredService<IApiTokenService>();
        var deniedToken = tokens.IssueToken(new() { Subject = "pc4-reader", Scopes = [ApiAccessScopeNames.ReadProcesses] });
        host.Client.DefaultRequestHeaders.Authorization = new(deniedToken.TokenType, deniedToken.Token);
        using var denied = await host.Client.PostAsJsonAsync("/api/processes/launch/check", request);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Empty(observer.Contexts);
        var allowedToken = tokens.IssueToken(new() { Subject = "pc4-operator", Scopes = [ApiAccessScopeNames.ExecuteProcesses] });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(allowedToken.TokenType, allowedToken.Token);
        using var allowed = await host.Client.PostAsJsonAsync("/api/processes/launch/check", request);
        using var body = await ReadAsync(allowed);
        await using var scope = host.App.Services.CreateAsyncScope();
        var saved = (await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>().FindByIntentAsync(new(request.CallerIntentId!.Value)))!;
        var principal = Assert.IsType<ProcessLaunchPrincipal.AuthenticatedOperator>(saved.Preparation.Authority!.Principal);
        Assert.Equal("pc4-operator", principal.SubjectId);
        Assert.Equal(project, saved.Preparation.Authority.ProjectAdmission!.ProjectId);
        Assert.Single(observer.Contexts);
    }

    private static void SetProbe(ProcessAuthoringContent content, string revision) {
        content.Definition.LaunchDriverActivations = [new() { DriverKey = ProbeDriver, Settings = new() { [RevisionSetting] = revision },
            InputArtifactBindings = [new() { BindingKey = "evidence", SourceStepKey = "work", ArtifactExpectationKey = "evidence-" + revision, PayloadSchema = "pc4-observation.v1" }] }];
        content.Definition.Steps[0].ArtifactExpectations = [new() { Key = "evidence-" + revision, Title = "Captured evidence", ArtifactKind = "Artifact" }];
    }

    [Theory]
    [InlineData(LaunchScope.Global)]
    [InlineData(LaunchScope.Project)]
    [InlineData(LaunchScope.ProjectNode)]
    public async Task HTTP_selection_canonicalizes_supported_inputs_and_refuses_explicit_contradictions(LaunchScope target) {
        var observer = new DriverObserver();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, services =>
            services.AddSingleton<IProcessLaunchVariableContributor>(observer));
        var (project, node) = await CreateTargetAsync(host.App.Services);
        var projectId = target == LaunchScope.Global ? (Guid?)null : project;
        await PublishAsync(host.App.Services, projectId, "Canonical authored selection", content => content.Definition.LaunchDriverActivations.Clear());
        var templates = host.App.Services.GetRequiredService<ProcessTemplatePackLoader>();
        var profile = templates.LoadLiveRunProfiles().First(item => item.ProcessTemplateKey != DefinitionKey);
        var definitionId = ProcessDefinitionCatalogProjectionService.CreateDefinitionId(new(DefinitionKey)).Value;
        var original = new ProcessLaunchApiRequest(ProjectId: projectId, ProjectNodeId: target == LaunchScope.ProjectNode ? node : null,
            RunReadiness: false, Execute: false);
        (ProcessLaunchApiRequest Request, string? Expected)[] cases = [
            (original with { DefinitionKey = "  DOTNET-SOLUTION-SETUP  " }, DefinitionKey),
            (original with { ProcessDefinitionId = definitionId }, DefinitionKey),
            (original with { DefinitionKey = DefinitionKey, ProcessDefinitionId = definitionId }, DefinitionKey),
            (original with { DefinitionKey = DefinitionKey, ProcessDefinitionId = Guid.NewGuid() }, null),
            (original with { ProcessDefinitionId = Guid.Empty }, null),
            (original with { ProcessDefinitionId = Guid.NewGuid() }, null),
            (original with { DefinitionKey = "pc4-definition-does-not-exist" }, null),
            (original with { DefinitionKey = DefinitionKey, LiveRunProfileKey = profile.Key }, null),
            (original with { LiveRunProfileKey = "  " + profile.Key.ToUpperInvariant() + "  " }, profile.ProcessTemplateKey),
            (original, templates.LoadLiveRunProfiles().First().ProcessTemplateKey)
        ];
        foreach (var (input, expected) in cases) {
            var request = input with { CallerIntentId = Guid.NewGuid() };
            var before = observer.Contexts.Count;
            using var response = await host.Client.PostAsJsonAsync("/api/processes/launch/check", request);
            if (expected is null) {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal(before, observer.Contexts.Count);
                continue;
            }
            using var body = await ReadAsync(response);
            Assert.Equal(expected, body.RootElement.GetProperty("launchPlan").GetProperty("definitionKey").GetString());
            if (expected == DefinitionKey) {
                Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("launchPlan").GetProperty("liveRunProfileKey").ValueKind);
            }
            await using var scope = host.App.Services.CreateAsyncScope();
            var saved = (await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
                .FindByIntentAsync(new(request.CallerIntentId!.Value)))!;
            Assert.Equal(expected, saved.Preparation.InitialCommit.InitialPlan!.ExecutableDefinitions!.DefinitionKey);
            Assert.Equal(expected, saved.Preparation.Request.DefinitionKey);
            if (target == LaunchScope.ProjectNode) {
                Assert.Equal(expected, observer.Contexts.Last().DefinitionKey);
                Assert.True(observer.Contexts.Last().DriverActivationsResolved);
            }
            using var replayResponse = await host.Client.PostAsJsonAsync("/api/processes/launch/check", request);
            using var replay = await ReadAsync(replayResponse);
            Assert.Equal(saved.Preparation.Review.PlanId.Value, replay.RootElement.GetProperty("launchPlanId").GetGuid());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HTTP_project_preparation_preserves_published_empty_drivers_and_replays_without_enrichment(bool launch) {
        var observer = new DriverObserver();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, services =>
            services.AddSingleton<IProcessLaunchVariableContributor>(observer));
        var (project, node) = await CreateTargetAsync(host.App.Services);
        await PublishAsync(host.App.Services, project, "Empty drivers", content => content.Definition.LaunchDriverActivations.Clear());
        var request = new ProcessLaunchApiRequest(DefinitionKey: DefinitionKey, ProjectId: project, ProjectNodeId: node,
            RunReadiness: false, Execute: false, CallerIntentId: Guid.NewGuid());
        var route = launch ? "/api/processes/launch" : "/api/processes/launch/check";
        using var response = await host.Client.PostAsJsonAsync(route, request);
        using var body = await ReadAsync(response);
        var admission = new ProcessLaunchAdmissionId(body.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid());
        var observed = Assert.Single(observer.Contexts);
        Assert.True(observed.DriverActivationsResolved);
        Assert.Empty(observed.DriverActivations);
        Assert.Equal(project, observed.Source.ProjectId);
        Assert.Equal(node, observed.Source.SelectedItem.Id);
        await using var scope = host.App.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>();
        var saved = (await store.GetAsync(admission))!;
        Assert.Equal("0", saved.Preparation.Request.Variables[DriverProbe]);
        var plan = saved.Preparation.InitialCommit.InitialPlan!;
        Assert.Equal("Empty drivers", ProcessExecutableDefinitionResolver.Decode(plan.ExecutableDefinitions!, DefinitionKey).Definition.DisplayName);
        Assert.Empty(ProcessExecutableDefinitionResolver.Decode(plan.ExecutableDefinitions!, DefinitionKey).Definition.LaunchDriverActivations);
        await PublishAsync(host.App.Services, project, "Later publication", content => content.Definition.LaunchDriverActivations.Clear());
        using var replayResponse = await host.Client.PostAsJsonAsync(route, request);
        using var replay = await ReadAsync(replayResponse);
        Assert.Equal(admission.Value, replay.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid());
        Assert.Equal(plan.Header.PlanId.Value, replay.RootElement.GetProperty("launchPlanId").GetGuid());
        Assert.Single(observer.Contexts);
        Assert.Equal(saved.PreparationFingerprint, (await store.GetAsync(admission))!.PreparationFingerprint);
        using var conflict = await host.Client.PostAsJsonAsync(route, request with { RequestedBy = "changed original input" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Single(observer.Contexts);
    }

    private static async Task<(Guid Project, string Node)> CreateTargetAsync(IServiceProvider services) {
        await using var scope = services.CreateAsyncScope();
        var project = Guid.NewGuid();
        Assert.True((await scope.ServiceProvider.GetRequiredService<ProjectsService>()
            .CreateAsync(project, new() { Name = "PC4 executable context" })).IsSuccess);
        var node = await scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(project,
            new ProjectObjectCreateRequest(ProjectObjectType.ProjectBlock, "PC4 target", "Selected context", "Original notes",
                $"project:{project}", 420, 260, ObjectSubtype: "delivery"));
        return (project, node.Id);
    }

    private static async Task PublishAsync(IServiceProvider services, Guid? project, string name, Action<ProcessAuthoringContent> change) {
        await using var scope = services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>();
        var target = project is { } id ? ProcessWorkspaceShellScope.ForProject(id) : ProcessWorkspaceShellScope.Global;
        var baseline = await workspace.ReadAsync(target, new(DefinitionKey), default);
        Assert.NotEmpty(workspace.ReadTemplate(DefinitionKey).Definition.LaunchDriverActivations);
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        content.Definition.DisplayName = name;
        content.Definition.Steps = [new() { Key = "work", Title = "Captured work", StepKind = "Work", OperationTargetScope = "ManagedProcessArtifactsOnly" }];
        content = content with { Guidance = new Dictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>>() };
        change(content);
        content = await scope.ServiceProvider.GetRequiredService<ProcessExecutableDefinitionResolver>().PinPublicationAsync(baseline, content, default);
        var saved = await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash(name), content,
            ProcessAuthoringLifecycle.Published, true, null, default);
        Assert.Equal(ProcessAuthoringOutcome.Accepted, saved.Outcome);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    private sealed class DriverObserver : IProcessLaunchVariableContributor {
        public ConcurrentQueue<ProcessLaunchPreparationContext> Contexts { get; } = new();
        public Action? AfterObservation { get; set; }
        public void Enrich(ProcessLaunchPreparationContext context, IDictionary<string, string> variables) {
            Contexts.Enqueue(context);
            variables[DriverProbe] = context.DriverActivations.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (context.DriverActivations.SingleOrDefault(item => item.DriverKey == ProbeDriver) is { } activation) {
                variables[RevisionVariable] = activation.Settings[RevisionSetting];
                var binding = Assert.Single(activation.InputArtifactBindings);
                variables[BindingVariable] = binding.SourceStepKey + "/" + binding.ArtifactExpectationKey;
            }
            AfterObservation?.Invoke();
        }
    }

    public enum LaunchScope { Global, Project, ProjectNode }
    public enum BoundaryChange { Publication, ProjectDeletion, ProjectRecreation }
}
