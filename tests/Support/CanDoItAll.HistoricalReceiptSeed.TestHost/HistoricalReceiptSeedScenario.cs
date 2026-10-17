using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Tests.HistoricalReceipts;

internal static class HistoricalReceiptSeedScenario {
    internal static async Task<HistoricalReceiptSeeded> CreateAsync(IServiceProvider services, HistoricalReceiptSeedKind kind,
        Guid nonce, DateTimeOffset processStarted, CancellationToken cancellationToken) {
        var database = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var state = services.GetRequiredService<IDatabaseRuntimeState>() as DatabaseRuntimeState
            ?? throw new InvalidOperationException("The seed requires the actual runtime generation owner.");
        state.PublishRestartObserved(state.GetSnapshot(), database.Profile);
        var generation = services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration();
        Require(generation.Value > 0, "The original child generation must be nonzero.");
        var profile = new AgentToolProfileBinding(database.Profile.Profile.Id, database.Profile.Profile.Runtime.Fingerprint, generation);
        var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>() as FileSandboxWorkspaceStore
            ?? throw new InvalidOperationException("The seed requires the canonical file workspace owner.");
        var projects = services.GetRequiredService<ProjectsService>();
        var projectId = Guid.NewGuid();
        Require((await projects.CreateAsync(projectId, new() { Name = "Historical restart target" }, cancellationToken)).IsSuccess,
            "The original target could not be created.");
        var admission = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException("The original project lifetime is unavailable.");
        var initialCatalog = await store.LoadCatalogAsync(cancellationToken);
        var sourceAgent = initialCatalog.Agents.First() with {
            Id = Guid.NewGuid(), Name = "Historical restart source", IsTemplate = false, TemplateKey = string.Empty, Tags = [],
            Status = AgentLifecycleStatus.Active, Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write("{}", new() {
                CanRead = true, CanWrite = true, CanCreateProjects = true, CanCreateSubprojects = true,
                AllowedProjectIds = [projectId], AllowedProjectLifetimes = [new(admission.DatabaseProfileId, projectId, admission.LifetimeId)]
            })
        };
        await store.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, sourceAgent] }, cancellationToken);
        var authority = await services.GetRequiredService<IAgentExecutionAuthorityResolver>().ResolveAsync(new(sourceAgent.Id,
            new(AgentChatTrustedSourceKinds.ProjectStructure), new(projectId.ToString("D")),
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")), generation, null) {
            ObservedProjectLifetime = new(admission.DatabaseProfileId, projectId, admission.LifetimeId)
        }, cancellationToken);
        Require(authority.ReadAllowed && authority.MutationAllowed && authority.DatabaseProfileGeneration == generation,
            "Canonical original project authority was not admitted.");
        var governance = AgentExecutionGovernanceSnapshot.FromAuthority(authority);
        var journal = new AgentToolAdmissionJournal(store, profile,
            backgroundSources: services.GetServices<IAgentToolBackgroundSourcePolicy>());
        var provider = (await store.LoadCatalogAsync(cancellationToken)).Providers.First();
        ExecutionRunRecord run;
        ChatSessionRecord? chat = null;
        AgentDefinition executor;
        if (kind == HistoricalReceiptSeedKind.Process) {
            var launchAuthority = await services.GetRequiredService<ProjectProcessLaunchAuthorityService>().CaptureAgentAsync(
                new(admission.DatabaseProfileId, projectId, admission.LifetimeId), governance,
                ProcessLaunchAgentOperation.StructureStart, cancellationToken);
            (run, executor) = await CreateClaimedProcessAsync(services, store, sourceAgent, launchAuthority, cancellationToken);
            var admitted = await journal.CreateForNewBackgroundRunAsync(run, run.InputSummary, cancellationToken: cancellationToken);
            run = run with { ToolAdmission = admitted with {
                SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion, Support = AgentToolAdmissionSupport.EffectJournalOnly
            } };
        } else {
            executor = sourceAgent;
            var now = DateTimeOffset.UtcNow;
            var runId = Guid.NewGuid();
            var source = new AgentTurnContextReference(new(Guid.NewGuid()), new(Guid.NewGuid()),
                new(AgentChatTrustedSourceKinds.ProjectStructure), new(projectId.ToString("D")),
                "agents", "chat", 1, "historical-restart-original-context", now);
            chat = new(Guid.NewGuid(), executor.Id, "Historical restart chat", now, now, [], LatestExecutionRunId: runId);
            run = new(runId, executor.Id, chat.Id, chat.Title, "chat-session", chat.Id.ToString("N"), "", "", "operator", "user",
                AgentTurnContextMetadata.Apply("{}", source, authority), "Retain five original owner effects.", "", provider.Name,
                provider.DefaultModel, ExecutionState.Preparing, null, now, now, now, null, "", null, [], Revision: 1, ProviderProfileId: provider.Id);
            run = run with { ToolAdmission = journal.CreateForNewRun(run, chat) with {
                SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion, Support = AgentToolAdmissionSupport.EffectJournalOnly
            } };
        }
        await store.SaveExecutionRunDetailAsync(new(run, chat, [], []), cancellationToken);
        var context = CreateContext(kind, executor, provider, run, projectId);
        var toolProvider = services.GetServices<IAgentRuntimeToolProvider>()
            .Single(item => item.Descriptor?.ProviderKey == HistoricalReceiptSeedProtocol.ToolProviderKey);
        var json = services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        List<Guid> operationIds = [];
        using (WorkspaceExecutionAuditContext.BeginScope(run)) {
            await using var lease = await journal.AcquireRunAsync(run.ToolAdmission!.Session.Reference, cancellationToken);
            using var bound = lease.Bind();
            await journal.BeginEffectSegmentAsync(lease, cancellationToken);
            var tools = await toolProvider.CreateToolsAsync(context, cancellationToken);
            var metadata = toolProvider.GetToolMetadata(context).Single(item => item.ToolName == ProjectStructureToolPolicy.ProjectStructureProjectUpdate);
            var tool = tools.OfType<AIFunction>().Single(item => item.Name == metadata.ToolName);
            for (var index = 0; index < 5; index++) {
                var original = (await projects.GetDefinitionAsync(projectId, cancellationToken)).Value
                    ?? throw new InvalidOperationException("The target definition is unavailable.");
                var update = new ProjectStructureProjectSaveRequest($"Original accepted restart effect {index}", "", "", "") {
                    ExpectedDefinition = original.Expectation
                };
                Dictionary<string, object?> arguments = new() { ["projectId"] = projectId, ["request"] = update };
                var payload = (metadata.PrepareAdmission ?? throw new InvalidOperationException("The tool has no durable admission codec."))(
                    JsonSerializer.SerializeToElement(arguments, json));
                var callId = $"historical-restart-effect-{index}";
                var prepared = await journal.AdmitBatchAsync(lease, payload.Digest, Envelope("{}"), [new(callId, payload, false)], cancellationToken);
                var claim = await journal.ClaimInvocationAsync(lease, prepared.Batches[^1].Id, callId, payload, cancellationToken);
                using var dispatch = claim.Bind();
                using var effect = AgentToolInvocationEffectScope.Begin();
                var result = await tool.InvokeAsync(new AIFunctionArguments(arguments), cancellationToken);
                Require(result is JsonElement && effect.CommittedEffect?.SourceId == claim.Proposal.IntentId.Value.ToString("D"),
                    "The actual tool did not acknowledge its original owner effect.");
                if (index < 4) {
                    await journal.CompleteInvocationAsync(claim, Envelope(((JsonElement)result!).GetRawText()),
                        AgentToolEffectState.Committed, cancellationToken);
                }
                operationIds.Add(claim.Proposal.IntentId.Value);
            }
        }
        var retained = await store.GetExecutionRunAsync(run.Id, cancellationToken)
            ?? throw new InvalidOperationException("The original execution was not retained.");
        var proposals = retained.ToolAdmission!.Batches.SelectMany(batch => batch.Proposals).ToArray();
        Require(proposals.Length == 5 && proposals.Take(4).All(item => item.State == AgentToolProposalState.Completed && item.Result is not null) &&
            proposals[4].State == AgentToolProposalState.Executing && proposals[4].Result is null,
            "The saved execution must retain four results and one unresolved accepted effect.");
        await using var owner = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync(cancellationToken);
        var receipts = await owner.Set<ProjectDefinitionReceiptRecord>().AsNoTracking()
            .Where(item => operationIds.Contains(item.OperationId)).ToArrayAsync(cancellationToken);
        Require(receipts.Length == 5 && receipts.All(item => item.ProjectId == projectId && item.LifetimeId == admission.LifetimeId),
            "The owner did not retain five exact target-lifetime receipts.");
        var dataset = receipts.Select(item => item.NamespaceId).Distinct().Single();
        var byId = receipts.ToDictionary(item => item.OperationId);
        var historical = new FileSandboxWorkspaceHistoricalReadStore(database.Profile.Profile.Storage.WorkspaceRoot,
            WorkspaceScopeDescriptor.Organization(profile.ProfileId.ToString("N")));
        var observed = await historical.ReadHistoricalRunAsync(run.Id, cancellationToken);
        Require(observed.Status == AgentHistoricalStorageReadStatus.Ready && observed.Snapshot is not null,
            "The seed must leave a complete read-only historical snapshot.");
        var runHash = observed.Snapshot!.FileDigest.Value.ToUpperInvariant();
        return new(HistoricalReceiptSeedProtocol.Version, nonce, Environment.ProcessId, processStarted, kind, profile.ProfileId,
            generation.Value, dataset, projectId, admission.LifetimeId, sourceAgent.Id,
            kind == HistoricalReceiptSeedKind.Process ? Guid.Parse(retained.ProcessRunId!) : null,
            kind == HistoricalReceiptSeedKind.Process ? Guid.Parse(retained.ProcessStepId!) : null,
            new(run.Id, run.ChatSessionId, run.AgentId, run.CreatedAtUtc), runHash,
            operationIds.Select(id => new HistoricalReceiptDigest(id, Convert.ToHexString(SHA256.HashData(byId[id].AcceptedPayload)))).ToArray());
    }

    private static AgentRuntimeToolProviderContext CreateContext(HistoricalReceiptSeedKind kind, AgentDefinition executor,
        ProviderProfile provider, ExecutionRunRecord run, Guid projectId) {
        var process = kind == HistoricalReceiptSeedKind.Process;
        return new(executor, provider, [], false, process ? AgentRuntimeToolProviderPurpose.GovernedProcessAutomation :
            AgentRuntimeToolProviderPurpose.InteractiveChat, "historical-restart-seed", AgentRuntimeContextIntent.Empty with {
                Purpose = process ? AgentRuntimeContextPurpose.GovernedProcessAutomation : AgentRuntimeContextPurpose.InteractiveChat,
                SourceKind = process ? run.SourceKind : AgentChatTrustedSourceKinds.ProjectStructure,
                SourceId = process ? run.SourceId : projectId.ToString("D"), ProcessRunId = run.ProcessRunId, ProcessStepId = run.ProcessStepId,
                IsGovernedProcessStep = process, AllowsProductMutation = true, RuntimeToolProvidersEnabled = true,
                WorkspaceToolsEnabled = false, ToolCapabilitiesEnabled = true,
                AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction]
            }, new Dictionary<string, string>()) {
            AdmittedToolSession = run.ToolAdmission!.Session.Reference, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson)
        };
    }

    private static async Task<(ExecutionRunRecord Run, AgentDefinition Executor)> CreateClaimedProcessAsync(IServiceProvider services,
        FileSandboxWorkspaceStore workspace, AgentDefinition source, ProcessLaunchAuthority authority, CancellationToken cancellationToken) {
        var preparation = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()));
        var initial = preparation.InitialCommit;
        var assignment = initial.InitialAssignments!.Single() with {
            AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction],
            LaunchVariables = new Dictionary<string, string> { [ProcessRuntimeLaunchVariables.ProjectId] = authority.ProjectAdmission!.ProjectId.ToString("D") }
        };
        var executor = source with { Id = Guid.Parse(assignment.ExecutorId), Name = "Historical restart Process executor", ConfigurationJson = "{}" };
        await workspace.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, executor] }, cancellationToken);
        preparation = preparation with { InitialCommit = initial with { InitialAssignments = [assignment] } };
        var saved = await services.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(preparation, cancellationToken);
        var committed = await services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(ProcessPreparedLaunchFixture.Commit(saved), cancellationToken);
        Require(committed.Succeeded, "The actual Process owner refused the prepared initial run.");
        var claimId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using (var context = await services.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync(cancellationToken)) {
            var state = await context.RuntimeStates.SingleAsync(item => item.RunId == assignment.RunId.Value, cancellationToken);
            var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == assignment.RunId.Value, cancellationToken);
            state.Status = ProcessRuntimeStatus.Active;
            step.Status = ProcessRuntimeStepStatus.Running;
            step.AttemptNumber = 1;
            step.ActiveClaimToken = claimId;
            context.DispatchClaims.Add(new() {
                RunId = assignment.RunId.Value, StepInstanceId = assignment.StepInstanceId.Value, ClaimToken = claimId,
                OwnerId = "historical-restart-seed", Status = DispatchClaimStatus.Claimed, AttemptNumber = 1,
                CreatedAtUtc = now.AddSeconds(-1), ExpiresAtUtc = now.AddHours(1)
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        var metadata = JsonSerializer.Serialize(new Dictionary<string, string> { [HistoricalReceiptSeedProtocol.ProcessClaimMetadataKey] = claimId.ToString("D") });
        return (new(Guid.NewGuid(), executor.Id, null, "Historical restart Process", "process-step", assignment.StepKey,
            assignment.RunId.ToString(), assignment.StepInstanceId.ToString(), "process-runtime", "system", metadata,
            "Retain five original owner effects.", "", "fixture", "fixture", ExecutionState.Preparing, null,
            now, now, now, null, "", null, [], ProcessRunId: assignment.RunId.ToString(), ProcessStepId: assignment.StepInstanceId.ToString()), executor);
    }

    private static AgentToolProtocolEnvelope Envelope(string json) => AgentToolProtocolEnvelope.Create("historical-seed-sdk", 1, json);

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }
    }
}
