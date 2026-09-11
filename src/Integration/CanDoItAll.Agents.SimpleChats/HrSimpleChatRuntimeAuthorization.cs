using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Agents.SimpleChats;

public sealed class HrSimpleChatRuntimeAuthorization(
    ISandboxWorkspaceCatalogStore catalogStore,
    ISandboxWorkspaceExecutionRunStore runs,
    IAgentToolAdmissionVerifier admissions,
    IAgentExecutionAuthorityResolver? executionAuthorityResolver = null) {

    internal async Task RequireCancelledReceiptAsync(AgentToolReceiptReconciliationAdmission admitted,
        LlmChatRuntimeIdentity profile, CancellationToken cancellationToken) {
        var session = admitted.Session;
        var reference = session.Reference;
        if (session.AgentId != HrAgentIdentity.AgentId || session.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
            session.Profile.ProfileId != profile.ProfileId || session.Profile.Generation.Value != profile.Generation ||
            session.Profile.Fingerprint != profile.Fingerprint) {
            throw Denied("The cancelled HR proposal does not match the current leased profile.");
        }

        var run = await runs.GetExecutionRunAsync(reference.ExecutionRunId, cancellationToken);
        if (run is null || run.Outcome != RunOutcome.Cancelled || run.AgentId != session.AgentId ||
            run.ChatSessionId != reference.ChatSessionId || run.ToolAdmission?.Session != session) {
            throw Denied("The original cancelled HR execution is not available for receipt reconciliation.");
        }

        var original = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson);
        var source = AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson);
        if (original is null || source is null || original.AuthorityId != reference.AuthorityId ||
            original.DatabaseProfileId != profile.ProfileId || original.DatabaseProfileGeneration.Value != profile.Generation ||
            !IsWithinAuthority(original, HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Create))) {
            throw Denied("The saved authority did not admit this owner create receipt.");
        }

        var receiptPolicy = HrSimpleChatToolPolicy.Get(HrSimpleChatOperation.Receipt);
        var catalog = (await catalogStore.LoadCatalogSnapshotAsync(cancellationToken)).Catalog;
        var actor = catalog.Agents.SingleOrDefault(agent => agent.Id == session.AgentId);
        if (actor is null || !IsAssigned(actor, catalog.Capabilities, receiptPolicy)) {
            throw Denied("The current managed HR actor lacks the explicit receipt lookup capability.");
        }

        var resolver = executionAuthorityResolver ?? throw new InvalidOperationException("Receipt reconciliation requires the canonical current authority resolver.");
        AgentExecutionAuthorityRecord current;
        try {
            current = await resolver.ResolveAsync(new(session.AgentId, source.SourceKind, source.SourceId,
                original.WorkspaceScope, session.Profile.Generation, UiAccessHint: null), cancellationToken);
        } catch (AgentExecutionAuthorityMismatchException) {
            throw Denied("Current source authority does not permit receipt reconciliation.");
        }

        if (current.AgentId != session.AgentId || current.DatabaseProfileId != profile.ProfileId ||
            current.DatabaseProfileGeneration.Value != profile.Generation || current.WorkspaceScope != original.WorkspaceScope ||
            !IsWithinAuthority(AgentExecutionGovernanceSnapshot.FromAuthority(current), receiptPolicy)) {
            throw Denied("Current execution authority does not permit this receipt lookup.");
        }
    }

    public static bool CanAttach(AgentRuntimeToolProviderContext context) {
        ArgumentNullException.ThrowIfNull(context);
        return context.Purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
            context.AdmittedToolSession is not null &&
            context.Governance is { ReadAllowed: true } governance &&
            governance.AgentId == context.Agent.Id &&
            governance.AuthorityId == context.AdmittedToolSession.AuthorityId &&
            IsManagedActor(context.Agent);
    }

    public static bool IsAssigned(AgentDefinition agent, IReadOnlyList<CapabilityCatalogItem> catalog,
        HrSimpleChatToolOperation operation) {
        if (!IsManagedActor(agent)) {
            return false;
        }

        var assignments = agent.Capabilities.Where(assignment =>
            string.Equals(assignment.CapabilityKey, operation.CapabilityKey, StringComparison.Ordinal)).ToArray();
        return assignments.Length == 1 && assignments[0].Kind == CapabilityKind.Tool && catalog.Count(capability =>
            capability.Id == assignments[0].CapabilityId && capability.Kind == CapabilityKind.Tool &&
            string.Equals(capability.Key, operation.CapabilityKey, StringComparison.Ordinal)) == 1;
    }

    public static bool IsWithinAuthority(AgentExecutionGovernanceSnapshot? governance, HrSimpleChatToolOperation operation) {
        if (governance is not { ReadAllowed: true } ||
            operation.Effect == AgentToolProposalEffect.Mutation && !governance.MutationAllowed ||
            governance.AllowedOperations.Count != 0) {
            return false;
        }

        return governance.AllowedCapabilityKeys.Count == 0 || governance.AllowedCapabilityKeys.Contains(operation.CapabilityKey);
    }

    public Task<AgentToolSessionAdmission> RequireSessionAsync(
        AgentRuntimeToolProviderContext context,
        HrSimpleChatToolOperation operation,
        LlmChatRuntimeIdentity profile,
        CancellationToken cancellationToken)
        => RequireSessionAsync(context, operation, operation, profile, cancellationToken);

    internal Task<AgentToolSessionAdmission> RequireResultDisclosureAsync(
        AgentRuntimeToolProviderContext context, HrSimpleChatToolOperation operation,
        LlmChatRuntimeIdentity profile, CancellationToken cancellationToken) {
        var readOperation = operation.Operation switch {
            HrSimpleChatOperation.Create => HrSimpleChatOperation.Receipt,
            HrSimpleChatOperation.Update or HrSimpleChatOperation.Status => HrSimpleChatOperation.Search,
            _ => operation.Operation
        };
        return RequireSessionAsync(context, operation, HrSimpleChatToolPolicy.Get(readOperation), profile, cancellationToken);
    }

    private async Task<AgentToolSessionAdmission> RequireSessionAsync(
        AgentRuntimeToolProviderContext context, HrSimpleChatToolOperation operation,
        HrSimpleChatToolOperation currentOperation, LlmChatRuntimeIdentity profile, CancellationToken cancellationToken) {
        if (!CanAttach(context)) {
            throw Denied("An admitted interactive managed HR session is required.");
        }

        var reference = context.AdmittedToolSession!;
        var admission = await admissions.RequireSessionAsync(reference, cancellationToken);
        if (admission.Reference != reference || admission.AgentId != HrAgentIdentity.AgentId ||
            admission.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
            admission.Profile.ProfileId != profile.ProfileId ||
            admission.Profile.Generation.Value != profile.Generation ||
            !string.Equals(admission.Profile.Fingerprint, profile.Fingerprint, StringComparison.Ordinal)) {
            throw Denied("The persisted HR session does not match the active execution binding.");
        }

        var run = await runs.GetExecutionRunAsync(reference.ExecutionRunId, cancellationToken);
        if (run is null || run.Id != reference.ExecutionRunId || run.AgentId != admission.AgentId ||
            run.ChatSessionId != reference.ChatSessionId ||
            run.State is not (ExecutionState.Preparing or ExecutionState.Running or ExecutionState.WaitingOnTool)) {
            throw Denied("The HR session is not the active admitted execution.");
        }

        var governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson);
        if (governance is null || governance.AuthorityId != reference.AuthorityId ||
            governance.AgentId != admission.AgentId || governance.DatabaseProfileId != profile.ProfileId ||
            governance.DatabaseProfileGeneration.Value != profile.Generation ||
            !string.Equals(governance.PolicyFingerprint, context.Governance!.PolicyFingerprint, StringComparison.Ordinal) ||
            !IsWithinAuthority(governance, operation)) {
            throw Denied("The persisted execution authority does not allow this Simple Chat operation.");
        }

        var source = AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson);
        if (source is null) {
            throw Denied("The admitted HR execution has no saved source authority to revalidate.");
        }

        var resolver = executionAuthorityResolver ?? throw new InvalidOperationException(
            "HR Simple Chat administration requires the canonical current source authority resolver.");
        AgentExecutionAuthorityRecord current;
        try {
            current = await resolver.ResolveAsync(new(admission.AgentId, source.SourceKind, source.SourceId,
                governance.WorkspaceScope, admission.Profile.Generation,
                UiAccessHint: null), cancellationToken);
        } catch (AgentExecutionAuthorityMismatchException) {
            throw Denied("The current source authority no longer permits this HR execution.");
        }

        if (current.AgentId != admission.AgentId || current.DatabaseProfileId != profile.ProfileId ||
            current.DatabaseProfileGeneration.Value != profile.Generation || current.WorkspaceScope != governance.WorkspaceScope ||
            !IsWithinAuthority(AgentExecutionGovernanceSnapshot.FromAuthority(current), currentOperation)) {
            throw Denied("The current source authority does not allow this Simple Chat operation.");
        }

        var catalog = (await catalogStore.LoadCatalogSnapshotAsync(cancellationToken)).Catalog;
        var actor = catalog.Agents.SingleOrDefault(agent => agent.Id == admission.AgentId);
        if (actor is null || !IsManagedActor(actor)) {
            throw Denied("The managed HR actor is no longer active or permitted to use tools.");
        }

        if (!IsAssigned(actor, catalog.Capabilities, currentOperation)) {
            throw Denied("The managed HR actor does not have the exact Simple Chat capability assignment.");
        }

        return admission;
    }

    public async Task<AgentToolAdmittedInvocation> RequireApprovedProposalAsync(
        AgentToolSessionAdmission session,
        AgentToolPreparedPayload proposed,
        CancellationToken cancellationToken) {
        var admitted = await admissions.RequireInvocationAsync(session.Reference, proposed.ToolName, proposed.Digest, cancellationToken);
        if (admitted.Session != session.Reference || admitted.BatchId.Value == Guid.Empty || admitted.IntentId.Value == Guid.Empty ||
            !string.Equals(admitted.Payload.ToolName, proposed.ToolName, StringComparison.Ordinal) ||
            admitted.Payload.SemanticVersion != proposed.SemanticVersion || admitted.Payload.Digest != proposed.Digest ||
            admitted.Payload.Effect != proposed.Effect || admitted.Payload.Recovery != proposed.Recovery ||
            admitted.ApprovalStatus != ExecutionApprovalStatus.Approved || admitted.ApprovedDigest != proposed.Digest) {
            throw Denied("An exact approved proposal from the active durable tool batch is required.");
        }

        return admitted;
    }

    private static bool IsManagedActor(AgentDefinition agent)
        => HrAgentIdentity.Matches(agent) && agent.Status == AgentLifecycleStatus.Active &&
            !agent.IsTemplate && agent.Permissions.CanUseTools;

    private static HrSimpleChatAdministrationException Denied(string message)
        => new("hr-simple-chat.authorization-denied", message);
}
