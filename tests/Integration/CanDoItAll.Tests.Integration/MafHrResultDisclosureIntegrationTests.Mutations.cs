using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafHrResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(HrAgentToolPolicy.HrAgentCreate, "agent-catalog")]
    [InlineData(HrAgentToolPolicy.HrAgentSettingsUpdate, "agent-catalog")]
    [InlineData(HrAgentToolPolicy.HrCrmPartyCreate, "crm-party")]
    [InlineData(HrAgentToolPolicy.HrCrmAffiliationUpsert, "crm-affiliation")]
    public async Task Actual_HR_owner_acknowledgement_survives_approval_and_independent_journal_replay(
        string toolName, string effectSourceKind) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)), managedHr: true);
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var canonical = (await workspace.ListAgentsAsync(includeTemplates: true)).Single(item => item.Id == HrAgentIdentity.AgentId);
        var agent = canonical with {
            ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            ConfigurationJson = AgentThinkingEffortPolicy.WriteAgentOverride(canonical.ConfigurationJson, null)
        };
        var capabilities = (await workspace.ListCapabilitiesAsync()).Where(item => item.Kind == CapabilityKind.Tool).ToArray();
        var available = capabilities.Select(item => item.Id).ToHashSet();
        agent = agent with { Capabilities = agent.Capabilities.Where(item => available.Contains(item.CapabilityId)).ToArray() };
        Assert.True(HrAgentRuntimeAuthorizationPolicy.IsToolAuthorized(agent, capabilities, toolName,
            requiresCrmScope: toolName is HrAgentToolPolicy.HrCrmPartyCreate or HrAgentToolPolicy.HrCrmAffiliationUpsert));
        var name = $"HR acknowledged target {Guid.NewGuid():N}";
        var prepared = await PrepareMutationAsync(services, toolName, name);
        var before = await ReadMutationOwnerAsync(services, toolName, name, prepared.PersonId);
        var initial = new ScriptClient(toolName, prepared.Request);
        var pending = await ExecuteAsync(fixture, services, agent, capabilities, initial);
        Assert.Single(pending.PendingApprovals);
        Assert.Equal(1, initial.Requests);
        Assert.Equal(before, await ReadMutationOwnerAsync(services, toolName, name, prepared.PersonId));
        await fixture.ApproveAsync(pending.PendingApprovals);

        var afterEffect = new ScriptClient(toolName, prepared.Request, stopAfterResult: true);
        var interrupted = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, capabilities, afterEffect));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(interrupted,
            "Fixture stops after the durable HR result and before the next response.");
        Assert.Null(afterEffect.ResultErrorCode);
        Assert.Equal(1, afterEffect.Requests);
        var original = Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!
            .ToolAdmission!.Batches.SelectMany(batch => batch.Proposals));
        Assert.Equal(AgentToolProposalState.Completed, original.State);
        Assert.Equal(AgentToolEffectState.Committed, original.EffectState);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, original.Payload.Recovery);
        Assert.Equal(ExecutionApprovalStatus.Approved, original.ApprovalStatus);
        Assert.Equal(original.Payload.Digest, original.ApprovedDigest);
        var acknowledged = Assert.IsType<MutationOwnerState>(await ReadMutationOwnerAsync(services, toolName, name, prepared.PersonId));
        Assert.NotEqual(before, acknowledged);
        Assert.Contains(acknowledged.Id.ToString("D"), original.Result!.PayloadJson, StringComparison.Ordinal);

        var restarted = new ScriptClient(toolName, prepared.Request);
        var completed = await ExecuteAsync(fixture, services, agent, capabilities, restarted);
        Assert.Equal("completed", completed.ResponseText);
        Assert.Empty(completed.PendingApprovals);
        Assert.Equal(1, restarted.Requests);
        AssertAcknowledgedTrace(completed, effectSourceKind, acknowledged.Id);
        var replay = new ScriptClient(toolName, prepared.Request);
        var replayed = await ExecuteAsync(fixture, services, agent, capabilities, replay);
        Assert.Equal("completed", replayed.ResponseText);
        Assert.Equal(0, replay.Requests);
        AssertAcknowledgedTrace(replayed, effectSourceKind, acknowledged.Id);
        Assert.Equal(acknowledged, await ReadMutationOwnerAsync(services, toolName, name, prepared.PersonId));
        var retained = Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!
            .ToolAdmission!.Batches.SelectMany(batch => batch.Proposals));
        Assert.Equal(original, retained);
    }

    [Theory]
    [InlineData(HrAgentToolPolicy.HrCrmPartyCreate)]
    [InlineData(HrAgentToolPolicy.HrCrmAffiliationUpsert)]
    public async Task A_denied_CRM_mutation_never_reaches_the_owner_and_a_separately_approved_proposal_writes_exactly_once(string toolName) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var canonical = (await workspace.ListAgentsAsync(includeTemplates: true)).Single(item => item.Id == HrAgentIdentity.AgentId);
        var agent = canonical with {
            ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            ConfigurationJson = AgentThinkingEffortPolicy.WriteAgentOverride(canonical.ConfigurationJson, null)
        };
        var capabilities = (await workspace.ListCapabilitiesAsync()).Where(item => item.Kind == CapabilityKind.Tool).ToArray();
        var available = capabilities.Select(item => item.Id).ToHashSet();
        agent = agent with { Capabilities = agent.Capabilities.Where(item => available.Contains(item.CapabilityId)).ToArray() };
        var name = $"HR denied target {Guid.NewGuid():N}";
        var prepared = await PrepareMutationAsync(services, toolName, name);
        var before = await ReadMutationOwnerAsync(services, toolName, name, prepared.PersonId);

        // First admitted run: the operator denies the proposal.
        await using (var deniedRun = await AgentToolAdmissionJournalFixture.CreateAsync(
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)), managedHr: true)) {
            var proposed = new ScriptClient(toolName, prepared.Request);
            var pending = await ExecuteAsync(deniedRun, services, agent, capabilities, proposed);
            Assert.Single(pending.PendingApprovals);
            Assert.Equal(before, await ReadMutationOwnerAsync(services, toolName, name, prepared.PersonId));
            await DenyAsync(deniedRun, pending.PendingApprovals);

            var resumed = new ScriptClient(toolName, prepared.Request);
            await Record.ExceptionAsync(() => ExecuteAsync(deniedRun, services, agent, capabilities, resumed));
            var rejected = Assert.Single((await deniedRun.NewStore().GetExecutionRunAsync(deniedRun.Session.ExecutionRunId))!
                .ToolAdmission!.Batches.SelectMany(batch => batch.Proposals));
            Assert.Equal(ExecutionApprovalStatus.Rejected, rejected.ApprovalStatus);
            Assert.Equal(AgentToolProposalState.Rejected, rejected.State);
            Assert.NotEqual(AgentToolEffectState.Committed, rejected.EffectState);
            Assert.Null(rejected.ApprovedDigest);
            Assert.Null(rejected.DispatchClaimId);
            // The owner was never called: its state is byte for byte what it was before the proposal.
            Assert.Equal(before, await ReadMutationOwnerAsync(services, toolName, name, prepared.PersonId));
        }

        // A separate admitted run with its own proposal is approved: exactly one canonical write.
        await using var approvedRun = await AgentToolAdmissionJournalFixture.CreateAsync(
            profileBinding: new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)), managedHr: true);
        var second = new ScriptClient(toolName, prepared.Request);
        var secondPending = await ExecuteAsync(approvedRun, services, agent, capabilities, second);
        Assert.Single(secondPending.PendingApprovals);
        await approvedRun.ApproveAsync(secondPending.PendingApprovals);
        var completed = await ExecuteAsync(approvedRun, services, agent, capabilities, new ScriptClient(toolName, prepared.Request));
        Assert.Equal("completed", completed.ResponseText);
        var written = Assert.IsType<MutationOwnerState>(await ReadMutationOwnerAsync(services, toolName, name, prepared.PersonId));
        Assert.NotEqual(before, written);
        var committed = Assert.Single((await approvedRun.NewStore().GetExecutionRunAsync(approvedRun.Session.ExecutionRunId))!
            .ToolAdmission!.Batches.SelectMany(batch => batch.Proposals));
        Assert.Equal(AgentToolEffectState.Committed, committed.EffectState);
        Assert.Contains(written.Id.ToString("D"), committed.Result!.PayloadJson, StringComparison.Ordinal);
        // ReadMutationOwnerAsync uses SingleOrDefault over the unique name: a duplicate write would have thrown.
    }

    private static async Task DenyAsync(AgentToolAdmissionJournalFixture fixture, IReadOnlyList<PendingToolApprovalRecord> pending) {
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore()).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId, current => {
            var decisions = pending.Select(item => new PendingToolApprovalDecision(item.ApprovalId, false) { ToolAdmission = item.ToolAdmission }).ToArray();
            var journal = AgentToolJournalTransitions.ApplyDecisions(current.Run.ToolAdmission!, pending, decisions, false);
            return current with { Run = current.Run with {
                ToolAdmission = journal, PendingApprovals = [], State = ExecutionState.Running,
                Revision = current.Run.Revision + 1, UpdatedAtUtc = DateTimeOffset.UtcNow
            } };
        });
    }

    private static void AssertAcknowledgedTrace(AgentRuntimeResponse response, string kind, Guid id) {
        var trace = Assert.Single(response.ToolInvocationTraces);
        Assert.True(trace.Succeeded);
        Assert.Equal(AgentToolInvocationOutcome.Succeeded, trace.Outcome);
        Assert.Equal(AgentToolEffectState.Committed, trace.EffectState);
        Assert.Equal(kind, trace.EffectSourceKind);
        Assert.Equal(id.ToString("D"), trace.EffectSourceId);
        Assert.Empty(trace.FailureCode);
        Assert.Empty(trace.FailureMessage);
        Assert.False(trace.CanRetryWithCorrectedInput);
        var completion = AgentToolCompletionAssessment.Create(response.ToolInvocationTraces, 0, portableOutputValid: true);
        Assert.Equal(ExecutionState.Completed, completion.State);
        Assert.Empty(completion.FailureSummary);
    }

    private static async Task<(object Request, Guid? PersonId)> PrepareMutationAsync(IServiceProvider services, string toolName, string name) {
        var administration = services.GetRequiredService<HrAgentAdministrationService>();
        var commands = services.GetRequiredService<ICrmPartyCommandService>();
        switch (toolName) {
            case HrAgentToolPolicy.HrAgentCreate:
                return (new HrAgentCreateInput(name, "Test role", "Original summary", "Follow explicit operator requests."), null);
            case HrAgentToolPolicy.HrAgentSettingsUpdate:
                var target = await administration.CreateAsync(HrAgentIdentity.AgentId,
                    new(name, "Test role", "Original summary", "Follow explicit operator requests."), default);
                return (new HrAgentSettingsUpdateInput(target.AgentId, target.UpdatedAtUtc, Summary: "Acknowledged update"), null);
            case HrAgentToolPolicy.HrCrmPartyCreate:
                return (new CrmPartyCreateCommand(PartyType.Person, name), null);
            case HrAgentToolPolicy.HrCrmAffiliationUpsert:
                var person = await commands.CreatePartyAsync(new(PartyType.Person, name), "hr-acknowledgement-fixture");
                var organization = await commands.CreatePartyAsync(new(PartyType.Organization, name + " organization"), "hr-acknowledgement-fixture");
                Assert.True(person.IsSuccess);
                Assert.True(organization.IsSuccess);
                return (new CrmPartyAffiliationUpsertCommand(null, person.Value!.PartyId, organization.Value!.PartyId,
                    PartyOrganizationAffiliationKind.Employee, true, JobTitle: "Acknowledged role"), person.Value.PartyId);
            default:
                throw new ArgumentOutOfRangeException(nameof(toolName));
        }
    }

    private static async Task<MutationOwnerState?> ReadMutationOwnerAsync(IServiceProvider services, string toolName, string name, Guid? personId) {
        if (toolName is HrAgentToolPolicy.HrAgentCreate or HrAgentToolPolicy.HrAgentSettingsUpdate) {
            var agent = (await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(includeTemplates: true))
                .Where(item => item.Name == name).SingleOrDefault();
            return agent is null ? null : new(agent.Id, JsonSerializer.Serialize(agent, MafToolProtocolCodec.SerializationOptions));
        }
        if (toolName == HrAgentToolPolicy.HrCrmAffiliationUpsert) {
            var result = await services.GetRequiredService<ICrmPartyCommandService>().ListAffiliationsAsync(personId!.Value);
            Assert.True(result.IsSuccess);
            var affiliation = result.Value!.SingleOrDefault();
            return affiliation is null ? null : new(affiliation.AffiliationId, JsonSerializer.Serialize(affiliation));
        }
        await using var database = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var parties = await database.Set<Party>().AsNoTracking().Where(item => item.DisplayName == name)
            .Select(item => new { item.Id, item.DisplayName, item.CreatedAtUtc, item.UpdatedAtUtc }).ToArrayAsync();
        var party = parties.SingleOrDefault();
        return party is null ? null : new(party.Id, JsonSerializer.Serialize(party));
    }

    private sealed record MutationOwnerState(Guid Id, string Snapshot);
}
