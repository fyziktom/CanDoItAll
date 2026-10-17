using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectDefinitionToolAdmission(IAgentToolAdmissionVerifier admissions,
    IAgentCatalogReadLeaseStore catalog, ProjectStructureAgentAdmissionService structureAdmissions,
    ProjectAgentSourceMutationAuthority agentAuthority, ProjectProcessExecutionMutationService processes,
    ProjectWriteAdmissionService projects, ProjectDefinitionOperationProtocol protocol,
    ProjectDefinitionToolReceiptRecovery recovery, ProjectDefinitionWriter writer,
    ProjectStructureAgentAuthorizationService authorization, ProjectStructureLeaseService leases,
    ILogger<ProjectDefinitionToolAdmission> logger) {
    internal const string EffectSourceKind = "project-definition-operation";
    private readonly ConditionalWeakTable<AgentRuntimeToolProviderContext, CapturedSource> sources = new();
    private readonly ProjectDefinitionGovernedInvocationFactory invocationFactory = new(agentAuthority, processes, catalog, projects);

    internal static bool UsesJournal(AgentRuntimeToolProviderContext context)
        => ProjectStructureProcessToolAdmission.UsesJournal(context);

    internal async Task CaptureAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken) {
        var observed = await RequireSessionAsync(context, cancellationToken);
        ProjectDefinitionToolAgentSource? agent = null;
        CanDoItAll.Processes.Application.ProcessExecutionDispatchAuthority? process = null;
        ProjectMutationCallerScope caller;
        if (observed.Session.Reference.BackgroundSource is not null) {
            process = (await processes.ObserveAsync(observed.Session.Reference.ExecutionRunId, cancellationToken))?.Dispatch
                ?? throw Denied("The original Process execution source cannot be read.");
            caller = ProjectDefinitionGovernedBindings.ProcessCaller(process);
        } else {
            await using var held = await catalog.AcquireAgentReadLeaseAsync(observed.Session.AgentId, cancellationToken);
            if (held.Agent is null || held.Scope != WorkspaceScopeDescriptor.Organization(observed.Session.Profile.ProfileId.ToString("N"))) {
                throw Denied("The original Agent source cannot be read.");
            }
            agent = new(AgentProjectStructureAccessMetadata.Read(held.Agent.ConfigurationJson),
                observed.Governance ?? throw Denied("The original Agent governance is missing."),
                await structureAdmissions.CaptureSourceProjectAsync(observed.Governance, observed.Session.Reference, cancellationToken));
            caller = ProjectDefinitionGovernedBindings.AgentCaller(observed.Session);
        }
        var metadata = await protocol.GetWriteContextAsync(caller, cancellationToken);
        var envelope = ProjectDefinitionToolProposalCodec.WriteSource(new(observed.Session, metadata.Namespace,
            metadata.Epoch, metadata.CallerBinding, ProjectDefinitionRequestVersion.VersionOne, agent, process));
        sources.Remove(context);
        sources.Add(context, new(envelope));
    }

    internal AgentRuntimeToolMetadata Metadata(AgentRuntimeToolProviderContext context, string provider, string tool) {
        var source = sources.TryGetValue(context, out var captured) ? captured.Envelope
            : throw Denied("The definition provider has not captured its original source.");
        return new(provider, tool, AgentRuntimeToolOperationKind.Mutation, true, ["projects", "project-structure"]) {
            ApprovalPolicy = context.Purpose == AgentRuntimeToolProviderPurpose.GovernedProcessAutomation
                ? AgentRuntimeToolApprovalPolicy.GovernedProcessAuthority : AgentRuntimeToolApprovalPolicy.Default,
            PrepareAdmission = arguments => ProjectDefinitionToolProposalCodec.Prepare(tool, arguments, source),
            RestoreAdmission = (arguments, original) => ProjectDefinitionToolProposalCodec.Prepare(tool, arguments, original),
            AuthorizeAdmissionAsync = (payload, token) => AuthorizeAsync(context, payload, token),
            AuthorizeResultDisclosureAsync = (disclosure, token) => AuthorizeDisclosureAsync(context, disclosure, token)
        };
    }

    internal async Task<ProjectDefinitionScalarsAcknowledgement> UpdateAsync(AgentRuntimeToolProviderContext context,
        Guid projectId, ProjectStructureProjectSaveRequest request, int? estimatedMinutes,
        ProjectStructureAgentContext leaseActor, CancellationToken cancellationToken) {
        var (admitted, source, input) = await RequireInvocationAsync(context, ProjectStructureToolPolicy.ProjectStructureProjectUpdate,
            new(request, ProjectId: projectId, EstimatedMinutes: estimatedMinutes), cancellationToken);
        var operation = source.Operation(admitted.IntentId);
        var lookup = await recovery.LookupAsync(source, admitted.IntentId, admitted.Payload.ToolName, input, cancellationToken);
        if (lookup is ProjectDefinitionLookup.FoundScalars found) {
            RecordAccepted(operation);
            return found.Result.Acknowledgement;
        }
        RequireOpen(lookup);
        var invocation = Invocation(source, admitted, input, null);
        var command = new UpdateProjectDefinitionScalarsCommand(input.Request.ExpectedDefinition!, ProjectDefinitionToolProposalCodec.Scalars(input.Request));
        ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement>? accepted = null;
        try {
            return await leases.RunWithProjectMutationLeaseAsync(projectId, request.LeaseToken, leaseActor, "save-project",
                async token => {
                    accepted = RequireAccepted(await writer.UpdateScalarsAsync(new(operation, source.RequestVersion, command), invocation, token));
                    RecordAccepted(operation);
                    return accepted.Acknowledgement;
                }, cancellationToken);
        } catch (Exception failure) when (accepted is not null) {
            logger.LogError(failure, "Definition operation {OperationId} committed project {ProjectId}, but its mutation lease cleanup failed.", operation.Id, projectId);
            return accepted.Acknowledgement;
        }
    }

    internal async Task<ProjectSummary> CreateAsync(AgentRuntimeToolProviderContext context, Guid? parentProjectId,
        ProjectStructureProjectSaveRequest request, int? estimatedMinutes,
        Action<ProjectCreationReservation> retainForSession, CancellationToken cancellationToken) {
        var tool = parentProjectId.HasValue ? ProjectStructureToolPolicy.ProjectStructureSubprojectCreate : ProjectStructureToolPolicy.ProjectStructureProjectCreate;
        var (admitted, source, input) = await RequireInvocationAsync(context, tool,
            new(request, ParentProjectId: parentProjectId, EstimatedMinutes: estimatedMinutes), cancellationToken);
        var operation = source.Operation(admitted.IntentId);
        var lookup = await recovery.LookupAsync(source, admitted.IntentId, tool, input, cancellationToken);
        if (lookup is ProjectDefinitionLookup.FoundFull found) {
            RecordAccepted(operation);
            try {
                if (await recovery.FindReservationAsync(source, admitted.IntentId, input, cancellationToken) is { } existing) {
                    retainForSession(existing);
                }
            } catch (Exception failure) {
                logger.LogError(failure, "Accepted definition operation {OperationId} could not restore session access for project {ProjectId}.",
                    operation.Id, found.Result.Acknowledgement.Definition.Project.ProjectId);
            }
            return Summary(found.Result, parentProjectId.HasValue);
        }
        RequireOpen(lookup);
        var original = Invocation(source, admitted, input, null);
        var projectId = ProjectDefinitionToolProposalCodec.ReservedProjectId(source, admitted.IntentId);
        var reservation = await projects.ReserveCreationAsync(projectId, source.Session.AgentId, admitted.IntentId.Value,
            parentProjectId, cancellationToken, original.Authorization, input.Request.ExpectedParentAdmission);
        ProjectDefinitionToolReceiptRecovery.RequireReservation(source, admitted.IntentId, input, reservation);
        await projects.RequireCreationGrantAsync(reservation, cancellationToken);
        await authorization.GrantCreatedProjectAccessAsync(source.Session.AgentId, reservation, cancellationToken);
        var invocation = Invocation(source, admitted, input, reservation);
        var command = new CreateProjectDefinitionCommand(ProjectDefinitionToolProposalCodec.Scalars(input.Request), [], [], null);
        var accepted = RequireAccepted(await writer.CreateAsync(new(operation, source.RequestVersion, command), invocation, reservation, cancellationToken));
        RecordAccepted(operation);
        try {
            retainForSession(reservation);
        } catch (Exception failure) {
            logger.LogError(failure, "Definition operation {OperationId} created project {ProjectId}, but its session access bookkeeping failed.", operation.Id, reservation.ProjectId);
        }
        return Summary(accepted, parentProjectId.HasValue);
    }

    private async Task<(AgentToolAdmittedInvocation Admitted, ProjectDefinitionToolSource Source, ProjectDefinitionToolArguments Input)>
        RequireInvocationAsync(AgentRuntimeToolProviderContext context, string tool, ProjectDefinitionToolArguments input, CancellationToken cancellationToken) {
        var observed = await RequireSessionAsync(context, cancellationToken);
        var candidate = AgentToolInvocationClaim.Current?.Proposal.Payload ?? throw Denied("The definition tool has no current serial proposal.");
        var admitted = await admissions.RequireInvocationAsync(observed.Session.Reference, tool, candidate.Digest, cancellationToken);
        var (source, original) = ProjectDefinitionToolProposalCodec.Read(admitted.Payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, observed.Session);
        var actual = ProjectDefinitionToolProposalCodec.Prepare(tool, JsonSerializer.SerializeToElement(input, ProjectDefinitionToolProposalCodec.Json),
            admitted.Payload.SourcePreparation!);
        if (actual != admitted.Payload || admitted.ApprovalStatus != ExecutionApprovalStatus.Approved || admitted.ApprovedDigest != actual.Digest) {
            throw Denied("The invocation differs from its exact saved and approved definition proposal.");
        }
        return (admitted, source, original);
    }

    private ProjectDefinitionInvocation Invocation(ProjectDefinitionToolSource source, AgentToolAdmittedInvocation admitted,
        ProjectDefinitionToolArguments input, ProjectCreationReservation? reservation) {
        ProjectWriteAdmission[] targets = input.Request.ExpectedDefinition is { } expected ? [expected.Project] :
            input.Request.ExpectedParentAdmission is { } parent ? [parent] : [];
        if (source.Process is { } dispatch) {
            var process = new ProjectProcessMutationAdmission(dispatch);
            if (reservation is not null) {
                process = process.WithCreatedProject(reservation);
            }
            var operation = admitted.Payload.ToolName switch {
                ProjectStructureToolPolicy.ProjectStructureProjectUpdate => ProjectProcessProjectOperation.Update,
                ProjectStructureToolPolicy.ProjectStructureSubprojectCreate => ProjectProcessProjectOperation.CreateChild,
                _ => ProjectProcessProjectOperation.CreateRoot
            };
            return invocationFactory.ForProcess(process, operation, targets, admitted);
        }
        var agent = source.AgentAdmission(admitted.Payload.ToolName);
        if (reservation is not null) {
            agent = agent.WithCreatedProject(reservation);
        }
        return invocationFactory.ForAgent(agent, new(source.Session, null, source.Agent!.Governance), admitted, targets);
    }

    private async ValueTask<IAsyncDisposable> AuthorizeAsync(AgentRuntimeToolProviderContext context, AgentToolPreparedPayload payload,
        CancellationToken cancellationToken) {
        var (source, _) = ProjectDefinitionToolProposalCodec.Read(payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, (await RequireSessionAsync(context, cancellationToken)).Session);
        return CompletedCheck.Instance;
    }

    private async ValueTask<IAsyncDisposable?> AuthorizeDisclosureAsync(AgentRuntimeToolProviderContext context,
        AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        var (source, input) = ProjectDefinitionToolProposalCodec.Read(disclosure.Payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, (await RequireSessionAsync(context, cancellationToken)).Session);
        if (disclosure.IsTypedFailure && disclosure.EffectState == AgentToolEffectState.NotCommitted) {
            return CompletedCheck.Instance;
        }
        var lookup = await recovery.LookupAsync(source, disclosure.IntentId, disclosure.Payload.ToolName, input, cancellationToken);
        object? expected = lookup switch {
            ProjectDefinitionLookup.FoundScalars found => found.Result.Acknowledgement,
            ProjectDefinitionLookup.FoundFull found => Summary(found.Result, input.ParentProjectId.HasValue),
            _ => null
        };
        if (expected is null || disclosure.EffectState != AgentToolEffectState.Committed ||
            JsonSerializer.SerializeToElement(expected, ProjectDefinitionToolProposalCodec.Json).GetRawText() !=
                JsonSerializer.SerializeToElement(disclosure.Result.Deserialize(expected.GetType(), ProjectDefinitionToolProposalCodec.Json),
                    ProjectDefinitionToolProposalCodec.Json).GetRawText()) {
            throw Denied("The saved definition result cannot be disclosed as the exact currently authorized owner receipt.");
        }
        return CompletedCheck.Instance;
    }

    private async Task<AgentToolSessionObservation> RequireSessionAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken) {
        if (!UsesJournal(context) || context.AdmittedToolSession is null) {
            throw Denied("Definition mutation requires a verified recoverable Agent or Process execution.");
        }
        var observation = await admissions.RequireSessionObservationAsync(context.AdmittedToolSession, cancellationToken);
        if (observation.Session.Reference != context.AdmittedToolSession || observation.Session.AgentId != context.Agent.Id ||
            observation.Session.Profile.ProfileId != projects.DatabaseProfileId) {
            throw Denied("The definition tool does not belong to this saved execution and current profile.");
        }
        return observation;
    }

    private static void RequireOpen(ProjectDefinitionLookup lookup) {
        if (lookup is not ProjectDefinitionLookup.NotObserved { State: ProjectOperationObservationState.Open }) {
            throw ProjectStructureAgentException.CreateAgentVisible(409, ProjectErrorCodes.DefinitionReceiptUnavailable,
                "The original operation cannot admit a new effect. Reconcile its retained owner result before preparing another proposal.",
                canRetryWithCorrectedInput: false, effectState: AgentToolEffectState.Unknown);
        }
    }

    private static ProjectDefinitionAccepted<T> RequireAccepted<T>(ProjectDefinitionOutcome<T> result) where T : class => result switch {
        ProjectDefinitionOutcome<T>.Accepted accepted => accepted.Result,
        ProjectDefinitionOutcome<T>.Rejected rejected => throw ProjectStructureAgentException.CreateAgentVisible(409,
            rejected.Errors.FirstOrDefault()?.Code ?? ProjectErrorCodes.DefinitionConflict,
            string.Join(" ", rejected.Errors.Select(error => error.Message)), canRetryWithCorrectedInput: true, effectState: AgentToolEffectState.NotCommitted),
        _ => throw ProjectStructureAgentException.CreateAgentVisible(503, ProjectErrorCodes.DefinitionReceiptUnavailable,
            "The original definition outcome is unconfirmed. Reconcile this operation without creating another intent.",
            canRetryWithCorrectedInput: false, effectState: AgentToolEffectState.Unknown)
    };

    private static void RecordAccepted(ProjectDefinitionOperation operation)
        => AgentToolInvocationEffectScope.RecordCommitted(EffectSourceKind, operation.Id.ToString("D"));
    private static ProjectSummary Summary(ProjectDefinitionAccepted<ProjectEditorAcknowledgement> accepted, bool child) {
        var ack = accepted.Acknowledgement;
        return new(ack.Definition.Project.ProjectId, ack.Name, ack.Status, ack.CurrentPhase,
            ack.Phases.Count, child ? 1 : 0, 0, accepted.AcceptedAtUtc) { ExpectedProjectAdmission = ack.Definition.Project };
    }
    private static AgentToolAdmissionException Denied(string message) => ProjectDefinitionToolProposalCodec.Denied(message);
    private sealed record CapturedSource(AgentToolProtocolEnvelope Envelope);
    private sealed class CompletedCheck : IAsyncDisposable {
        internal static CompletedCheck Instance { get; } = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
