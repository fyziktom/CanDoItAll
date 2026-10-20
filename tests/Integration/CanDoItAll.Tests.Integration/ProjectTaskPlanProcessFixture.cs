using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Runtime;
using CanDoItAll.Tests.Integration.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectTaskPlanProcessFixture : IAsyncDisposable {
    private readonly IAgentToolBackgroundSourcePolicy policy;
    private ProjectTaskPlanProcessFixture(ProjectTaskPlanOperationFixture sql, FileSandboxWorkspaceStore store,
        AgentToolProfileBinding profile, AgentDefinition executor, AgentDefinition? launcher,
        ProjectTaskPlanProcessAdmissionFixture process, ExecutionRunRecord original, ProviderProfile provider,
        AgentPlanningLiveOwnerFixture.PlanningClock clock, IAgentToolBackgroundSourcePolicy policy, MutableGenerations generations) {
        Sql = sql;
        Store = store;
        Profile = profile;
        Executor = executor;
        Launcher = launcher;
        Process = process;
        Original = original;
        Provider = provider;
        Clock = clock;
        Generations = generations;
        this.policy = policy;
        Journal = new(store, profile, clock, backgroundSources: [policy]);
        Owner = new(Journal);
    }

    internal ProjectTaskPlanOperationFixture Sql { get; }
    internal FileSandboxWorkspaceStore Store { get; }
    internal AgentToolProfileBinding Profile { get; }
    internal AgentDefinition Executor { get; }
    internal ProviderProfile Provider { get; }
    internal AgentDefinition? Launcher { get; }
    internal ProjectTaskPlanProcessAdmissionFixture Process { get; }
    internal ExecutionRunRecord Original { get; }
    internal AgentPlanningLiveOwnerFixture.PlanningClock Clock { get; }
    internal MutableGenerations Generations { get; }
    internal AgentToolAdmissionJournal Journal { get; }
    internal IAgentToolBackgroundSourcePolicy SourcePolicy => policy;
    internal AgentPlanningDecisionOwner Owner { get; }
    internal ProjectTaskPlanProcessCommand? PublishedCommand { get; private set; }
    internal ProjectTaskPlanInvocation? Invocation { get; private set; }
    internal Func<Task>? BeforePublication { get; set; }
    internal Func<AgentPlanningPublicationExpectation, AgentPlanningPublicationExpectation>? ChangeExpectation { get; set; }
    internal WorkspaceScopeDescriptor Scope => WorkspaceScopeDescriptor.Organization(Profile.ProfileId.ToString("N"));

    internal static async Task<ProjectTaskPlanProcessFixture> CreateAsync(bool agentSource = true, bool sameExecutor = false,
        IInterceptor? processReadProbe = null, bool registeredSource = false,
        Func<AgentDefinition, ProviderProfile, IAgentInheritedPlanningParticipant, AgentToolSemanticDigest>? runtimeContract = null,
        bool includeOperationContract = true) {
        var clock = new AgentPlanningLiveOwnerFixture.PlanningClock(DateTimeOffset.UtcNow);
        var generations = new MutableGenerations();
        var sql = await ProjectTaskPlanOperationFixture.CreateAsync(configureServices: services => {
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IAgentExecutionProfileGenerationSource>(generations);
            if (processReadProbe is not null) {
                services.AddScoped(provider => ProcessDecisionDispatchAuthorityFixture.Query(provider, clock, processReadProbe));
            }
        });
        try {
            var services = sql.Services;
            var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
            var profile = new AgentToolProfileBinding(database.Profile.Profile.Id, database.Profile.Profile.Runtime.Fingerprint, new(database.Generation));
            generations.Generation = profile.Generation;
            var scope = WorkspaceScopeDescriptor.Organization(profile.ProfileId.ToString("N"));
            var store = new FileSandboxWorkspaceStore(services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(), scope,
                new AgentProjectAccessCatalogPolicy(services.GetRequiredService<ProjectWriteAdmissionService>(), profile.ProfileId));
            await using var seed = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding: profile, includeRecoveryInput: true);
            var launcher = seed.Agent with {
                Id = Guid.NewGuid(), Name = "Original Process launcher", TemplateKey = string.Empty, IsTemplate = false, Tags = [],
                Status = AgentLifecycleStatus.Active, Permissions = seed.Agent.Permissions with { CanUseTools = true },
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write("{}", new() {
                    CanRead = true, CanWrite = true, AllowedProjectIds = [sql.Project.ProjectId],
                    AllowedProjectLifetimes = [new(sql.Project.DatabaseProfileId, sql.Project.ProjectId, sql.Project.LifetimeId)]
                })
            };
            var executor = sameExecutor ? launcher : launcher with {
                Id = Guid.NewGuid(), Name = "Task-plan Process executor",
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write("{}", new() { CanWriteTasks = true })
            };
            var provider = seed.Provider with { Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.ChatCompletions };
            await store.UpdateCatalogAsync(current => current with {
                Agents = agentSource && !sameExecutor ? [.. current.Agents, launcher, executor] : [.. current.Agents, executor],
                Providers = current.Providers.Where(item => item.Id != provider.Id).Append(provider).ToArray()
            });
            var authority = services.GetRequiredService<ProjectProcessLaunchAuthorityService>();
            var source = agentSource ? await authority.CaptureAgentAsync(
                new(sql.Project.DatabaseProfileId, sql.Project.ProjectId, sql.Project.LifetimeId),
                new(new(Guid.NewGuid()), launcher.Id, profile.ProfileId, profile.Generation, scope,
                    true, true, "task-authority-fixture", "original-launch-source", schemaVersion: AgentExecutionAuthorityRecord.CurrentSchemaVersion),
                ProcessLaunchAgentOperation.StructureStart)
                : await authority.CaptureLocalAsync(sql.Project.ProjectId, ProcessLaunchOperatorSurface.UserInterface);
            var process = await ProjectTaskPlanProcessAdmissionFixture.CreateAsync(sql, source, executor.Id, clock);
            IAgentToolBackgroundSourcePolicy policy = registeredSource
                ? services.GetServices<IAgentToolBackgroundSourcePolicy>().OfType<ProcessToolBackgroundSourcePolicy>().Single()
                : new SelectedSourcePolicy(services.GetRequiredService<IProcessDecisionDispatchAuthorityQuery>(), process, profile);
            var journal = new AgentToolAdmissionJournal(store, profile, clock, backgroundSources: [policy]);
            var assignment = await services.GetRequiredService<IProcessRuntimeStepAssignmentStore>()
                .LoadAsync(process.State.RunId, process.Parent.Child.Address.StepInstanceId)
                ?? throw new InvalidOperationException("The original Process assignment is missing.");
            var metadata = services.GetRequiredService<ProcessExecutionMetadataComposer>().ComposeClaimedExecution(
                includeOperationContract ? assignment : assignment with { AllowedOperations = [] },
                new(process.Source.Evidence.DispatchClaimToken));
            var contract = runtimeContract?.Invoke(executor, provider, services.GetServices<IAgentInheritedPlanningParticipant>()
                .Single(item => item.Owner == AgentPlanningOwner.ProjectTaskPlan)) ?? AgentToolProtocolEnvelope.ComputeDigest("task-authority-fixture");
            var run = seed.Detail.Run with {
                Id = process.Parent.Child.Address.ChildId.Value, AgentId = executor.Id, ChatSessionId = null, ToolAdmission = null,
                MetadataJson = AgentPlanningRunMetadata.ApplyInherited(metadata, AgentPlanningOwner.ProjectTaskPlan,
                    contract, scope),
                SourceKind = ProjectTaskPlanProcessContract.SourceKind, SourceId = process.Source.Evidence.StepKey,
                ProcessRunId = process.Source.Evidence.RunId.Value.ToString("D"),
                ProcessStepId = process.Source.Evidence.StepInstanceId.Value.ToString("D"),
                CorrelationId = process.Source.Evidence.RunId.Value.ToString("D"),
                CausationId = process.Source.Evidence.StepInstanceId.Value.ToString("D"),
                RequestedBy = "process-runtime", RequestedByKind = "system",
                CreatedAtUtc = clock.Now, UpdatedAtUtc = clock.Now
            };
            var parentBinding = AgentToolProtocolEnvelope.Create(ProcessDecisionChildMaterialBindingCodec.Format,
                ProcessDecisionChildMaterialBindingCodec.Version, ProcessDecisionChildMaterialBindingCodec.Serialize(process.Parent));
            var admission = await new AgentInheritedPlanningAdmission(journal).CreateAsync(run, "Plan one original task.",
                AgentPlanningOwner.ProjectTaskPlan, parentBinding, async (binding, token) => {
                    var parent = process.Parent;
                    var caller = new ProjectTaskPlanCaller(ProjectTaskPlanCallerKind.ProcessAgent,
                        ProjectDefinitionGovernedBindings.ProcessCaller(process.Source).Key.ToArray());
                    ProjectTaskPlanProcessTarget target = new(binding.Session, sql.Project, process.Source, new(true, true),
                        await sql.Publisher().GetWriteContextAsync(caller, token));
                    var material = AgentToolProtocolEnvelope.Create(ProjectTaskPlanProcessCodec.MaterialFormat, 1, process.Material.PayloadJson);
                    AgentPlanningInheritedOrigin origin = new(profile, sql.Binding.Namespace.Value, executor.Id,
                        binding.Session.Reference.BackgroundSource!, parentBinding,
                        parent.Child.MaterialId.Value, process.Material.CapturedAtUtc, material.Digest, process.Policy,
                        new(parent.Child.BudgetId, parent.Child.AllocationId, parent.Child.Grant));
                    return new(ProjectTaskPlanProcessCodec.WriteTarget(target), origin);
                });
            var saved = await store.SaveExecutionRunDetailAsync(new(run with { ToolAdmission = admission }, null, [], []));
            return new(sql, store, profile, executor, agentSource ? launcher : null, process, saved.Run, provider, clock, policy, generations);
        } catch {
            await sql.DisposeAsync();
            throw;
        }
    }

    internal async Task<ExecutionRunRecord> ReadAsync()
        => await Store.GetExecutionRunAsync(Original.Id) ?? throw new InvalidOperationException("Original inherited run is missing.");

    internal async Task<ExecutionRunRecord> PrepareAsync() {
        await using var lease = await Owner.StartInheritedAsync(AgentPlanningDecisionExpectation.From(await ReadAsync()), AllowRead, default);
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        var origin = Original.ToolAdmission!.PlanningDecision!.InheritedOrigin!;
        var basis = ProjectTaskPlanProcessCodec.Basis(origin, Process.Material);
        await Owner.InheritBasisAsync(work, (_, _) => Task.FromResult(basis), default);
        var dispatch = await Owner.EnterProviderAsync(work, basis.Value.Digest, AgentToolProtocolEnvelope.ComputeDigest("task-fixture-response"), default);
        var current = await ReadAsync();
        var call = ProjectTaskPlanProcessCodec.Prepare(current.ToolAdmission!.PlanningDecision!, basis, ProjectTaskPlanAgentFixture.Draft);
        await Owner.AdmitResponseAsync(work, dispatch.Id, dispatch.RequestDigest,
            AgentToolProtocolEnvelope.Create("task-fixture-response", 1, ProjectTaskPlanAgentFixture.Draft), call, default);
        Clock.Timestamp += TimeSpan.FromSeconds(1).Ticks;
        return await Owner.PauseForApprovalAsync(work, new(Guid.NewGuid().ToString("D"), call.CallId,
            call.Payload.ToolName, "function", "Approve the original Process task plan.", call.Payload.ArgumentsJson), default);
    }

    internal async Task<ProjectTaskPlanOutcome> PublishAsync(ExecutionRunRecord waiting, CancellationToken token = default,
        bool settleOwner = true) {
        var binding = ProjectTaskPlanAgentFixture.Binding(waiting);
        var origin = waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin!;
        var request = new AgentSavedEffectContinuationRequest(waiting.Id, null, waiting.Revision, waiting.ToolAdmission.Revision, binding,
            waiting.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) {
                ToolAdmission = item.ToolAdmission
            }).ToArray());
        await using var lease = await Owner.ResumeInheritedSavedEffectAsync(request, AgentPlanningDecisionExpectation.From(waiting),
            new(origin, RequireOriginalAsync), AllowRead, token);
        using var bound = lease.Bind();
        await Journal.ObserveSavedEffectReceiptAsync(lease, new ReceiptProbe(this), token);
        var payload = AgentToolJournalTransitions.RequireProposal(waiting.ToolAdmission, binding).Payload;
        var claim = await Journal.ClaimSavedInvocationAsync(lease, binding, payload, token);
        using var claimed = claim.Bind();
        var current = await ReadAsync();
        PublishedCommand = ProjectTaskPlanProcessCodec.Resolve(AgentPlanningDecisionObservation.From(current)!,
            current.ToolAdmission!.Session, payload, binding);
        var expected = AgentPlanningPublicationExpectation.Inherited(Scope, current.ToolAdmission.Session,
            current.Revision, current.ToolAdmission.Revision, Launcher?.Id == Executor.Id ? null : Launcher?.Id);
        if (ChangeExpectation is not null) {
            expected = ChangeExpectation(expected);
        }
        var authority = new ProjectTaskPlanProcessAuthority(PublishedCommand, Store, expected, lease.PlanningWork!, binding,
            Sql.Services.GetRequiredService<ProjectProcessLaunchAuthorityService>(), Sql.Services.GetRequiredService<IProcessDecisionPublicationFence>(), Generations);
        Invocation = new(PublishedCommand.Target.Caller, authority, Sql.Authority);
        if (BeforePublication is not null) {
            await BeforePublication();
        }
        var outcome = await Sql.Publisher().PublishAsync(PublishedCommand.Command, Invocation, token);
        var result = ProjectTaskPlanAgentResult.From(outcome);
        await Journal.CompleteInvocationAsync(claim, ProjectTaskPlanAgentCodec.Write(ProjectTaskPlanAgentCodec.ResultFormat, result, 65_536),
            result.EffectState, token, requiresOwnerReconciliation: result.EffectState == AgentToolEffectState.Unknown);
        if (settleOwner && result.EffectState != AgentToolEffectState.Unknown) {
            await Journal.CheckpointSavedEffectAsync(lease, token);
            await Owner.SettleTerminalAsync(lease.PlanningWork!, result.EffectState == AgentToolEffectState.Committed
                ? AgentPlanningGenerationState.Completed : AgentPlanningGenerationState.Failed, token);
        }
        return outcome;
    }

    internal Task<ProjectTaskPlanOutcome> ReplayAsync()
        => Sql.Publisher().PublishAsync(PublishedCommand!.Command, Invocation!);

    internal Task RevokeAsync(Guid agentId, Func<AgentDefinition, AgentDefinition?> change)
        => Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id == agentId ? change(agent) : agent).OfType<AgentDefinition>().ToArray()
        });

    private async Task RequireOriginalAsync(ExecutionRunRecord run, CancellationToken token) {
        var observed = await policy.ObserveAsync(run, Profile, token);
        if (!observed.ReadAllowed || !observed.DispatchAllowed) {
            throw new AgentToolReceiptAccessDeniedException();
        }
    }

    internal void ExpireClaim() => Clock.Now = Process.ClaimExpiresAtUtc;
    private static ValueTask<bool> AllowRead(AgentToolSessionObservation _, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(true);
    }
    public ValueTask DisposeAsync() => Sql.DisposeAsync();

    internal sealed class MutableGenerations : IAgentExecutionProfileGenerationSource {
        internal DatabaseProfileGeneration Generation { get; set; } = new(1);
        public DatabaseProfileGeneration GetGeneration() => Generation;
        internal void Advance() => Generation = new(Generation.Value + 1);
    }

    private sealed class ReceiptProbe(ProjectTaskPlanProcessFixture fixture) : IAgentToolReceiptReconciliationProvider {
        public bool Supports(string toolName) => toolName == ProjectStructureToolPolicy.ProjectTaskPlanPublish;
        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            var admitted = await claim.RequireAsync(cancellationToken);
            var original = ProjectTaskPlanProcessCodec.Resolve(admitted.Planning!, admitted.Session, admitted.Payload, admitted.Binding);
            var result = await fixture.Sql.Reader.LookupAsync(original.Command.Operation, new(original.Target.Caller, fixture.Sql.Authority), cancellationToken);
            Assert.Equal(ProjectTaskPlanLookupStatus.Open, result.Status);
            return AgentToolReceiptObservation.NotObserved;
        }
    }

    private sealed class SelectedSourcePolicy(IProcessDecisionDispatchAuthorityQuery query, ProjectTaskPlanProcessAdmissionFixture process,
        AgentToolProfileBinding profile) : IAgentToolBackgroundSourcePolicy {
        public string SourceKind => ProjectTaskPlanProcessContract.SourceKind;
        public ValueTask<AgentToolBackgroundSourceObservation> ObserveInitialInheritedAsync(ExecutionRunRecord candidate,
            AgentToolProfileBinding current, AgentToolProtocolEnvelope originalParent, CancellationToken cancellationToken = default) {
            Assert.Equal(AgentToolProtocolEnvelope.Create(ProcessDecisionChildMaterialBindingCodec.Format,
                ProcessDecisionChildMaterialBindingCodec.Version, ProcessDecisionChildMaterialBindingCodec.Serialize(process.Parent)), originalParent);
            return ObserveAsync(candidate, current, cancellationToken);
        }
        public async ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution,
            AgentToolProfileBinding current, CancellationToken cancellationToken = default) {
            Assert.Equal(profile, current);
            Assert.Equal(process.Source.Evidence.ExecutionRunId, execution.Id);
            Assert.Equal(process.Source.Evidence.ExecutorAgentId, execution.AgentId);
            var observed = await query.ReadDecisionDispatchAsync(process.Source.Evidence, process.Parent,
                ProjectTaskPlanProcessContract.Execution, ProjectTaskPlanProcessContract.Content, cancellationToken);
            return new(AgentToolProtocolEnvelope.ComputeDigest(process.Source.OwnerFingerprint),
                observed.Snapshot is not null, observed.Snapshot?.ObservedCurrentDispatch == true);
        }
    }
}
