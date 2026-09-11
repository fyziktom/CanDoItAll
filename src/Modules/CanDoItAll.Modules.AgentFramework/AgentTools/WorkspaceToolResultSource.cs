using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkspaceToolResultSource(IAgentToolAdmissionVerifier admissions,
    IAgentExecutionAuthorityResolver authorityResolver, IAgentCatalogReadLeaseStore catalogLeases,
    ICanonicalRuntimeDatabase database, IDatabaseRuntimeState runtimeState, ProjectWriteAdmissionService projects)
    : IAgentWorkspaceToolResultSource {
    public async ValueTask<AgentToolProtocolEnvelope> CaptureAsync(AgentRuntimeToolProviderContext context,
        WorkspaceScopeDescriptor workspaceScope, CancellationToken cancellationToken = default) {
        var observation = await RequireObservationAsync(context, workspaceScope, cancellationToken);
        var executionScope = WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(
            observation.Session.Reference.ExecutionRunId, observation.Session.AgentId, workspaceScope);
        var scopes = new[] { workspaceScope, executionScope }.OfType<WorkspaceScopeDescriptor>()
            .Where(scope => scope.Kind == WorkspaceScopeKind.Project).Distinct().ToArray();
        var original = ImmutableArray.CreateBuilder<ProjectWriteAdmission>();
        var state = WorkspaceToolSourceState.Complete;
        foreach (var scope in scopes) {
            var admission = Guid.TryParse(scope.Key, out var id) && id != Guid.Empty
                ? await projects.CaptureAsync(id, cancellationToken) : null;
            if (admission is null || admission.DatabaseProfileId != observation.Session.Profile.ProfileId) {
                state = WorkspaceToolSourceState.MissingOriginalLifetime;
            } else {
                original.Add(admission);
            }
        }
        return WorkspaceToolSourceEvidence.Write(new(observation.Session.Reference, observation.Session.AgentId,
            observation.Session.Profile.ProfileId, workspaceScope, state, original.ToImmutable(), executionScope));
    }

    public async ValueTask<AgentToolProtocolEnvelope> CompleteAsync(AgentToolProtocolEnvelope original,
        CancellationToken cancellationToken = default) {
        var evidence = WorkspaceToolSourceEvidence.Read(original);
        try {
            if (WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(evidence.Session.ExecutionRunId,
                    evidence.AgentId, evidence.WorkspaceScope) != evidence.ExecutionWorkspaceScope) {
                evidence = evidence with { State = WorkspaceToolSourceState.ExecutionScopeChangedDuringOperation };
            }
        } catch (AgentToolAdmissionException) {
            evidence = evidence with { State = WorkspaceToolSourceState.ExecutionScopeChangedDuringOperation };
        }
        foreach (var admission in evidence.Projects) {
            if (await projects.CaptureAsync(admission.ProjectId, cancellationToken) != admission) {
                evidence = evidence with { State = WorkspaceToolSourceState.LifetimeChangedDuringOperation };
                break;
            }
        }
        return WorkspaceToolSourceEvidence.Write(evidence);
    }

    public async ValueTask<IAgentWorkspaceToolResultReadLease> AcquireReadAsync(AgentRuntimeToolProviderContext context,
        WorkspaceScopeDescriptor workspaceScope, AgentToolProtocolEnvelope original,
        CancellationToken cancellationToken = default) {
        var observation = await RequireObservationAsync(context, workspaceScope, cancellationToken);
        WorkspaceToolSourceEvidence evidence;
        try {
            evidence = WorkspaceToolSourceEvidence.Read(original);
        } catch (Exception exception) when (exception is InvalidDataException or JsonException or ArgumentException) {
            throw Unavailable();
        }
        var session = observation.Session;
        if (evidence.State != WorkspaceToolSourceState.Complete) {
            throw Unavailable();
        }
        if (evidence.Session != session.Reference || evidence.AgentId != session.AgentId ||
                evidence.ProfileId != session.Profile.ProfileId || evidence.WorkspaceScope != workspaceScope ||
                evidence.ExecutionWorkspaceScope != WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(
                    session.Reference.ExecutionRunId, session.AgentId, workspaceScope)) {
            throw Denied();
        }
        var held = await catalogLeases.AcquireAgentReadLeaseAsync(session.AgentId, cancellationToken);
        try {
            var actor = held.Agent;
            if (held.Scope != WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N")) ||
                    actor is null || actor.Id != session.AgentId || actor.IsTemplate || actor.Status != AgentLifecycleStatus.Active ||
                    !actor.Permissions.CanUseTools || held.Capabilities.IsDefault) {
                throw Denied();
            }
            var access = AgentProjectStructureAccessMetadata.Read(actor.ConfigurationJson);
            foreach (var admission in evidence.Projects) {
                if (!access.CanRead || !access.AllowAllProjects &&
                        (!access.AllowedProjectIds.Contains(admission.ProjectId) || !access.AllowedProjectLifetimes.Contains(
                            new(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId))) ||
                        await projects.CaptureAsync(admission.ProjectId, cancellationToken) != admission) {
                    throw Denied();
                }
            }
            RequireProfile(session.Profile);
            return new ReadLease(this, held, session.Profile);
        } catch {
            await held.DisposeAsync();
            throw;
        }
    }

    private async Task<AgentToolSessionObservation> RequireObservationAsync(AgentRuntimeToolProviderContext context,
        WorkspaceScopeDescriptor workspaceScope, CancellationToken cancellationToken) {
        if (context.AdmittedToolSession is null || context.ToolAdmissionSupport != AgentToolAdmissionSupport.Recoverable) {
            throw Denied();
        }
        var observation = await admissions.RequireSessionObservationAsync(context.AdmittedToolSession, cancellationToken);
        var session = observation.Session;
        RequireProfile(session.Profile);
        if (session.Reference != context.AdmittedToolSession || session.AgentId != context.Agent.Id ||
                context.ContextIntent.WorkspaceScope is { } requestedScope && requestedScope != workspaceScope) {
            throw Denied();
        }
        if (session.Reference.BackgroundSource is not null) {
            if (session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                    context.Purpose != AgentRuntimeToolProviderPurpose.GovernedProcessAutomation) {
                throw Denied();
            }
            return observation;
        }
        var original = observation.Governance;
        var source = observation.TurnContext;
        if (session.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
                context.Purpose != AgentRuntimeToolProviderPurpose.InteractiveChat || source is null ||
                original is not { ReadAllowed: true } || original.AuthorityId != session.Reference.AuthorityId ||
                original.AgentId != session.AgentId || original.DatabaseProfileId != session.Profile.ProfileId ||
                original.DatabaseProfileGeneration != session.Profile.Generation ||
                original.WorkspaceScope is { } savedScope && savedScope != workspaceScope ||
                context.Governance is not { } supplied || supplied.AuthorityId != original.AuthorityId ||
                supplied.PolicyFingerprint != original.PolicyFingerprint || supplied.WorkspaceScope != original.WorkspaceScope) {
            throw Denied();
        }
        AgentExecutionAuthorityRecord current;
        try {
            current = await authorityResolver.ResolveAsync(AgentExecutionAuthorityResolutionRequest.FromCaptured(source, original), cancellationToken);
        } catch (Exception exception) when (exception is AgentExecutionAuthorityMismatchException or AgentChatContextAccessDeniedException) {
            throw Denied();
        }
        if (!current.ReadAllowed || current.AgentId != session.AgentId || current.DatabaseProfileId != session.Profile.ProfileId ||
                current.DatabaseProfileGeneration != session.Profile.Generation || current.WorkspaceScope != original.WorkspaceScope) {
            throw Denied();
        }
        RequireProfile(session.Profile);
        return observation;
    }

    private void RequireProfile(AgentToolProfileBinding original) {
        var current = runtimeState.GetSnapshot();
        if (original.ProfileId != database.Profile.Profile.Id || original.ProfileId != current.ActiveProfileId ||
                original.Fingerprint != current.ActiveFingerprint || original.Generation.Value != current.Generation) {
            throw Denied();
        }
    }

    private static AgentToolAdmissionException Denied() => new("workspace.result-disclosure-denied",
        "Current read authority does not permit the saved workspace source and result.");
    private static AgentToolAdmissionException Unavailable() => new("workspace.result-authority-unavailable",
        "The saved workspace result lacks supported original source authority. Recovery cannot disclose or repeat it.");

    private sealed class ReadLease(WorkspaceToolResultSource source, IAgentCatalogReadLease held,
        AgentToolProfileBinding profile) : IAgentWorkspaceToolResultReadLease {
        public AgentDefinition Agent => held.Agent ?? throw Denied();
        public ImmutableArray<CapabilityCatalogItem> Capabilities => held.Capabilities;
        public void RequireCurrent() {
            _ = held.Revision;
            source.RequireProfile(profile);
        }
        public ValueTask DisposeAsync() => held.DisposeAsync();
    }
}

internal enum WorkspaceToolSourceState {
    Complete,
    MissingOriginalLifetime,
    LifetimeChangedDuringOperation,
    ExecutionScopeChangedDuringOperation
}

internal sealed record WorkspaceToolSourceEvidence(AgentToolSessionReference Session, Guid AgentId, Guid ProfileId,
    WorkspaceScopeDescriptor WorkspaceScope, WorkspaceToolSourceState State, ImmutableArray<ProjectWriteAdmission> Projects,
    WorkspaceScopeDescriptor? ExecutionWorkspaceScope = null) {
    private const string Format = "workspace-tool-result-source";
    private const int Version = 1;

    internal static AgentToolProtocolEnvelope Write(WorkspaceToolSourceEvidence evidence) {
        Validate(evidence);
        return AgentToolProtocolEnvelope.Create(Format, Version, JsonSerializer.Serialize(evidence));
    }

    internal static WorkspaceToolSourceEvidence Read(AgentToolProtocolEnvelope envelope) {
        if (envelope.Format != Format || envelope.Version != Version) {
            throw new InvalidDataException("The workspace result source format is unsupported.");
        }
        var evidence = JsonSerializer.Deserialize<WorkspaceToolSourceEvidence>(envelope.PayloadJson)
            ?? throw new InvalidDataException("The workspace result source is absent.");
        Validate(evidence);
        return evidence;
    }

    private static void Validate(WorkspaceToolSourceEvidence evidence) {
        if (evidence.Session is null || evidence.AgentId == Guid.Empty || evidence.ProfileId == Guid.Empty ||
                evidence.WorkspaceScope is null || !Enum.IsDefined(evidence.State) || evidence.Projects.IsDefault ||
                evidence.Projects.Length > 2 || evidence.Projects.Any(item => item is null || item.DatabaseProfileId != evidence.ProfileId) ||
                evidence.Projects.Select(item => item.ProjectId).Distinct().Count() != evidence.Projects.Length) {
            throw new InvalidDataException("The workspace result source is invalid.");
        }
        if (evidence.State != WorkspaceToolSourceState.Complete) {
            return;
        }
        foreach (var scope in new[] { evidence.WorkspaceScope, evidence.ExecutionWorkspaceScope }.OfType<WorkspaceScopeDescriptor>()
                .Where(scope => scope.Kind == WorkspaceScopeKind.Project)) {
            if (!Guid.TryParse(scope.Key, out var id) || evidence.Projects.Count(item => item.ProjectId == id) != 1) {
                throw new InvalidDataException("The workspace result source lacks its exact original project lifetime.");
            }
        }
    }
}
