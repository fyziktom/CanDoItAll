using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureProcessToolAdmission {
    private async Task<AgentToolSessionAdmission> RequireBackgroundSessionAsync(AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken) {
        if (!UsesJournal(context) || context.AdmittedToolSession?.BackgroundSource is null || context.Governance is not null) {
            throw Denied("This Process producer requires its original background execution and durable proposal.");
        }
        var session = await admissions.RequireSessionAsync(context.AdmittedToolSession, cancellationToken);
        if (session.Reference != context.AdmittedToolSession || session.AgentId != context.Agent.Id ||
                session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                session.Profile.ProfileId != database.Profile.Profile.Id) {
            throw Denied("The Process producer does not match its original background executor and profile.");
        }
        await ReadBackgroundExecutionAsync(session, cancellationToken);
        return session;
    }

    private async Task<ProcessExecutionDispatchAuthority> ReadBackgroundExecutionAsync(AgentToolSessionAdmission session,
        CancellationToken cancellationToken) {
        var source = session.Reference.BackgroundSource
            ?? throw Denied("The Process producer has no saved background source.");
        var found = await (processExecutions ?? throw new InvalidOperationException("The Process execution authority reader is not installed."))
            .ReadAsync(session.Reference.ExecutionRunId, cancellationToken);
        if (found.Snapshot is not { } execution || execution.Evidence.ExecutionRunId != session.Reference.ExecutionRunId ||
                execution.Evidence.ExecutorAgentId != session.AgentId || source.SourceId != execution.Evidence.StepKey ||
                AgentToolProtocolEnvelope.ComputeDigest(execution.OwnerFingerprint) != source.OwnerFingerprint) {
            throw Denied("The original background Process claim and journal source no longer agree.");
        }
        return execution;
    }

    private async Task<ProjectStructureProcessLaunchInvocation> CaptureBackgroundAsync(ProjectStructureProcessToolClaim claim,
        ProjectWriteAdmission expectedProject, CancellationToken cancellationToken) {
        var execution = claim.Execution ?? throw Denied("The background proposal has no saved Process dispatch.");
        if (!execution.ObservedCurrentDispatch || execution.SourceAuthority?.ProjectAdmission is not { } project ||
                execution.ProjectReference is null || expectedProject != new ProjectWriteAdmission(project.DatabaseProfileId, project.ProjectId, project.LifetimeId) ||
                !execution.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase)) {
            throw Denied("The current Process claim does not permit a launch in this original project lifetime.");
        }
        await authorities.RequireCurrentAsync(execution.SourceAuthority, cancellationToken);
        var source = claim.BackgroundSource!;
        var toolSource = new ProcessLaunchToolSource(ProcessLaunchToolSource.CurrentSchemaVersion, execution, source.SourceKind,
            source.SourceId, source.ExecutionFingerprint.Value, new(claim.Admitted.IntentId.Value), claim.Admitted.Payload.Digest.Value);
        toolSource.Validate();
        return new(new(claim.Admitted.IntentId.Value), claim.Admitted.Payload.Digest.Value,
            execution.SourceAuthority, codec.Read(claim.Admitted.Payload), toolSource);
    }

    private static void RequireBackgroundReceipt(ProcessPreparedLaunchSnapshot saved, AgentToolSessionReference session,
        AgentToolBusinessIntentId intent, AgentToolPreparedPayload payload, Guid projectId) {
        var background = session.BackgroundSource;
        var source = saved.Preparation.ToolSource;
        if (background is null || source is null || source.IntentId.Value != intent.Value ||
                source.Execution.Evidence.ExecutionRunId != session.ExecutionRunId ||
                source.SourceKind != background.SourceKind || source.SourceId != background.SourceId ||
                source.ExecutionFingerprint != background.ExecutionFingerprint.Value ||
                AgentToolProtocolEnvelope.ComputeDigest(source.Execution.OwnerFingerprint) != background.OwnerFingerprint ||
                source.ProposalFingerprint != payload.Digest.Value || source.Execution.ProjectId != projectId ||
                saved.Preparation.Request.ProducerInputFingerprint != payload.Digest.Value) {
            throw Denied("The saved Process receipt belongs to a different background proposal, claim or project.");
        }
        source.RequirePreparation(saved.Preparation);
    }

    private async ValueTask<IAsyncDisposable?> AuthorizeBackgroundResultAsync(AgentToolSessionAdmission session,
        AgentToolResultDisclosure disclosure, ProjectStructureProcessStartProposal input, ProcessPreparedLaunchSnapshot? saved,
        CancellationToken cancellationToken) {
        if (saved is null && disclosure.EffectState == AgentToolEffectState.NotCommitted) {
            var execution = await ReadBackgroundExecutionAsync(session, cancellationToken);
            if (execution.SourceAuthority is not { } authority || execution.ProjectId != input.ProjectId) {
                throw Denied("The original Process source has no project result-read authority.");
            }
            return await authorities.AcquireResultReadAsync(authority, cancellationToken);
        }
        if (saved is null) {
            throw Denied("The original Process tool receipt is unavailable for result disclosure.");
        }
        RequireBackgroundReceipt(saved, session.Reference, disclosure.IntentId, disclosure.Payload, input.ProjectId);
        if (disclosure.EffectState != AgentToolEffectState.NotCommitted) {
            RequireResultIdentity(disclosure.Result, input, saved);
        }
        return await authorities.AcquireResultReadAsync(saved.Preparation.Authority!, cancellationToken);
    }

    private async ValueTask<AgentToolReceiptObservation> ReconcileBackgroundAsync(AgentToolReceiptReconciliationAdmission admitted,
        AgentToolReceiptReconciliationClaim claim, ProjectStructureProcessStartProposal input, CancellationToken cancellationToken) {
        if (admitted.Session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                admitted.Session.Profile.ProfileId != database.Profile.Profile.Id) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        var saved = await preparations.FindByIntentAsync(new(admitted.Binding.IntentId.Value), cancellationToken);
        await claim.RequireAsync(cancellationToken);
        if (saved is null) {
            return AgentToolReceiptObservation.NotObserved;
        }
        RequireBackgroundReceipt(saved, admitted.Session.Reference, admitted.Binding.IntentId, admitted.Payload, input.ProjectId);
        return Receipt(saved);
    }
}
