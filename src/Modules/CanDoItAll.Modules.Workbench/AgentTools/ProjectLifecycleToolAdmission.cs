using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectLifecycleToolAdmission(IAgentToolAdmissionVerifier admissions,
    ProjectDefinitionToolAdmission definitions, IAgentCatalogReadLeaseStore catalog,
    ProjectAgentSourceMutationAuthority agents, ProjectProcessExecutionMutationService processes,
    ProjectWriteAdmissionService projects, ProjectLifecycleWriter writer, ProjectLifecycleHierarchyReader hierarchy,
    ProjectLifecycleToolReceiptRecovery recovery, ProjectCreatedTargetProvenance createdTargets,
    ProjectLifecycleCurrentWriteAuthority currentWrite, ProjectStructureLeaseService leases,
    ILogger<ProjectLifecycleToolAdmission> logger) {
    internal const string EffectSourceKind = "project-hierarchy-operation";
    private readonly ProjectHierarchyGovernedInvocationFactory invocationFactory = new(agents, processes, catalog, projects, currentWrite);

    internal AgentRuntimeToolMetadata Metadata(AgentRuntimeToolProviderContext context, string provider, string tool) {
        var source = definitions.OriginalSource(context);
        return new(provider, tool, AgentRuntimeToolOperationKind.Mutation, true, ["projects", "project-structure"]) {
            ApprovalPolicy = context.Purpose == AgentRuntimeToolProviderPurpose.GovernedProcessAutomation
                ? AgentRuntimeToolApprovalPolicy.GovernedProcessAuthority : AgentRuntimeToolApprovalPolicy.Default,
            PrepareAdmission = arguments => ProjectLifecycleToolProposalCodec.Prepare(tool, arguments, source),
            RestoreAdmission = (arguments, original) => ProjectLifecycleToolProposalCodec.Prepare(tool, arguments, original),
            AuthorizeAdmissionAsync = (payload, token) => AuthorizeAsync(context, payload, token),
            AuthorizeResultDisclosureAsync = (disclosure, token) => AuthorizeDisclosureAsync(context, disclosure, token)
        };
    }

    internal async Task<ProjectHierarchySnapshot> ReadHierarchyAsync(AgentRuntimeToolProviderContext context,
        ProjectHierarchySnapshot summary, CancellationToken cancellationToken) {
        await RequireSessionAsync(context, cancellationToken);
        var original = definitions.OriginalLifecycleContext(context);
        var project = await projects.CaptureAsync(summary.ProjectId, cancellationToken)
            ?? throw Denied("The original hierarchy project is no longer available.");
        var edges = await hierarchy.ReadHierarchyAsync(original, project, cancellationToken);
        return summary with { ExpectedProjectAdmission = project, ObservedEdges = edges };
    }

    internal async Task<ProjectStructureHierarchyAcknowledgement> ChangeAsync(AgentRuntimeToolProviderContext context,
        Guid parentProjectId, ProjectStructureSubprojectChangeRequest request, int? estimatedMinutes,
        ProjectStructureAgentContext leaseActor, CancellationToken cancellationToken) {
        var observed = await RequireSessionAsync(context, cancellationToken);
        var candidate = AgentToolInvocationClaim.Current?.Proposal.Payload
            ?? throw Denied("The hierarchy tool has no current serial proposal.");
        var admitted = await admissions.RequireInvocationAsync(observed.Session.Reference,
            ProjectStructureToolPolicy.ProjectStructureSubprojectLink, candidate.Digest, cancellationToken);
        var (source, input) = ProjectLifecycleToolProposalCodec.Read(admitted.Payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, observed.Session);
        var actual = ProjectLifecycleToolProposalCodec.Prepare(admitted.Payload.ToolName,
            JsonSerializer.SerializeToElement(new ProjectLifecycleToolArguments(parentProjectId, request, estimatedMinutes),
                ProjectDefinitionToolProposalCodec.Json), admitted.Payload.SourcePreparation!);
        if (actual != admitted.Payload || admitted.ApprovalStatus != ExecutionApprovalStatus.Approved || admitted.ApprovedDigest != actual.Digest) {
            throw Denied("The invocation differs from its exact saved and approved hierarchy proposal.");
        }
        var original = ProjectLifecycleToolProposalCodec.Submission(source, admitted.IntentId, input);
        var prefix = await admissions.RequirePriorInvocationsAsync(source.Session.Reference, cancellationToken);
        var sources = await createdTargets.ResolveAsync(source, original, prefix, definitions.OriginalCreationLookup, cancellationToken);
        var lookup = await recovery.LookupAsync(source, original, sources, cancellationToken);
        if (lookup is ProjectLifecycleLookup.Found found) {
            if (found.Accepted.Original != original) {
                throw Denied("The retained hierarchy operation belongs to a different original intent.");
            }
            RecordAccepted(found.Accepted);
            return Acknowledgement(found.Accepted);
        }
        if (lookup is not ProjectLifecycleLookup.NotObserved { State: ProjectOperationObservationState.Open }) {
            throw Unconfirmed();
        }
        var invocation = sources.Process is { } process
            ? await invocationFactory.ForProcessAsync(process, admitted, original, cancellationToken)
            : await invocationFactory.ForAgentAsync(sources.Agent!, observed, admitted, original, cancellationToken);
        ProjectLifecycleAccepted? accepted = null;
        try {
            return await leases.RunWithProjectMutationLeaseAsync(input.Request.ChildProjectId, input.Request.LeaseToken,
                leaseActor, "change-subproject-link", async token => {
                    var outcome = await writer.SubmitAsync(original, invocation, token);
                    if (outcome is not ProjectLifecycleOutcome.Accepted known) {
                        throw Unconfirmed();
                    }
                    accepted = known.Result;
                    RecordAccepted(accepted);
                    return Acknowledgement(accepted);
                }, cancellationToken);
        } catch (Exception failure) when (accepted is not null) {
            logger.LogError(failure, "Hierarchy operation {OperationId} committed for child {ProjectId}, but its lease completion failed.",
                original.Operation.Id, input.Request.ChildProjectId);
            return Acknowledgement(accepted);
        }
    }

    private async ValueTask<IAsyncDisposable> AuthorizeAsync(AgentRuntimeToolProviderContext context,
        AgentToolPreparedPayload payload, CancellationToken cancellationToken) {
        var (source, _) = ProjectLifecycleToolProposalCodec.Read(payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, (await RequireSessionAsync(context, cancellationToken)).Session);
        return CompletedCheck.Instance;
    }

    private async ValueTask<IAsyncDisposable?> AuthorizeDisclosureAsync(AgentRuntimeToolProviderContext context,
        AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        var (source, input) = ProjectLifecycleToolProposalCodec.Read(disclosure.Payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, (await RequireSessionAsync(context, cancellationToken)).Session);
        if (disclosure.IsTypedFailure && disclosure.EffectState == AgentToolEffectState.NotCommitted) {
            return CompletedCheck.Instance;
        }
        var original = ProjectLifecycleToolProposalCodec.Submission(source, disclosure.IntentId, input);
        var prefix = await admissions.RequirePriorResultInvocationsAsync(source.Session.Reference, disclosure.IntentId,
            disclosure.Payload, cancellationToken);
        var sources = await createdTargets.ResolveAsync(source, original, prefix, definitions.OriginalCreationLookup, cancellationToken);
        var lookup = await recovery.LookupAsync(source, original, sources, cancellationToken);
        if (lookup is not ProjectLifecycleLookup.Found found || found.Accepted.Original != original ||
                disclosure.EffectState != AgentToolEffectState.Committed ||
                disclosure.Result.Deserialize<ProjectStructureHierarchyAcknowledgement>(ProjectDefinitionToolProposalCodec.Json)
                    != Acknowledgement(found.Accepted)) {
            throw Denied("The hierarchy result cannot be disclosed as the exact currently authorized owner receipt.");
        }
        return CompletedCheck.Instance;
    }

    private async Task<AgentToolSessionObservation> RequireSessionAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken) {
        if (!ProjectDefinitionToolAdmission.UsesJournal(context) || context.AdmittedToolSession is null) {
            throw Denied("Hierarchy mutation requires a verified recoverable Agent or Process execution.");
        }
        var observed = await admissions.RequireSessionObservationAsync(context.AdmittedToolSession, cancellationToken);
        if (observed.Session.Reference != context.AdmittedToolSession || observed.Session.AgentId != context.Agent.Id ||
                observed.Session.Profile.ProfileId != projects.DatabaseProfileId) {
            throw Denied("The hierarchy tool does not belong to this saved execution and current profile.");
        }
        return observed;
    }

    private static ProjectStructureHierarchyAcknowledgement Acknowledgement(ProjectLifecycleAccepted accepted)
        => accepted.Acknowledgement switch {
            ProjectLifecycleAcknowledgement.LinkEnsured ensured => new(accepted.Original.Operation.Id, ensured.EdgeId, ensured.Created, false),
            ProjectLifecycleAcknowledgement.LinkReconnected reconnected => new(accepted.Original.Operation.Id,
                reconnected.DestinationEdgeId, reconnected.Created, true),
            _ => throw new InvalidDataException("The hierarchy tool received an unsupported owner acknowledgement.")
        };

    private static void RecordAccepted(ProjectLifecycleAccepted accepted)
        => AgentToolInvocationEffectScope.RecordCommitted(EffectSourceKind, accepted.Original.Operation.Id.ToString("D"));

    private static ProjectStructureAgentException Unconfirmed()
        => ProjectStructureAgentException.CreateAgentVisible(409, ProjectErrorCodes.DefinitionReceiptUnavailable,
            "The original hierarchy outcome is unresolved. Reconcile this operation before preparing another intent.",
            canRetryWithCorrectedInput: false, effectState: AgentToolEffectState.Unknown);

    private static AgentToolAdmissionException Denied(string message) => ProjectDefinitionToolProposalCodec.Denied(message);

    private sealed class CompletedCheck : IAsyncDisposable {
        internal static CompletedCheck Instance { get; } = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed record ProjectStructureHierarchyAcknowledgement(Guid OperationId, Guid EdgeId, bool Created, bool Reconnected);
