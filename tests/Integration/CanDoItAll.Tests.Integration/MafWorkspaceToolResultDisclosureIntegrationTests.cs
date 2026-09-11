using CanDoItAll.Infrastructure.Persistence;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed partial class MafWorkspaceToolResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceReadFile)]
    [InlineData(ToolContractCatalog.WorkspaceConvertDocument)]
    [InlineData(ToolContractCatalog.WorkspacePowerShellRunScript)]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImage)]
    public async Task Actual_configured_tool_restores_original_result_only_after_current_operation_grant_is_restored(string toolName) {
        await using var fixture = await Fixture.CreateAsync(toolName);
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(toolName == ToolContractCatalog.WorkspacePowerShellRunScript
            ? AgentToolEffectState.Unknown : AgentToolEffectState.None, saved.EffectState);
        Assert.Equal(toolName == ToolContractCatalog.WorkspaceReadFile || toolName == ToolContractCatalog.WorkspaceAnalyzeImage
            ? AgentToolProposalEffect.Read : AgentToolProposalEffect.Mutation, saved.Payload.Effect);
        Assert.Equal(ExecutionApprovalStatus.Approved, saved.ApprovalStatus);
        Assert.Equal(saved.Payload.Digest, saved.ApprovedDigest);
        var evidence = WorkspaceToolResultEvidence.Read(saved.DisclosureEvidence!);
        Assert.Equal(WorkspaceToolResultEvidenceState.Complete, evidence.State);
        Assert.Equal(fixture.Journal.StorageScope, evidence.Scope);
        Assert.NotEmpty(evidence.Paths);
        await File.WriteAllTextAsync(Path.Combine(fixture.Journal.WorkspaceRoot, "source.txt"), "A later human edit.");
        var counts = fixture.Operations.Counts;
        await fixture.SaveActorAsync(toolAllowed: false);
        var deniedClient = new ToolClient(toolName);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(deniedClient));
        Assert.Contains("workspace.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, deniedClient.Requests);
        Assert.Equal(counts, fixture.Operations.Counts);
        Assert.Equal(saved, await fixture.ProposalAsync());

        await fixture.SaveActorAsync();
        var current = await fixture.CurrentAuthorityAsync();
        Assert.True(current.ReadAllowed);
        Assert.False(current.MutationAllowed);
        var restoredClient = new ToolClient(toolName);
        var restored = await fixture.ExecuteAsync(restoredClient);
        Assert.Contains("completed", restored.ResponseText, StringComparison.Ordinal);
        Assert.Equal(1, restoredClient.Requests);
        Assert.Equal(counts, fixture.Operations.Counts);
        Assert.Equal(saved, await fixture.ProposalAsync());
        Assert.Contains("original", Assert.Single(restoredClient.Inputs), StringComparison.OrdinalIgnoreCase);
        if (toolName == ToolContractCatalog.WorkspaceReadFile) {
            Assert.DoesNotContain("A later human edit.", Assert.Single(restoredClient.Inputs), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Revoked_original_project_read_denies_the_saved_result_then_restoration_recovers_without_another_read() {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspaceReadFile);
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        await fixture.SaveActorAsync(sourceRead: false);
        var deniedClient = new ToolClient(fixture.ToolName);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(deniedClient));
        Assert.Contains("workspace.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, deniedClient.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
        await fixture.SaveActorAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.Journal.WorkspaceRoot, "source.txt"), "replacement bytes");
        var restored = new ToolClient(fixture.ToolName);
        await fixture.ExecuteAsync(restored);
        Assert.Contains("original", Assert.Single(restored.Inputs), StringComparison.Ordinal);
        Assert.DoesNotContain("replacement bytes", Assert.Single(restored.Inputs), StringComparison.Ordinal);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Fact]
    public async Task Source_revocation_after_approval_is_a_persisted_fresh_pre_dispatch_denial_without_an_owner_effect() {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspaceConvertDocument);
        var approval = await fixture.ExecuteAsync(new(fixture.ToolName));
        Assert.Single(approval.PendingApprovals);
        await fixture.Journal.ApproveAsync(approval.PendingApprovals);
        await fixture.SaveActorAsync(sourceRead: false);
        var client = new ToolClient(fixture.ToolName);
        await fixture.ExecuteAsync(client);
        var saved = await fixture.ProposalAsync();
        var completedRun = (await fixture.Journal.NewStore().GetExecutionRunAsync(fixture.Journal.Session.ExecutionRunId))!;
        Assert.Equal(2, completedRun.ToolAdmission!.Batches.Length);
        Assert.Empty(completedRun.ToolAdmission.Batches[^1].Proposals);
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(AgentToolEffectState.NotCommitted, saved.EffectState);
        Assert.Contains("workspace.result-disclosure-denied", saved.Result!.PayloadJson, StringComparison.Ordinal);
        Assert.Null(saved.DisclosureEvidence);
        Assert.Equal((0, 0, 0), fixture.Operations.Counts);
        await fixture.SaveActorAsync();
        var restored = new ToolClient(fixture.ToolName);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(restored));
        Assert.Contains("workspace.result-authority-unavailable", Codes(denied));
        Assert.Equal(0, restored.Requests);
        Assert.Equal((0, 0, 0), fixture.Operations.Counts);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reused_project_scope_cannot_relabel_a_cached_workspace_result(bool duringOperation) {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspaceConvertDocument);
        if (duringOperation) {
            fixture.Operations.AfterConversion = fixture.ReplaceProjectAsync;
        }
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        if (!duringOperation) {
            await fixture.ReplaceProjectAsync();
        }
        await fixture.SaveActorAsync();
        var next = new ToolClient(fixture.ToolName);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(next));
        Assert.Contains(duringOperation ? "workspace.result-authority-unavailable" : "workspace.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, next.Requests);
        Assert.Equal(1, fixture.Operations.Conversions);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Fact]
    public async Task Original_paths_cannot_be_restored_through_another_runtime_workspace_root() {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspaceReadFile);
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        var next = new ToolClient(fixture.ToolName);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(next,
            Path.Combine(fixture.Journal.WorkspaceRoot, "unrelated-workspace")));
        Assert.Contains("workspace.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, next.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Fact]
    public async Task A_listing_with_an_uncaptured_workspace_remains_unavailable_without_relisting() {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspaceListFiles, projectScope: false);
        fixture.ReadPath = ".";
        var foreignPath = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")).CombineDataPath("source.txt");
        var physical = Path.Combine(fixture.Journal.WorkspaceRoot, foreignPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
        await File.WriteAllTextAsync(physical, "original other-scope content");
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        Assert.Contains(foreignPath, saved.Result!.PayloadJson, StringComparison.Ordinal);
        Assert.Equal(WorkspaceToolResultEvidenceState.UncapturedWorkspaceScope, WorkspaceToolResultEvidence.Read(saved.DisclosureEvidence!).State);
        var next = new ToolClient(fixture.ToolName, readPath: fixture.ReadPath);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(next));
        Assert.Contains("workspace.result-authority-unavailable", Codes(denied));
        Assert.Equal(0, next.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Active_run_artifact_mapping_retains_the_original_execution_scope_and_each_project_lifetime(bool projectExecutionScope) {
        await using var fixture = await Fixture.CreateAsync(ToolContractCatalog.WorkspaceReadFile);
        await fixture.ConfigureExecutionArtifactAsync(projectExecutionScope);
        Assert.NotNull(fixture.AdmittedSession.BackgroundSource);
        Assert.Equal(AgentRuntimeContextPurpose.GovernedProcessAutomation, fixture.AdmittedRun.ToolAdmission!.Session.Purpose);
        Assert.Null(fixture.AdmittedRun.ChatSessionId);
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.AdmittedRun.MetadataJson));
        Assert.False(AgentTurnContextMetadata.ContainsTurnContextReference(fixture.AdmittedRun.MetadataJson));
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        var evidence = WorkspaceToolResultEvidence.Read(saved.DisclosureEvidence!);
        Assert.Equal(WorkspaceToolResultEvidenceState.Complete, evidence.State);
        Assert.Equal(fixture.ExecutionScope, evidence.ExecutionWorkspaceScope);
        var source = CanDoItAll.Modules.AgentFramework.WorkspaceToolSourceEvidence.Read(evidence.Source);
        Assert.Equal(projectExecutionScope ? 2 : 1, source.Projects.Length);
        Assert.Equal(fixture.ExecutionScope, source.ExecutionWorkspaceScope);
        Assert.Contains(evidence.Paths, path => path.RelativePath == fixture.ExecutionArtifactPath);
        Assert.Equal(fixture.AdmittedSession, source.Session);
        fixture.SetBackgroundReadAllowed(false);
        var revokedClient = new ToolClient(fixture.ToolName);
        var revoked = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(revokedClient));
        Assert.Contains("tool-admission.denied", Codes(revoked));
        Assert.Equal(0, revokedClient.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
        fixture.SetBackgroundReadAllowed(true);
        var originalScope = fixture.ExecutionScope;
        fixture.ExecutionScope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"));
        var differentScopeClient = new ToolClient(fixture.ToolName);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(differentScopeClient));
        Assert.Contains("workspace.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, differentScopeClient.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
        fixture.ExecutionScope = originalScope;
        await File.WriteAllTextAsync(Path.Combine(fixture.Journal.WorkspaceRoot, fixture.ExecutionArtifactPath), "later replacement bytes");
        var restored = new ToolClient(fixture.ToolName);
        await fixture.ExecuteAsync(restored);
        Assert.Contains("original", Assert.Single(restored.Inputs), StringComparison.Ordinal);
        Assert.DoesNotContain("later replacement bytes", Assert.Single(restored.Inputs), StringComparison.Ordinal);
        Assert.Equal(saved, await fixture.ProposalAsync());
        if (projectExecutionScope) {
            await fixture.ReplaceExecutionProjectAsync();
            var replacementClient = new ToolClient(fixture.ToolName);
            var replaced = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(replacementClient));
            Assert.Contains("workspace.result-disclosure-denied", Codes(replaced));
            Assert.Equal(0, replacementClient.Requests);
            Assert.Equal(saved, await fixture.ProposalAsync());
        }
    }

    private static IEnumerable<string> Codes(Exception exception) {
        if (exception is AgentToolAdmissionException admission) {
            yield return admission.Code;
        }
        if (exception.InnerException is { } inner) {
            foreach (var code in Codes(inner)) {
                yield return code;
            }
        }
    }

    private sealed partial class Fixture(TestApplication application, AsyncServiceScope scope, AgentToolAdmissionJournalFixture journal,
        ProjectWriteAdmission project, string toolName, CapabilityCatalogItem capability) : IAsyncDisposable {
        internal AgentToolAdmissionJournalFixture Journal => journal;
        internal ProjectWriteAdmission Project { get; private set; } = project;
        private readonly Guid sourceProjectId = project.ProjectId;
        internal string ToolName => toolName;
        internal string ReadPath { get; set; } = "source.txt";
        internal WorkspaceScopeDescriptor? ExecutionScope { get; set; }
        internal string ExecutionArtifactPath { get; private set; } = string.Empty;
        internal OwnedOperations Operations { get; } = new();
        internal IServiceProvider Services => scope.ServiceProvider;
        private AgentDefinition agent = journal.Agent;
        private ProjectWriteAdmission? executionProject;
        private string processRunId = string.Empty;
        private string processStepId = string.Empty;

        internal static async Task<Fixture> CreateAsync(string toolName, bool projectScope = true) {
            var application = await TestApplication.CreateAsync();
            var scope = application.Services.CreateAsyncScope();
            AgentToolAdmissionJournalFixture? journal = null;
            try {
                var services = scope.ServiceProvider;
                var projectId = Guid.NewGuid();
                Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Workspace disclosure source" })).IsSuccess);
                var project = (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId))!;
                var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
                var workspace = projectScope ? WorkspaceScopeDescriptor.Project(projectId.ToString("D")) : WorkspaceScopeDescriptor.Sandbox;
                journal = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding:
                    new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)),
                    transientContext: new("Original admitted workspace source", workspace), storageScope: workspace);
                Assert.Null(await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(journal.Session.ExecutionRunId));
                Assert.NotNull(await journal.NewStore().GetExecutionRunAsync(journal.Session.ExecutionRunId));
                var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Tool, "reviewed-workspace-result",
                    "Reviewed workspace result", string.Empty, string.Empty, JsonSerializer.Serialize(new { tool = toolName, approvalRequired = true }),
                    CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UtcNow, true);
                var fixture = new Fixture(application, scope, journal, project, toolName, capability);
                await fixture.SaveActorAsync();
                fixture.agent = (await services.GetRequiredService<ISandboxWorkspaceCatalogStore>().LoadCatalogAsync()).Agents.Single(item => item.Id == journal.Agent.Id);
                await File.WriteAllTextAsync(Path.Combine(journal.WorkspaceRoot, "source.txt"), "original reviewed content");
                await File.WriteAllTextAsync(Path.Combine(journal.WorkspaceRoot, "script.ps1"), "Write-Output 'original reviewed command'");
                await File.WriteAllBytesAsync(Path.Combine(journal.WorkspaceRoot, "source.png"), Convert.FromBase64String(Png));
                return fixture;
            } catch {
                if (journal is not null) {
                    await journal.DisposeAsync();
                }
                await scope.DisposeAsync();
                await application.DisposeAsync();
                throw;
            }
        }

        internal async Task SaveActorAsync(bool toolAllowed = true, bool sourceRead = true) {
            var access = new AgentWorkspaceToolAccessSettings {
                Profile = AgentWorkspaceToolProfileKind.Custom,
                CanReadFiles = toolAllowed || toolName != ToolContractCatalog.WorkspaceReadFile,
                CanTransformArtifacts = toolAllowed && toolName is ToolContractCatalog.WorkspaceConvertDocument or ToolContractCatalog.WorkspaceAnalyzeImage,
                CanRunLocalScripts = toolAllowed && toolName == ToolContractCatalog.WorkspacePowerShellRunScript
            };
            var catalog = Services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
            var currentActor = (await catalog.LoadCatalogAsync()).Agents.Single(item => item.Id == agent.Id);
            var configuration = AgentWorkspaceToolAccessMetadata.Write(currentActor.ConfigurationJson, access);
            var grantedProjects = sourceRead
                ? new[] { Project, executionProject }.OfType<ProjectWriteAdmission>().ToArray() : [];
            configuration = AgentProjectStructureAccessMetadata.Write(configuration, new() {
                CanRead = sourceRead, AllowedProjectIds = grantedProjects.Select(item => item.ProjectId).ToList(),
                AllowedProjectLifetimes = grantedProjects.Select(item => new AgentProjectStructureLifetime(
                    item.DatabaseProfileId, item.ProjectId, item.LifetimeId)).ToList()
            });
            var actor = agent with {
                IsTemplate = false, TemplateKey = string.Empty, Status = AgentLifecycleStatus.Active,
                Permissions = agent.Permissions with { CanUseTools = true },
                ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(configuration),
                Capabilities = [new(capability.Id, capability.Key, capability.Kind, capability.ProofStatus, capability.LastVerifiedAtUtc, capability.ProofNotes)]
            };
            await catalog.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Where(item => item.Id != actor.Id).Append(actor).ToArray(),
                Capabilities = current.Capabilities.Where(item => item.Id != capability.Id).Append(capability).ToArray()
            });
            var saved = (await catalog.LoadCatalogAsync()).Agents.Single(item => item.Id == actor.Id);
            Assert.Equal(toolAllowed, AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(
                AgentWorkspaceToolAccessMetadata.Read(saved.ConfigurationJson), toolName));
            Assert.Equal(sourceRead, AgentProjectStructureAccessMetadata.Read(saved.ConfigurationJson).CanRead);
            Assert.Equal(sourceRead, AgentProjectStructureAccessMetadata.Read(saved.ConfigurationJson).AllowedProjectLifetimes
                .Contains(new(Project.DatabaseProfileId, Project.ProjectId, Project.LifetimeId)));
        }

        internal async Task<AgentExecutionAuthorityRecord> CurrentAuthorityAsync() {
            var original = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(journal.Detail.Run.MetadataJson)!;
            var source = AgentTurnContextMetadata.TryReadTurnContextReference(journal.Detail.Run.MetadataJson)!;
            return await Services.GetRequiredService<IAgentExecutionAuthorityResolver>().ResolveAsync(new(agent.Id,
                source.SourceKind, source.SourceId, original.WorkspaceScope, journal.Profile.Generation, UiAccessHint: null));
        }

        internal async Task ReplaceProjectAsync() {
            var projects = Services.GetRequiredService<ProjectsService>();
            await projects.DeleteAsync(Project.ProjectId);
            Assert.True((await projects.CreateAsync(Project.ProjectId, new() { Name = "Unrelated replacement" })).IsSuccess);
            var original = Project;
            Project = (await Services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(Project.ProjectId))!;
            Assert.NotEqual(original.LifetimeId, Project.LifetimeId);
        }

        internal async Task ConfigureExecutionArtifactAsync(bool useProject, bool admitBackground = true) {
            if (useProject) {
                var id = Guid.NewGuid();
                Assert.True((await Services.GetRequiredService<ProjectsService>().CreateAsync(id, new() { Name = "Owning execution scope" })).IsSuccess);
                executionProject = (await Services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(id))!;
                ExecutionScope = WorkspaceScopeDescriptor.Project(id.ToString("D"));
                await SaveActorAsync();
                agent = (await Services.GetRequiredService<ISandboxWorkspaceCatalogStore>().LoadCatalogAsync()).Agents.Single(item => item.Id == agent.Id);
            } else {
                ExecutionScope = WorkspaceScopeDescriptor.Organization(journal.Profile.ProfileId.ToString("N"));
            }
            processRunId = Guid.NewGuid().ToString("D");
            processStepId = Guid.NewGuid().ToString("D");
            ReadPath = WorkspaceScopeDescriptor.Sandbox.CombineArtifactPath("process-runs", processRunId, "original.txt");
            ExecutionArtifactPath = ExecutionScope.CombineArtifactPath("process-runs", processRunId, "original.txt");
            var physical = Path.Combine(journal.WorkspaceRoot, ExecutionArtifactPath);
            Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
            await File.WriteAllTextAsync(physical, "original current-run artifact");
            if (admitBackground) {
                await AdmitBackgroundArtifactAsync();
            }
        }

        internal async Task ReplaceExecutionProjectAsync() {
            var original = executionProject!;
            var projects = Services.GetRequiredService<ProjectsService>();
            await projects.DeleteAsync(original.ProjectId);
            Assert.True((await projects.CreateAsync(original.ProjectId, new() { Name = "Replacement execution scope" })).IsSuccess);
            executionProject = (await Services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(original.ProjectId))!;
            Assert.NotEqual(original.LifetimeId, executionProject.LifetimeId);
            await SaveActorAsync();
        }

        internal async Task CompleteThroughLostFinalAcknowledgementAsync() {
            var response = await ExecuteAsync(new(toolName, readPath: ReadPath));
            var approval = Assert.Single(response.PendingApprovals);
            Assert.Equal(toolName, approval.ToolName);
            Assert.NotNull(approval.ToolAdmission);
            Assert.Equal((0, 0, 0), Operations.Counts);
            await ApproveAsync(response.PendingApprovals);
            var checkpointFailure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(new(toolName, failAfterResult: true, readPath: ReadPath)));
            AgentToolAdmissionJournalFixture.RequireScriptedFault(checkpointFailure,
                "Injected final acknowledgement loss after the durable workspace result.");
            Assert.Equal(AgentToolProposalState.Completed, (await ProposalAsync()).State);
        }

        internal async Task<AgentToolProposalRecord> ProposalAsync()
            => Assert.Single((await journal.NewStore().GetExecutionRunAsync(AdmittedSession.ExecutionRunId))!.ToolAdmission!.Batches
                .SelectMany(batch => batch.Proposals));

        internal async Task<AgentRuntimeResponse> ExecuteAsync(ToolClient client, string? workspaceRoot = null) {
            using var audit = ExecutionScope is null ? null : WorkspaceExecutionAuditContext.BeginScope(
                AdmittedRun with { ProcessRunId = processRunId, ProcessStepId = processStepId }, journal.StorageScope, ExecutionScope);
            var dependencies = MafAgentRuntimeDependencies.FromServices(Services);
            dependencies = dependencies with {
                ProviderAgentFactory = new ToolAgentFactory(client, toolName), ToolAdmissionJournal = NewAdmissionJournal(),
                WorkspaceRuntimeServicesFactory = new OwnedRuntimeFactory(Services.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(), Operations),
                ImageAnalysisService = Operations,
                CapabilityDependencies = dependencies.CapabilityDependencies with { RuntimeToolProviders = [], ContextContributors = [] }
            };
            var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
                AdmittedToolSession = AdmittedSession, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
                Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(AdmittedRun.MetadataJson),
                AuthorityPolicyFingerprint = AdmittedSession.BackgroundSource?.OwnerFingerprint.Value ?? "fixture-authority",
                ModelContextDigest = backgroundSource is null ? "fixture-context" : AgentTurnContextMetadata.EmptyModelContextDigest,
                ContextIntent = AgentRuntimeContextIntent.Empty with {
                    Purpose = AdmittedRun.ToolAdmission!.Session.Purpose,
                    SourceKind = backgroundSource?.SourceKind ?? (journal.StorageScope.Kind == WorkspaceScopeKind.Project ? "project-structure" : "agents"),
                    SourceId = backgroundSource is null
                        ? journal.StorageScope.Kind == WorkspaceScopeKind.Project ? sourceProjectId.ToString("D") : "agents"
                        : AdmittedRun.SourceId,
                    WorkspaceToolsEnabled = true, ToolCapabilitiesEnabled = true, RuntimeToolProvidersEnabled = false
                }
            };
            var provider = journal.Provider with { Transport = ProviderTransportKind.ChatCompletions,
                DefaultModel = agent.Model, SuggestedModels = [agent.Model],
                ConfigurationJson = ProviderModelThinkingConfiguration.Write(journal.Provider.ConfigurationJson, agent.Model,
                    new(agent.Model, AgentThinkingEffortSupportStatus.Supported,
                        AgentThinkingEffortControlMode.EffortLevels, [AgentReasoningEffortLevel.Medium], AgentReasoningEffortLevel.Medium))
            };
            var runtimeChat = backgroundSource is null ? journal.Detail.ChatSession!
                : ChatSessionRuntimeCompatibilityAdapter.CreateRuntimeSession(AdmittedRun, agent.Id, transcriptSession: null);
            return await new MafAgentRuntime(workspaceRoot ?? journal.WorkspaceRoot, journal.StorageScope, dependencies).ExecutionPort.ExecuteAsync(
                new(agent, provider, runtimeChat, [capability], [], "Run the reviewed workspace operation.", string.Empty,
                    (_, _, _) => Task.CompletedTask, ExecutionOptions: options));
        }

        public async ValueTask DisposeAsync() {
            await scope.DisposeAsync();
            await journal.DisposeAsync();
            await application.DisposeAsync();
        }
    }

    private sealed class OwnedRuntimeFactory(IPhysicalFileSystemPathPolicyFactory paths, OwnedOperations operations) : IWorkspaceRuntimeServicesFactory {
        public WorkspaceRuntimeServices Create(WorkspaceExecutionScope scope) {
            var targets = new ExternalTargetPathRegistry();
            var commands = new WorkspaceCommandExecutionService(scope.WorkspaceRoot, operations, paths, scope.Scope, externalTargetRegistry: targets);
            var images = new WorkspaceImageOperationService(scope.WorkspaceRoot, paths, scope.Scope, targets);
            return new(scope, new WorkspaceFileService(scope.WorkspaceRoot, paths, scope.Scope, targets), commands,
                new WorkspaceArtifactToolService(scope.WorkspaceRoot, commands, operations, paths, scope.Scope, images, targets),
                images, operations, targets);
        }
    }

    private sealed class OwnedOperations : IWorkspaceProcessHost, IWorkspaceDocumentMarkdownConverter, IAgentImageAnalysisService {
        internal int Commands { get; private set; }
        internal int Conversions { get; private set; }
        internal int Analyses { get; private set; }
        internal (int Commands, int Conversions, int Analyses) Counts => (Commands, Conversions, Analyses);
        internal Func<Task>? AfterConversion { get; set; }
        public ExecutionBoundaryDescriptor DescribeBoundary() => new("Test transport", "Workspace", "None", "None", "Recorded", false,
            "Command recipe and receipts are real; the process transport is recorded.");
        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(WorkspaceProcessExecutionRequest request, CancellationToken cancellationToken = default) {
            Commands++;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(true, 0, "original reviewed command", string.Empty,
                false, false, now, now, false, DescribeBoundary(), string.Empty));
        }
        public async Task<WorkspaceDocumentMarkdownConversionResult> ConvertToMarkdownAsync(WorkspaceDocumentMarkdownConversionRequest request,
            CancellationToken cancellationToken = default) {
            Conversions++;
            var markdown = await File.ReadAllTextAsync(request.SourcePath, cancellationToken);
            if (AfterConversion is not null) {
                await AfterConversion();
            }
            return new(true, "original converted document", request.SourcePath, markdown, markdown.Length, false, string.Empty);
        }
        public Task<AgentImageAnalysisResult> AnalyzeAsync(AgentImageAnalysisRequest request, CancellationToken cancellationToken = default) {
            Analyses++;
            Assert.Single(request.Sources);
            return Task.FromResult(new AgentImageAnalysisResult(request.Model, "original reviewed image", 3, 4));
        }
    }

    private sealed class ToolAgentFactory(ToolClient client, string name) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.True(new ProviderProfileService().ResolveFeatureMatrix(provider).SupportsApprovalRequiredAIFunction);
            Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(options.ChatOptions!.Tools!, tool => tool.Name == name));
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ToolClient(string name, bool failAfterResult = false, string readPath = "source.txt") : IChatClient {
        internal int Requests { get; private set; }
        internal List<string> Inputs { get; } = [];
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) {
            Requests++;
            var input = messages.ToArray();
            Inputs.Add(JsonSerializer.Serialize(input, MafToolProtocolCodec.SerializationOptions));
            if (input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()) {
                if (failAfterResult) {
                    throw new IOException("Injected final acknowledgement loss after the durable workspace result.");
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            Dictionary<string, object?> arguments = new() {
                [WorkspaceToolResultDisclosure.IsCollectionTool(name) ? "relativePath" : "path"] = name switch {
                    ToolContractCatalog.WorkspacePowerShellRunScript => "script.ps1",
                    ToolContractCatalog.WorkspaceAnalyzeImage => "source.png",
                    _ => readPath
                }
            };
            if (name == ToolContractCatalog.WorkspaceSearch) {
                arguments["query"] = "original";
            }
            if (name == ToolContractCatalog.WorkspaceAnalyzeImage) {
                arguments["prompt"] = "Describe the original image";
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("workspace-diagnostic-call", name, arguments)])));
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) {
                yield return update;
            }
        }
        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }

    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";
}
