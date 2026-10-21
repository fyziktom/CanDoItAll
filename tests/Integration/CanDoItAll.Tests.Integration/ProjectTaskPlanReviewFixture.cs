using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

internal sealed record ProjectTaskPlanReviewFixture(ProjectTaskPlanExecutionEntryTests.EntryFixture Entry,
    ProcessStepMaterialFixture Process, ProcessDecisionChildMaterialBinding Parent, ExecutionRunRecord Waiting,
    ProcessRuntimeStateSnapshot State, ProcessDecisionReviewRequest Request, ProjectTaskPlanScenarioClock Clock) : IAsyncDisposable {
    internal IServiceProvider Services => Entry.Files.Sql.Services;
    internal bool ManagerAllowed { get; set; } = true;
    internal RuntimeCommandContext CommandContext => new(RuntimeCommandId.New(),
        new(ProcessEventActorKind.Manager, new("review-completion-fixture")), new("review-completion-fixture"), Clock.GetUtcNow());

    internal static async Task<ProjectTaskPlanReviewFixture> CreateAsync(ReadBasisMode mode = ReadBasisMode.StepSnapshot,
        CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null,
        Action<IServiceCollection>? configureServices = null, int steps = 1, int promptCharacters = 16_384) {
        var clock = new ProjectTaskPlanScenarioClock();
        var entry = await ProjectTaskPlanExecutionEntryTests.EntryFixture.CreateAsync(testEnvironment: environment, activeProfile: profile,
            configureServices: services => {
                services.AddSingleton<TimeProvider>(clock);
                configureServices?.Invoke(services);
            });
        ProcessStepMaterialFixture? process = null;
        try {
            process = await ProcessStepMaterialFixture.CreateAsync(mode, steps, existingSql: entry.Files.Sql,
                executorId: entry.Files.Agent.Id, allowTaskPublication: true, promptCharacters: promptCharacters);
            var services = entry.Files.Sql.Services;
            var produced = await services.GetRequiredService<ProcessDecisionStepPreparationCoordinator>()
                .PrepareAsync(process.State, process.Claim, TimeSpan.FromSeconds(30));
            Assert.True(produced.EnrolledNow);
            var parent = Assert.IsType<ProcessDecisionChildMaterialBinding>(produced.Child);
            var input = await services.GetRequiredService<IProcessAcceptedExecutionInputReader>()
                .ReadAsync(parent.Child.Address.RunId, parent.Child.Address.StepInstanceId);
            ProcessDecisionSingleActivityAdmission.Require(process.State, input, parent.ExecutionContract);
            var assignment = input.Assignment;
            var context = ExecutionInvocationContext.Empty with {
                SourceKind = ProjectTaskPlanProcessContract.SourceKind, SourceId = assignment.StepKey,
                CorrelationId = assignment.RunId.ToString(), CausationId = assignment.StepInstanceId.ToString(),
                RequestedBy = "process-runtime", RequestedByKind = "system",
                ProcessRunId = assignment.RunId.ToString(), ProcessStepId = assignment.StepInstanceId.ToString(),
                MetadataJson = services.GetRequiredService<ProcessExecutionMetadataComposer>()
                    .ComposeClaimedExecution(assignment, new(process.Claim.ClaimToken.Value))
            };
            var envelope = AgentToolProtocolEnvelope.Create(ProcessDecisionChildMaterialBindingCodec.Format,
                ProcessDecisionChildMaterialBindingCodec.Version, ProcessDecisionChildMaterialBindingCodec.Serialize(parent));
            AgentInheritedPlanningWorkspaceRequest request = new(parent.Child.Address.ChildId.Value,
                new(new(parent.Child.BudgetId, parent.Child.AllocationId, parent.Child.Grant), AgentInheritedPlanningStartMode.FreshEnrollment),
                entry.Files.Agent.Id, assignment.Prompt, AgentExecutionOperationId.New(), context, AgentPlanningOwner.ProjectTaskPlan,
                WorkspaceScopeDescriptor.Project(entry.Files.Sql.Project.ProjectId.ToString("D")), envelope, async (run, token) => {
                    Assert.Equal(parent.Child.Address.ChildId.Value, run.Id);
                    var child = await services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(parent.Child.Address, token);
                    Assert.Equal(parent.Child, child!.Admission);
                }, candidate => {
                    var evidence = ProcessExecutionClaimEvidenceReader.Read(candidate, out _)
                        ?? throw new InvalidOperationException("The original claim was not retained.");
                    return AgentToolProtocolEnvelope.Create(ProcessDecisionInitialSourceCodec.Format, ProcessDecisionInitialSourceCodec.Version,
                        ProcessDecisionInitialSourceCodec.Serialize(evidence));
                });
            clock.Freeze();
            entry.Wire.OnRequest = token => {
                clock.Advance(TimeSpan.FromSeconds(1));
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            };
            var waiting = (await services.GetRequiredService<IAgentInheritedPlanningWorkspaceService>()
                .ExecuteInheritedPlanningAsync(request)).Run;
            Assert.True(waiting.State == ExecutionState.WaitingOnTool, waiting.ResultSummary);
            var claim = process.State.Claims.Single(item => item.ClaimToken == process.Claim.ClaimToken);
            var key = Guid.NewGuid();
            StrategyId strategy = new("review-completion-fixture");
            StrategyResultEnvelope result = new(strategy, "1.0.0", key, StrategyOutcome.NeedsManager,
                [], [], [], [], "sha256:" + new string('e', 64)) { ExecutionRunId = new(waiting.Id) };
            var state = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(process.State.RunId))!;
            var blocked = await services.GetRequiredService<ProcessDecisionResultCoordinator>().SubmitAsync(state,
                new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("review-completion-fixture")),
                    new("review-completion-fixture"), clock.GetUtcNow()),
                new(parent.Child.Address.StepInstanceId, claim.OwnerId, claim.ClaimToken, new(key), result));
            Assert.True(blocked.Succeeded, string.Join("; ", blocked.Diagnostics.Select(item => item.Message)));
            Assert.Equal(ProcessDecisionPhase.WaitingForReview, blocked.Decision!.Current.Phase);
            Assert.Equal(DispatchClaimStatus.Completed, blocked.State.Claims.Single().Status);
            var selected = (await services.GetRequiredService<IProcessDecisionResultQuery>().ReadAsync(
                parent.Child.Address.RunId, parent.Child.Address.StepInstanceId, new(waiting.Id)))!;
            return new(entry, process, parent, waiting, blocked.State,
                new(parent.Child.Address, strategy, new(key), new(Guid.NewGuid()), selected.Head!.Expectation,
                    new(parent.Child.Address.StepInstanceId, selected.Budget.BudgetId, selected.Budget.Revision)), clock);
        } catch {
            if (process is not null) {
                await process.DisposeAsync();
            }
            await entry.DisposeAsync();
            throw;
        }
    }

    internal ProcessDecisionReviewCoordinator Coordinator(IProcessRuntimeUnitOfWork? unitOfWork = null,
        IProcessIdempotencyStore? receipts = null) => new(
            Services.GetRequiredService<IProcessAcceptedExecutionInputReader>(), Services.GetRequiredService<IProcessDecisionResultQuery>(),
            Services.GetServices<IProcessDecisionChildOutcomeReader>(), receipts ?? Services.GetRequiredService<IProcessIdempotencyStore>(),
            unitOfWork ?? Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), NullLogger<ProcessDecisionReviewCoordinator>.Instance);

    internal Task<ProcessRuntimeCommitResult> CompleteAsync()
        => Coordinator().CompleteAsync(State, CommandContext, Request);

    internal async Task PublishAsync() {
        var admission = Waiting.ToolAdmission!;
        var binding = ProjectTaskPlanAgentFixture.Binding(Waiting);
        var journal = new AgentToolAdmissionJournal(Entry.Files.Store, admission.Session.Profile, Clock,
            backgroundSources: Services.GetServices<IAgentToolBackgroundSourcePolicy>());
        var owner = new AgentPlanningDecisionOwner(journal);
        var continuation = AgentInheritedSavedEffectContinuation.ForManagerReview(admission.PlanningDecision!.InheritedOrigin!,
            AgentPlanningDecisionExpectation.From(Waiting), binding, async (run, token) => {
                Assert.Equal(Waiting.Id, run.Id);
                var child = await Services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(Parent.Child.Address, token);
                if (child?.Admission != Parent.Child || !child.IsCurrent || child.Closure is not null || child.Settlement is not null ||
                        child.Generation.Phase != ProcessDecisionPhase.WaitingForReview) {
                    throw new AgentToolReceiptAccessDeniedException();
                }
            }, token => {
                token.ThrowIfCancellationRequested();
                if (!ManagerAllowed) {
                    throw new AgentToolReceiptAccessDeniedException();
                }
                return Task.CompletedTask;
            });
        var request = new AgentSavedEffectContinuationRequest(Waiting.Id, null, Waiting.Revision, admission.Revision, binding,
            Waiting.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) {
                ToolAdmission = item.ToolAdmission
            }).ToArray());
        await using var lease = await owner.ResumeInheritedSavedEffectAsync(request, AgentPlanningDecisionExpectation.From(Waiting),
            continuation, (observation, token) => new(Services.GetRequiredService<ProjectTaskPlanSavedPublicationResolver>()
                .CanReadAsync(observation, token)), default);
        using var bound = lease.Bind();
        var dependencies = MafAgentRuntimeDependencies.FromServices(Services);
        var dispatcher = new MafSavedEffectDispatcher(Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            WorkspaceScopeDescriptor.Organization(Entry.Files.Sql.Project.DatabaseProfileId.ToString("N")),
            Services.GetRequiredService<CanDoItAll.Infrastructure.FileSystem.IPhysicalFileSystemPathPolicyFactory>(),
            Services.GetRequiredService<IWorkspaceRuntimeServicesFactory>(), journal, Services.GetRequiredService<IAgentCatalogReadLeaseStore>(),
            Services.GetServices<IAgentRuntimeToolProvider>().ToArray(), Services.GetServices<IAgentToolReceiptReconciliationProvider>().ToArray(),
            dependencies.ToolInvocationPolicyPipeline, dependencies.ToolPolicies, NullLogger<MafSavedEffectDispatcher>.Instance);
        var published = await dispatcher.DispatchAsync(AgentSavedEffectDispatchRequest.ForInherited(admission.Session.Reference, binding), default);
        Assert.True(published.EffectState == AgentToolEffectState.Committed,
            published.OwnerResult is null ? "Saved publication was refused before the Project owner returned a result."
                : $"Project publication refused: {ProjectTaskPlanAgentFixture.Result(published).Refusal}.");
        Assert.Null(ProjectTaskPlanAgentFixture.Result(published).Refusal);
        Clock.Advance(TimeSpan.FromSeconds(2));
        var closed = await owner.SettleTerminalAsync(lease.PlanningWork!, AgentPlanningGenerationState.Completed, default);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, closed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Single(Entry.Wire.Requests);
        await Entry.Files.Sql.VerifyAsync(1, 1);
    }

    public async ValueTask DisposeAsync() {
        try {
            await Process.DisposeAsync();
        } finally {
            await Entry.DisposeAsync();
        }
    }
}
