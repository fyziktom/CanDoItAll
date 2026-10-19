using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    private sealed record HistoricalSeed(Fixture Owner, ExecutionRunRecord Run, AgentToolSessionReference Session,
        FileSandboxWorkspaceStore Store, IReadOnlyList<AgentToolBusinessIntentId> Intents, string RunPath, string CatalogPath,
        IReadOnlyDictionary<Guid, string> ReceiptDigests) {
        internal AgentHistoricalReceiptReviewRequest Request => new(Run.Id, Run.ChatSessionId, Run.AgentId, Run.CreatedAtUtc);
    }

    private static async Task<HistoricalSeed> SeedHistoricalReceiptsAsync(IServiceProvider services, NativeClock clock,
        bool process, bool sharedExecutor = false, int receiptCount = 4, bool hierarchy = false,
        bool createdDefinitions = false, bool unresolvedLast = false) {
        ArgumentOutOfRangeException.ThrowIfLessThan(receiptCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(receiptCount, 5);
        Assert.False(hierarchy && createdDefinitions);
        Assert.False(unresolvedLast && !createdDefinitions);
        var owner = await Fixture.CreateAsync(services);
        var authority = await CaptureProjectOperationsAsync(owner);
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var profile = NativeProfile(services);
        var journal = new AgentToolAdmissionJournal(store, profile, clock, [NativeBackgroundPolicy(services, owner, clock)]);
        ExecutionRunRecord run;
        ChatSessionRecord? chat = null;
        AgentRuntimeToolProviderContext context;
        if (process) {
            var source = Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(authority.Principal);
            var ceiling = source.Ceiling;
            Assert.Equal(ProcessLaunchSourceScopeKind.Organization, ceiling.WorkspaceScopeKind);
            var governance = new AgentExecutionGovernanceSnapshot(new(ceiling.AuthorityId), ceiling.AgentId,
                authority.DatabaseProfileId, new(ceiling.DatabaseProfileGeneration),
                WorkspaceScopeDescriptor.Organization(ceiling.WorkspaceScopeKey), ceiling.ReadAllowed, ceiling.MutationAllowed,
                ceiling.PolicyVersion, ceiling.PolicyFingerprint, ceiling.AllowedOperations, ceiling.AllowedCapabilityKeys,
                ceiling.WritableExternalTargetAliases, ceiling.ReadOnlyExternalTargetAliases, ceiling.AllowedManagedArtifactReadRefs,
                schemaVersion: AgentExecutionAuthorityRecord.CurrentSchemaVersion);
            authority = await owner.Authority.CaptureAgentAsync(owner.Project, governance, source.Operation);
            Assert.Equal(ProcessLaunchAgentCeiling.CurrentSchemaVersion,
                Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(authority.Principal).Ceiling.EffectiveSchemaVersion);
            run = sharedExecutor
                ? await CreateSharedHistoricalProcessAsync(services, owner, clock, authority)
                : await CreateClaimedExecutionAsync(services, owner, clock, persistExecution: false, authority: authority);
            var admission = await journal.CreateForNewBackgroundRunAsync(run, "Retain four exact definition receipts.");
            run = run with { ToolAdmission = admission with {
                SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion, Support = AgentToolAdmissionSupport.EffectJournalOnly
            } };
            context = await ToolProducerContextAsync(owner, run);
        } else {
            var now = clock.Now;
            var agent = (await owner.Writer.LoadCatalogAsync()).Agents.Single(item => item.Id == owner.Agent.Id);
            var provider = (await owner.Writer.LoadCatalogAsync()).Providers.First();
            var projectScope = WorkspaceScopeDescriptor.Project(owner.Project.ProjectId.ToString("D"));
            var originalAuthority = new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), agent.Id, profile.ProfileId,
                profile.Generation, projectScope, true, true, "historical-basis-fixture", "historical-basis-original-authority", now,
                schemaVersion: AgentExecutionAuthorityRecord.CurrentSchemaVersion,
                sourceProjectLifetime: new(owner.Project.DatabaseProfileId, owner.Project.ProjectId, owner.Project.LifetimeId));
            var originalContext = new AgentTurnContextReference(new(Guid.NewGuid()), new(Guid.NewGuid()),
                new(AgentChatTrustedSourceKinds.ProjectStructure), new(owner.Project.ProjectId.ToString("D")),
                "agents", "chat", 1, "historical-basis-original-context", now);
            var runId = Guid.NewGuid();
            chat = new(Guid.NewGuid(), agent.Id, "Original historical review", now, now, [], LatestExecutionRunId: runId);
            run = new(runId, agent.Id, chat.Id, chat.Title, "chat-session", chat.Id.ToString("N"), string.Empty, string.Empty,
                "operator", "user", AgentTurnContextMetadata.Apply("{}", originalContext, originalAuthority),
                "Retain four exact definition receipts.", string.Empty, provider.Name, provider.DefaultModel,
                ExecutionState.Preparing, null, now, now, now, null, string.Empty, null, [], Revision: 1, ProviderProfileId: provider.Id);
            var admission = journal.CreateForNewRun(run, chat);
            run = run with { ToolAdmission = admission with {
                SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion, Support = AgentToolAdmissionSupport.EffectJournalOnly
            } };
            context = new(agent, provider, [], false, AgentRuntimeToolProviderPurpose.InteractiveChat, "historical-basis",
                AgentRuntimeContextIntent.Empty with {
                    Purpose = AgentRuntimeContextPurpose.InteractiveChat,
                    SourceKind = AgentChatTrustedSourceKinds.ProjectStructure, SourceId = owner.Project.ProjectId.ToString("D"),
                    RuntimeToolProvidersEnabled = true, WorkspaceToolsEnabled = false, ToolCapabilitiesEnabled = true
                }, new Dictionary<string, string>()) {
                AdmittedToolSession = run.ToolAdmission.Session.Reference, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
                Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson)
            };
        }
        await store.SaveExecutionRunDetailAsync(new(run, chat, [], []));
        var providerTools = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        var projects = services.GetRequiredService<ProjectsService>();
        List<AgentToolBusinessIntentId> intents = [];
        using (WorkspaceExecutionAuditContext.BeginScope(run)) {
            await using var lease = await journal.AcquireRunAsync(run.ToolAdmission!.Session.Reference, default);
            using var bound = lease.Bind();
            await journal.BeginEffectSegmentAsync(lease, default);
            var tools = await providerTools.CreateToolsAsync(context, default);
            if (hierarchy || createdDefinitions) {
                Assert.Equal(5, receiptCount);
                intents.AddRange(await SeedHistoricalCreatedTargetOperationsAsync(services, owner, journal, lease, providerTools, context, tools,
                    process, createdDefinitions, unresolvedLast));
            } else {
                var metadata = Assert.Single(providerTools.GetToolMetadata(context), item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate);
                var tool = Assert.Single(tools.OfType<AIFunction>(), item => item.Name == metadata.ToolName);
                for (var index = 0; index < receiptCount; index++) {
                    var original = (await projects.GetDefinitionAsync(owner.Project.ProjectId)).Value!;
                    var request = new ProjectStructureProjectSaveRequest($"Historical accepted definition {index}", "", "", "") {
                        ExpectedDefinition = original.Expectation
                    };
                    Dictionary<string, object?> arguments = new() { ["projectId"] = owner.Project.ProjectId, ["request"] = request };
                    var payload = metadata.PrepareAdmission!(JsonSerializer.SerializeToElement(arguments, ProjectDefinitionToolProposalCodec.Json));
                    var callId = $"historical-definition-{index}";
                    var saved = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                        [new(callId, payload, false)], default);
                    var batch = saved.Batches[^1];
                    var claim = await journal.ClaimInvocationAsync(lease, batch.Id, callId, payload, default);
                    using var dispatch = claim.Bind();
                    using var effect = AgentToolInvocationEffectScope.Begin();
                    var accepted = Assert.IsType<JsonElement>(await tool.InvokeAsync(new AIFunctionArguments(arguments)));
                    Assert.Equal(new AgentToolCommittedEffect(ProjectDefinitionToolAdmission.EffectSourceKind, claim.Proposal.IntentId.Value.ToString("D")),
                        effect.CommittedEffect);
                    await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope(accepted.GetRawText()),
                        AgentToolEffectState.Committed, default);
                    intents.Add(claim.Proposal.IntentId);
                }
            }
        }
        run = (await store.GetExecutionRunAsync(run.Id))!;
        Assert.Equal(receiptCount, run.ToolAdmission!.Batches.Length);
        var proposals = run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals).ToArray();
        Assert.All(unresolvedLast ? proposals[..^1] : proposals, proposal => {
            Assert.Equal(AgentToolProposalState.Completed, proposal.State);
            Assert.NotNull(proposal.Result);
        });
        if (unresolvedLast) {
            Assert.Equal(AgentToolProposalState.Executing, proposals[^1].State);
            Assert.Null(proposals[^1].Result);
        }
        var root = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Storage.WorkspaceRoot;
        var layout = new FileSandboxWorkspaceStorageLayout(root, WorkspaceScopeDescriptor.Organization(profile.ProfileId.ToString("N")));
        var receipts = await HistoricalReceiptDigestsAsync(services, intents);
        Assert.Equal(receiptCount, receipts.Count);
        return new(owner, run, run.ToolAdmission.Session.Reference, store, intents, layout.RunPath(run.Id), layout.CatalogPath, receipts);
    }

    private static async Task<ExecutionRunRecord> CreateSharedHistoricalProcessAsync(IServiceProvider services, Fixture fixture,
        NativeClock clock, ProcessLaunchAuthority authority) {
        var preparation = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()));
        var initial = preparation.InitialCommit;
        var assignment = Assert.Single(initial.InitialAssignments!) with {
            ExecutorId = fixture.Agent.Id.ToString("D"),
            AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction],
            LaunchVariables = new Dictionary<string, string> { [ProcessRuntimeLaunchVariables.ProjectId] = fixture.Project.ProjectId.ToString("D") }
        };
        preparation = preparation with {
            InitialCommit = initial with { InitialAssignments = [assignment] },
            Review = preparation.Review with { Steps = preparation.Review.Steps.Select(step => step with {
                ExecutorId = assignment.ExecutorId
            }).ToArray() }
        };
        var saved = await fixture.Store.PrepareAsync(preparation);
        await using (var context = fixture.Context()) {
            Assert.True((await fixture.UnitOfWork(context).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
        }
        var claim = Guid.NewGuid();
        await using (var context = fixture.Context()) {
            var state = await context.RuntimeStates.SingleAsync(item => item.RunId == assignment.RunId.Value);
            var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == assignment.RunId.Value);
            state.Status = ProcessRuntimeStatus.Active;
            step.Status = ProcessRuntimeStepStatus.Running;
            step.AttemptNumber = 1;
            step.ActiveClaimToken = claim;
            context.DispatchClaims.Add(new() {
                RunId = assignment.RunId.Value, StepInstanceId = assignment.StepInstanceId.Value,
                ClaimToken = claim, OwnerId = "historical-shared-executor-fixture", Status = DispatchClaimStatus.Claimed, AttemptNumber = 1,
                CreatedAtUtc = clock.Now.AddSeconds(-1), ExpiresAtUtc = clock.Now.AddHours(1)
            });
            await context.SaveChangesAsync();
        }
        return new(Guid.NewGuid(), fixture.Agent.Id, null, "Shared historical Process executor", "process-step", assignment.StepKey,
            assignment.RunId.ToString(), assignment.StepInstanceId.ToString(), "process-runtime", "system",
            JsonSerializer.Serialize(new Dictionary<string, string> { ["agentProcessDispatchClaimIdentity"] = claim.ToString("D") }),
            "Execute.", "", "fixture", "fixture", ExecutionState.Preparing, null, clock.Now, clock.Now, clock.Now, null, "", null, [],
            ProcessRunId: assignment.RunId.ToString(), ProcessStepId: assignment.StepInstanceId.ToString());
    }

    private sealed class HistoricalPage {
        private readonly AgentHistoricalReceiptReviewService service;
        internal AgentHistoricalCatalogBasis Basis { get; private set; } = null!;
        internal HistoricalReceiptProbe Receipts { get; private set; } = null!;
        internal HistoricalSqlProbe Sql { get; } = new();
        internal ConcurrentQueue<long> CatalogBytes { get; } = new();

        internal HistoricalPage(IServiceProvider services,
            Func<HistoricalPage, AgentToolHistoricalReceiptReadClaim, CancellationToken, ValueTask>? afterReceipt = null) {
            var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
            var generations = services.GetRequiredService<IAgentExecutionProfileGenerationSource>();
            var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
            var receiptReader = new ProjectDefinitionReceiptReader(
                new Factory<ProjectsDbContext>(NativeOptions<ProjectsDbContext>(services, Sql), static options => new(options)),
                services.GetRequiredService<DatasetActivationService>(), admissions,
                services.GetRequiredService<ILogger<ProjectDefinitionReceiptReader>>());
            var original = services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>();
            service = new(database.Profile, services.GetRequiredService<IDatabaseRuntimeState>(),
                services.GetRequiredService<IOptions<StorageOptions>>(), services.GetRequiredService<IHostEnvironment>(),
                services.GetServices<IAgentHistoricalReceiptSourceAuthorityProvider>(), basis => {
                    Basis = basis;
                    var process = new ProjectProcessHistoricalReadAuthority(original, database, generations, basis, admissions,
                        services.GetRequiredService<IOptionsMonitor<ApiAccessOptions>>(), services.GetRequiredService<TimeProvider>());
                    Receipts = new(new ProjectDefinitionHistoricalReceiptRecovery(basis, process, admissions, receiptReader, services.GetRequiredService<ProjectCreatedTargetProvenance>()),
                        afterReceipt is null ? null : (claim, token) => afterReceipt(this, claim, token));
                    return new([new ProcessHistoricalReceiptSourcePolicy(original, database, generations, process)], [Receipts]);
                });
        }

        internal async Task<AgentHistoricalReceiptReview> ReadAsync(HistoricalSeed seed, CancellationToken cancellationToken = default) {
            CatalogBytes.Clear();
            Sql.Reset();
            using var root = new Activity("historical-basis-composed-page")
                .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded).Start();
            using var listener = new ActivityListener {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => {
                    if (activity.TraceId == root.TraceId && activity.OperationName == "workspace.historical.read" &&
                        activity.GetTagItem("payload.type") is "HistoricalCatalog") {
                        CatalogBytes.Enqueue(Assert.IsType<long>(activity.GetTagItem("payload.bytes")));
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
            return await service.ReadSavedEffectReceiptsAsync(seed.Request, cancellationToken);
        }
    }

    private sealed class HistoricalReceiptProbe(IAgentToolHistoricalReceiptReadProvider inner,
        Func<AgentToolHistoricalReceiptReadClaim, CancellationToken, ValueTask>? after) : IAgentToolHistoricalReceiptReadProvider {
        internal List<AgentToolHistoricalReceiptReadClaim> Claims { get; } = [];
        internal List<AgentToolHistoricalReceiptObservation> Observations { get; } = [];
        public bool Supports(string toolName) => inner.Supports(toolName);
        public async ValueTask<AgentToolHistoricalReceiptObservation> ReadAsync(AgentToolHistoricalReceiptReadClaim claim,
            CancellationToken cancellationToken = default) {
            Claims.Add(claim);
            var observed = await inner.ReadAsync(claim, cancellationToken);
            Observations.Add(observed);
            if (after is not null) {
                await after(claim, cancellationToken);
            }
            return observed;
        }
    }

    private sealed class HistoricalSqlProbe : DbCommandInterceptor {
        internal int FinalTargets { get; private set; }
        internal Func<CancellationToken, Task>? BeforeFinalTarget { get; set; }
        internal void Reset() => FinalTargets = 0;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("AS \"TargetAvailable\"", StringComparison.Ordinal) &&
                command.CommandText.Contains("Infrastructure_DatasetLifecycle", StringComparison.Ordinal) &&
                !command.CommandText.Contains("AcceptedPayload", StringComparison.Ordinal)) {
                Assert.Contains("Projects_ProjectRetirements", command.CommandText, StringComparison.Ordinal);
                Assert.Contains("LifetimeId", command.CommandText, StringComparison.Ordinal);
                Assert.NotNull(command.Connection);
                FinalTargets++;
                if (BeforeFinalTarget is { } before) {
                    await before(cancellationToken);
                }
            }
            return result;
        }
    }

    private static async Task<string> HistoricalFileDigestAsync(string path)
        => Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));

    private static async Task<IReadOnlyDictionary<Guid, string>> HistoricalReceiptDigestsAsync(IServiceProvider services,
        IReadOnlyList<AgentToolBusinessIntentId> intents) {
        var ids = intents.Select(intent => intent.Value).ToArray();
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var rows = await database.Set<ProjectDefinitionReceiptRecord>().AsNoTracking()
            .Where(row => ids.Contains(row.OperationId)).Select(row => new { row.OperationId, row.AcceptedPayload }).ToArrayAsync();
        var lifecycle = await database.Set<ProjectLifecycleReceiptRecord>().AsNoTracking()
            .Where(row => ids.Contains(row.OperationId)).Select(row => new { row.OperationId, row.AcceptedPayload }).ToArrayAsync();
        return rows.Concat(lifecycle).ToDictionary(row => row.OperationId, row => Convert.ToHexString(SHA256.HashData(row.AcceptedPayload)));
    }

    private static async Task AssertHistoricalReceiptsUnchangedAsync(IServiceProvider services, HistoricalSeed seed) {
        var current = await HistoricalReceiptDigestsAsync(services, seed.Intents);
        Assert.Equal(seed.ReceiptDigests.OrderBy(item => item.Key), current.OrderBy(item => item.Key));
    }

    private static void AssertHistoricalAccepted(HistoricalSeed seed, HistoricalPage page, AgentHistoricalReceiptReview result, int catalogReads) {
        Assert.Equal(seed.Request, result.Request);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, result.Stop);
        Assert.Null(result.NextCursor);
        Assert.Equal(4, result.Observations.Count);
        Assert.All(result.Observations, observation => Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, observation.Knowledge));
        Assert.Equal(seed.Intents, result.Observations.Select(item => item.Binding.IntentId));
        Assert.Equal(4, page.Receipts.Claims.Count);
        Assert.Equal(4, page.Sql.FinalTargets);
        Assert.Equal(catalogReads, page.CatalogBytes.Count);
        Assert.All(page.CatalogBytes, bytes => Assert.True(bytes > 0));
        Assert.Throws<InvalidOperationException>(() => page.Basis.RequireCurrent());
    }
}
