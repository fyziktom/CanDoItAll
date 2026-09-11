using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using System.Text.Json;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureProcessToolAdmission(
    IAgentToolAdmissionVerifier admissions,
    ICanonicalRuntimeDatabase database,
    IProcessPreparedLaunchStore preparations,
    ProjectProcessLaunchAuthorityService authorities,
    ProjectStructureProcessProposalCodec codec,
    IProcessExecutionDispatchAuthorityReader? processExecutions = null) : IAgentToolReceiptReconciliationProvider {
    public const string EffectSourceKind = "process-launch-admission";

    public bool Supports(string toolName) => codec.Supports(toolName);

    public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        var input = codec.Read(admitted.Payload);
        if (admitted.Session.Reference.BackgroundSource is not null) {
            return await ReconcileBackgroundAsync(admitted, claim, input, cancellationToken);
        }
        if (admitted.Session.Profile.ProfileId != database.Profile.Profile.Id ||
                admitted.Session.Purpose != AgentRuntimeContextPurpose.InteractiveChat) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var saved = await preparations.FindByIntentAsync(new(admitted.Binding.IntentId.Value), cancellationToken);
        await claim.RequireAsync(cancellationToken);
        if (saved is null) {
            return AgentToolReceiptObservation.NotObserved;
        }
        if (saved.Preparation.Request.ProducerInputFingerprint != admitted.Payload.Digest.Value ||
                saved.Preparation.Authority?.Principal is not ProcessLaunchPrincipal.AgentExecution source ||
                source.Operation != ProcessLaunchAgentOperation.StructureStart ||
                source.Ceiling.AgentId != admitted.Session.AgentId || source.Ceiling.AuthorityId != admitted.Session.Reference.AuthorityId.Value ||
                saved.Preparation.Authority.DatabaseProfileId != database.Profile.Profile.Id ||
                saved.Preparation.Authority.ProjectAdmission?.ProjectId != input.ProjectId) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        return Receipt(saved);
    }

    private static AgentToolReceiptObservation Receipt(ProcessPreparedLaunchSnapshot saved)
        => new(new(EffectSourceKind, saved.Preparation.AdmissionId.Value.ToString("D")),
            AgentToolProtocolEnvelope.Create("structure-process-launch-receipt", 1,
                JsonSerializer.Serialize(new {
                    AdmissionId = saved.Preparation.AdmissionId.Value,
                    LaunchPlanId = saved.Preparation.Review.PlanId.Value,
                    AcceptedRunId = saved.AcceptedAtUtc is null ? (Guid?)null : saved.Preparation.InitialCommit.Mutation.State.RunId.Value,
                    saved.State,
                    saved.LinkDeliveryState
                }, ProjectStructureProcessProposalCodec.SerializerOptions)));
    public static bool UsesJournal(AgentRuntimeToolProviderContext context)
        => context.Purpose is (AgentRuntimeToolProviderPurpose.InteractiveChat or AgentRuntimeToolProviderPurpose.GovernedProcessAutomation) &&
            context.AdmittedToolSession is not null &&
            context.ToolAdmissionSupport == AgentToolAdmissionSupport.Recoverable;

    public async ValueTask<IAsyncDisposable> AuthorizeProposalAsync(AgentRuntimeToolProviderContext context,
        AgentToolPreparedPayload payload, CancellationToken cancellationToken) {
        codec.Read(payload);
        await RequireSessionAsync(context, cancellationToken);
        return CompletedCheck.Instance;
    }

    public async ValueTask<IAsyncDisposable?> AuthorizeResultDisclosureAsync(AgentRuntimeToolProviderContext context,
        AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        var input = codec.Read(disclosure.Payload);
        var session = await RequireSessionAsync(context, cancellationToken);
        var saved = await preparations.FindByIntentAsync(new(disclosure.IntentId.Value), cancellationToken);
        await RequireSessionAsync(context, cancellationToken);
        if (session.Reference.BackgroundSource is not null) {
            return await AuthorizeBackgroundResultAsync(session, disclosure, input, saved, cancellationToken);
        }
        if (saved is null && disclosure.EffectState == AgentToolEffectState.NotCommitted) {
            return await authorities.AcquireUncommittedResultReadAsync(context.Governance!, input.ProjectId, cancellationToken);
        }
        var authority = saved?.Preparation.Authority;
        if (saved is null || saved.Preparation.Request.ProducerInputFingerprint != disclosure.Payload.Digest.Value ||
                authority?.Principal is not ProcessLaunchPrincipal.AgentExecution source ||
                source.Operation != ProcessLaunchAgentOperation.StructureStart || source.Ceiling.AgentId != session.AgentId ||
                source.Ceiling.AuthorityId != session.Reference.AuthorityId.Value ||
                authority.DatabaseProfileId != session.Profile.ProfileId || authority.ProjectAdmission?.ProjectId != input.ProjectId) {
            throw Denied("The original Process receipt does not match this saved proposal and source authority.");
        }
        if (disclosure.EffectState != AgentToolEffectState.NotCommitted) {
            RequireResultIdentity(disclosure.Result, input, saved);
        }
        return await authorities.AcquireResultReadAsync(authority, cancellationToken);
    }

    private static void RequireResultIdentity(JsonElement value, ProjectStructureProcessStartProposal input,
        ProcessPreparedLaunchSnapshot saved) {
        var result = value.Deserialize<ProjectStructureProcessNodeStartResult>(ProjectStructureProcessProposalCodec.ResultSerializerOptions)
            ?? throw Denied("The saved Process result has no original receipt identity.");
        if (result.ProjectId != input.ProjectId || result.NodeId != input.NodeId ||
                result.ProcessDefinitionId != saved.Preparation.Review.DefinitionId.Value ||
                result.LaunchPlanId != saved.Preparation.Review.PlanId.Value || result.Observation is not { } observation ||
                observation.AdmissionId != saved.Preparation.AdmissionId || result.RunId != observation.AcceptedRunId?.Value ||
                result.RunId is { } runId && (saved.AcceptedAtUtc is null ||
                    runId != saved.Preparation.InitialCommit.Mutation.State.RunId.Value)) {
            throw Denied("The saved Process result differs from the original owner receipt.");
        }
    }

    internal async Task<ProjectStructureProcessToolClaim> RequireInvocationAsync(AgentRuntimeToolProviderContext context,
        ProjectStructureProcessStartProposal input, CancellationToken cancellationToken) {
        var session = await RequireSessionAsync(context, cancellationToken);
        var payload = codec.Prepare(input);
        var admitted = await admissions.RequireInvocationAsync(session.Reference, payload.ToolName, payload.Digest, cancellationToken);
        if (admitted.Session != session.Reference || admitted.IntentId.Value == Guid.Empty || admitted.BatchId.Value == Guid.Empty ||
                admitted.Payload != payload || admitted.ApprovalStatus != ExecutionApprovalStatus.Approved ||
                admitted.ApprovedDigest != payload.Digest) {
            throw Denied("The current serial tool dispatch does not own this exact approved Process proposal.");
        }
        return session.Reference.BackgroundSource is { } background
            ? new(admitted, null, input.ProjectId, await ReadBackgroundExecutionAsync(session, cancellationToken), background)
            : new(admitted, context.Governance!, input.ProjectId);
    }

    internal async Task<ProjectStructureProcessLaunchInvocation?> FindReplayAsync(ProjectStructureProcessToolClaim claim,
        CancellationToken cancellationToken) {
        var saved = await preparations.FindByIntentAsync(new(claim.Admitted.IntentId.Value), cancellationToken);
        if (saved is null) {
            return null;
        }
        if (claim.BackgroundSource is not null) {
            RequireBackgroundReceipt(saved, claim.Admitted.Session, claim.Admitted.IntentId, claim.Admitted.Payload, claim.ProjectId);
            return new(new(claim.Admitted.IntentId.Value), claim.Admitted.Payload.Digest.Value, saved.Preparation.Authority!,
                codec.Read(claim.Admitted.Payload), saved.Preparation.ToolSource);
        }
        var authority = saved.Preparation.Authority;
        if (saved.Preparation.Request.ProducerInputFingerprint != claim.Admitted.Payload.Digest.Value ||
                authority?.Principal is not ProcessLaunchPrincipal.AgentExecution source ||
                source.Operation != ProcessLaunchAgentOperation.StructureStart ||
                source.Ceiling.AuthorityId != claim.Governance!.AuthorityId.Value || source.Ceiling.AgentId != claim.Governance.AgentId ||
                authority.DatabaseProfileId != claim.Governance.DatabaseProfileId ||
                authority.ProjectAdmission?.ProjectId != claim.ProjectId) {
            throw new ProcessLaunchIntentConflictException(new(claim.Admitted.IntentId.Value),
                "The saved Process admission belongs to a different proposal, source authority or project.");
        }
        return new(new(claim.Admitted.IntentId.Value), claim.Admitted.Payload.Digest.Value, authority, codec.Read(claim.Admitted.Payload));
    }

    internal async Task<ProjectStructureProcessLaunchInvocation> CaptureAsync(ProjectStructureProcessToolClaim claim,
        ProjectWriteAdmission expectedProject, CancellationToken cancellationToken) {
        if (expectedProject.ProjectId != claim.ProjectId) {
            throw Denied("The Process proposal cannot change its admitted project.");
        }
        if (claim.BackgroundSource is not null) {
            return await CaptureBackgroundAsync(claim, expectedProject, cancellationToken);
        }
        var authority = await authorities.CaptureAgentAsync(new(expectedProject.DatabaseProfileId, expectedProject.ProjectId,
            expectedProject.LifetimeId), claim.Governance!, ProcessLaunchAgentOperation.StructureStart, cancellationToken);
        return new(new(claim.Admitted.IntentId.Value), claim.Admitted.Payload.Digest.Value, authority, codec.Read(claim.Admitted.Payload));
    }

    private async Task<AgentToolSessionAdmission> RequireSessionAsync(AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken) {
        if (context.Purpose == AgentRuntimeToolProviderPurpose.GovernedProcessAutomation) {
            return await RequireBackgroundSessionAsync(context, cancellationToken);
        }
        if (!UsesJournal(context) || context.ToolAdmissionSupport != AgentToolAdmissionSupport.Recoverable ||
                context.Governance is not { ReadAllowed: true } governance || governance.AgentId != context.Agent.Id ||
                governance.AuthorityId != context.AdmittedToolSession!.AuthorityId) {
            throw Denied("This Process producer requires its retained interactive execution and durable tool proposal.");
        }
        var admitted = await admissions.RequireSessionAsync(context.AdmittedToolSession, cancellationToken);
        if (admitted.Reference != context.AdmittedToolSession || admitted.AgentId != governance.AgentId ||
                admitted.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
                admitted.Profile.ProfileId != database.Profile.Profile.Id || admitted.Profile.ProfileId != governance.DatabaseProfileId ||
                admitted.Profile.Generation != governance.DatabaseProfileGeneration) {
            throw Denied("The Process producer's session, actor or runtime profile does not match its saved execution authority.");
        }
        return admitted;
    }

    private static ProjectStructureAgentException Denied(string message) => new(403, "ProcessToolAdmissionDenied", message);

    private sealed class CompletedCheck : IAsyncDisposable {
        internal static CompletedCheck Instance { get; } = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed record ProjectStructureProcessToolClaim(
    AgentToolAdmittedInvocation Admitted,
    AgentExecutionGovernanceSnapshot? Governance,
    Guid ProjectId,
    ProcessExecutionDispatchAuthority? Execution = null,
    AgentToolBackgroundSourceBinding? BackgroundSource = null);

public sealed record ProjectStructureProcessLaunchInvocation {
    internal ProjectStructureProcessLaunchInvocation(ProcessLaunchIntentId intentId, string inputFingerprint,
        ProcessLaunchAuthority authority, ProjectStructureProcessStartProposal proposal, ProcessLaunchToolSource? toolSource = null) {
        IntentId = intentId;
        InputFingerprint = inputFingerprint;
        Authority = authority;
        Proposal = proposal;
        ToolSource = toolSource;
    }

    public ProcessLaunchIntentId IntentId { get; }
    public string InputFingerprint { get; }
    public ProcessLaunchAuthority Authority { get; }
    public ProjectStructureProcessStartProposal Proposal { get; }
    public ProcessLaunchToolSource? ToolSource { get; }
}
