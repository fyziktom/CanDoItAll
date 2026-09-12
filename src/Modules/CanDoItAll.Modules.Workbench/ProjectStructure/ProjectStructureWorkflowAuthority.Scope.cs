using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureWorkflowAuthorityService {
    private static readonly JsonSerializerOptions ScopeJson = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions ExecutorScopeJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static WorkflowProjectLifetime ToWorkflowLifetime(ProjectWriteAdmission project)
        => new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId);

    internal static ProjectWriteAdmission ToProjectAdmission(WorkflowProjectLifetime project)
        => new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId);

    private ProjectWriteAdmissionService RequireProjectAdmissions() => projectAdmissions
        ?? throw new InvalidOperationException("Workflow project authority requires the Projects owner admission service.");

    private IAgentCatalogReadLeaseStore RequireCatalog() => catalog
        ?? throw new InvalidOperationException("Workflow Agent authority requires the held catalog read lease store.");

    private static WorkflowStructureProjectScope CaptureAgentProjectScope(AgentProjectStructureAccessSettings access,
        AgentExecutionGovernanceSnapshot governance, Guid projectId) {
        var projects = access.AllowedProjectLifetimes
            .Where(project => project.DatabaseProfileId == governance.DatabaseProfileId &&
                access.AllowedProjectIds.Contains(project.ProjectId) && (projectId == Guid.Empty || project.ProjectId == projectId))
            .Select(project => new WorkflowProjectLifetime(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)).ToArray();
        return new(projects, projectId != Guid.Empty && projects.Any(project => project.ProjectId == projectId) ? [projectId] : []);
    }

    public async Task<WorkflowStructureAuthority> CaptureAgentAsync(AgentDefinition agent, AgentExecutionGovernanceSnapshot governance,
        CancellationToken cancellationToken = default) {
        var authority = CaptureAgent(agent, governance);
        if (authority.ProjectId != Guid.Empty && authority.ProjectScope!.Find(authority.ProjectId) is null) {
            var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
            if (!access.AllowAllProjects) {
                throw Denied("The Agent's original grant has no exact lifetime for this Workflow project.");
            }
            var captured = await RequireProjectAdmissions().CaptureAsync(authority.ProjectId, cancellationToken)
                ?? throw Denied("The selected Workflow project no longer exists.");
            authority = authority with { ProjectScope = new([ToWorkflowLifetime(captured)], [captured.ProjectId]) };
        }
        authority.ProjectScope!.Validate(authority);
        return authority;
    }

    public async Task<WorkflowStructureAuthority> PrepareLaunchAsync(WorkflowStructureAuthority authority,
        WorkflowDefinition definition, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(definition);
        if (authority.ProjectScope is not { } scope) {
            return authority;
        }
        scope.Validate(authority);
        var fixedTargets = definition.Graph.Nodes.Where(node => node.Settings.ExecutorId == WorkflowExecutorIds.ProjectStructure)
            .Select(node => JsonSerializer.Deserialize<WorkflowProjectStructureExecutorSettings>(node.Settings.ExecutorSettingsJson, ExecutorScopeJson)
                ?? throw new InvalidOperationException("The saved Structure executor settings are missing."))
            .Where(settings => settings.Operation is WorkflowProjectStructureOperation.CreateTaskNodes or WorkflowProjectStructureOperation.CreateAsset
                or WorkflowProjectStructureOperation.ReadTree or WorkflowProjectStructureOperation.ReadNode)
            .Select(settings => settings.ProjectId).OfType<Guid>().Where(id => id != Guid.Empty).Distinct().Order().ToArray();
        if (fixedTargets.Length > ProjectRecordQueryLimits.MaximumReferenceCount) {
            throw new InvalidOperationException("The Workflow's explicit project targets exceed the supported bounded admission set.");
        }
        var missing = fixedTargets.Where(id => scope.Find(id) is null).ToArray();
        if (missing.Length > 0 && !authority.AllProjects) {
            throw Denied("An explicit Workflow target is outside the source's saved exact project grants.");
        }
        var captured = await RequireProjectAdmissions().CaptureManyAsync(missing, cancellationToken);
        if (captured.Count != missing.Length) {
            throw Denied("An explicit Workflow project no longer exists.");
        }
        return authority with { ProjectScope = new(scope.Projects.Concat(captured.Select(ToWorkflowLifetime)).ToArray(),
            scope.AdmissionProjectIds.Concat(fixedTargets).ToArray(), workflowStartCapabilityId: scope.WorkflowStartCapabilityId) };
    }

    internal async Task<WorkflowProjectLifetime> CaptureOutputTargetAsync(WorkflowStructureAuthority authority,
        Guid projectId, CancellationToken cancellationToken) {
        if (authority.ProjectScope is not { } scope) {
            throw new WorkflowStructureLegacyLineageException();
        }
        scope.Validate(authority);
        if (scope.Find(projectId) is { } original) {
            return original;
        }
        if (!authority.AllProjects) {
            throw Denied("The dynamic Workflow target is outside its original exact project grants.");
        }
        var target = await RequireProjectAdmissions().CaptureAsync(projectId, cancellationToken)
            ?? throw Denied("The dynamic Workflow project no longer exists.");
        return ToWorkflowLifetime(target);
    }

    internal async Task EnsureCurrentTargetAsync(WorkflowStructureAuthority authority, WorkflowStructureAuthorityUse use,
        WorkflowProjectLifetime target, CancellationToken cancellationToken) {
        await using var current = await AcquireAsync(authority, use, target, cancellationToken);
    }

    public async Task<IWorkflowStructureSourceAuthorityLease> AcquireAsync(WorkflowStructureAuthority authority,
        WorkflowStructureAuthorityUse use, WorkflowProjectLifetime? target = null, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(authority);
        if (authority.ProjectScope is null) {
            throw new WorkflowStructureLegacyLineageException();
        }
        IAgentCatalogReadLease? held = null;
        try {
            var dispatch = await ReadProcessDispatchAsync(authority, use, cancellationToken);
            if (authority.ProcessAuthority?.ToolInvocation is { } tool) {
                var ids = authority.AgentGovernance is { } original
                    ? new[] { original.AgentId, tool.ExecutorAgentId }.Distinct().ToArray() : new[] { tool.ExecutorAgentId };
                held = await RequireCatalog().AcquireAgentsReadLeaseAsync(ids, cancellationToken);
            } else if (authority.Channel == WorkflowStructureAuthorityChannel.AgentExecution) {
                var governance = authority.AgentGovernance ?? throw Denied("The Workflow source has no saved Agent governance.");
                held = await RequireCatalog().AcquireAgentReadLeaseAsync(governance.AgentId, cancellationToken);
            }
            RequireCurrentSource(authority, use, target, held);
            RequireHeldProcessSource(dispatch, held, use);
            return new WorkflowSourceLease(this, authority, use, target, held, dispatch);
        } catch {
            if (held is not null) {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    private void RequireCurrentSource(WorkflowStructureAuthority authority, WorkflowStructureAuthorityUse use,
        WorkflowProjectLifetime? target, IAgentCatalogReadLease? held, bool checkAdmissionTargets = true) {
        var scope = authority.ProjectScope ?? throw new WorkflowStructureLegacyLineageException();
        scope.Validate(authority);
        if (!Enum.IsDefined(use) || !Enum.IsDefined(authority.Channel) || !Enum.IsDefined(authority.OperatorSurface) ||
                authority.DatabaseProfileId != canonicalDatabase.Profile.Profile.Id ||
                authority.ExpiresAtUtc <= timeProvider.GetUtcNow() || string.IsNullOrWhiteSpace(authority.PolicyFingerprint) ||
                target is not null && (target.DatabaseProfileId != authority.DatabaseProfileId ||
                    scope.Find(target.ProjectId) is { } saved && saved != target ||
                    scope.Find(target.ProjectId) is null && !authority.AllProjects) ||
                use == WorkflowStructureAuthorityUse.TaskOutput && (!authority.CanCreateTasks || target is null) ||
                use == WorkflowStructureAuthorityUse.AssetOutput && (!authority.CanCreateAssets || target is null)) {
            throw Denied("The Workflow's original authority does not permit this exact project operation.");
        }
        if (authority.ProcessAuthority?.ToolInvocation is { } tool) {
            RequireProcessToolExecutor(tool, held, use == WorkflowStructureAuthorityUse.Disclosure);
        }
        if (authority.Channel != WorkflowStructureAuthorityChannel.AgentExecution) {
            RequireCurrentOperator(authority);
            return;
        }
        var governance = authority.AgentGovernance;
        var agent = held?.Agent;
        if (governance is null || held is null || agent is null ||
                held.Scope != WorkspaceScopeDescriptor.Organization(authority.DatabaseProfileId.ToString("N")) ||
                authority.Principal.Kind != WorkflowLaunchActorKind.Agent ||
                !Guid.TryParse(authority.Principal.SubjectId, out var sourceId) || sourceId != governance.AgentId || agent.Id != sourceId ||
                agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active || !agent.Permissions.CanUseTools ||
                governance.DatabaseProfileId != authority.DatabaseProfileId || governance.DatabaseProfileGeneration !=
                    (generations ?? throw new InvalidOperationException("Workflow authority requires the current profile generation.")).GetGeneration() ||
                governance.PolicyFingerprint != authority.PolicyFingerprint ||
                !governance.ReadAllowed || use != WorkflowStructureAuthorityUse.Disclosure && !governance.MutationAllowed ||
                use == WorkflowStructureAuthorityUse.Schedule && (!agent.Permissions.CanScheduleWork || !scheduledAuthority.AllowsScheduling(governance)) ||
                authority.SchedulerAuthority is not null && use != WorkflowStructureAuthorityUse.Disclosure && !agent.Permissions.CanScheduleWork) {
            throw Denied("The original Workflow Agent source is no longer admitted in this profile generation.");
        }
        if (use != WorkflowStructureAuthorityUse.Disclosure && scope.WorkflowStartCapabilityId is { } capabilityId &&
                (agent.Capabilities.Count(item => item.CapabilityKey == WorkflowRuntimeCapabilityKeys.RunStart) != 1 ||
                    !agent.Capabilities.Any(item => item.CapabilityId == capabilityId && item.CapabilityKey == WorkflowRuntimeCapabilityKeys.RunStart && item.Kind == CapabilityKind.Tool) ||
                    held.Capabilities.Count(item => item.Id == capabilityId && item.Key == WorkflowRuntimeCapabilityKeys.RunStart && item.Kind == CapabilityKind.Tool) != 1 ||
                    governance.AllowedCapabilityKeys.Count > 0 && !governance.AllowedCapabilityKeys.Contains(WorkflowRuntimeCapabilityKeys.RunStart))) {
            throw Denied("The original Workflow launch capability is no longer assigned or present in the held catalog.");
        }
        var required = target is not null ? new[] { target } : checkAdmissionTargets
            ? scope.Projects.Where(project => scope.AdmissionProjectIds.Contains(project.ProjectId)).ToArray() : [];
        var projectFreeSandbox = governance.WorkspaceScope.IsDefaultSandbox &&
            use is WorkflowStructureAuthorityUse.Admission or WorkflowStructureAuthorityUse.Schedule or WorkflowStructureAuthorityUse.Disclosure &&
            target is null && authority.ProjectId == Guid.Empty && !authority.AllProjects && authority.ProjectIds.Count == 0 &&
            scope.Projects.Count == 0 && scope.AdmissionProjectIds.Count == 0 && !authority.CanCreateTasks && !authority.CanCreateAssets;
        if (governance.WorkspaceScope.Kind == WorkspaceScopeKind.Project &&
                (!Guid.TryParse(governance.WorkspaceScope.Key, out var projectId) || required.Any(project => project.ProjectId != projectId)) ||
                governance.WorkspaceScope.Kind == WorkspaceScopeKind.Organization &&
                    (!Guid.TryParse(governance.WorkspaceScope.Key, out var profileId) || profileId != authority.DatabaseProfileId) ||
                governance.WorkspaceScope.Kind is not (WorkspaceScopeKind.Project or WorkspaceScopeKind.Organization) && !projectFreeSandbox) {
            throw Denied("The selected Workflow projects exceed the original execution workspace scope.");
        }
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        var operation = use switch {
            WorkflowStructureAuthorityUse.TaskOutput => ProjectStructureToolPolicy.ProjectStructureNodeCreate,
            WorkflowStructureAuthorityUse.AssetOutput => ProjectStructureToolPolicy.ProjectStructureAssetCreate,
            WorkflowStructureAuthorityUse.StructureAdmission or WorkflowStructureAuthorityUse.StatusProjection => ProjectStructureToolPolicy.ProjectStructureNodeWorkflowStart,
            _ => null
        };
        if (required.Length > 0 && !access.CanRead || required.Any(project => !access.AllowAllProjects &&
                (!access.AllowedProjectIds.Contains(project.ProjectId) ||
                    !access.AllowedProjectLifetimes.Contains(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)))) ||
                use == WorkflowStructureAuthorityUse.TaskOutput && !ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(access) ||
                use == WorkflowStructureAuthorityUse.AssetOutput && !ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access) ||
                use is WorkflowStructureAuthorityUse.StructureAdmission or WorkflowStructureAuthorityUse.StatusProjection &&
                    !ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access) ||
                authority.ProcessAuthority is { ToolInvocation: null } && use != WorkflowStructureAuthorityUse.Disclosure &&
                    !ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access) ||
                authority.ProcessAuthority is null && operation is not null && governance.AllowedOperations.Count > 0 && !governance.AllowedOperations.Contains(operation)) {
            throw Denied("Current Agent policy no longer permits the Workflow's exact original project lifetime and operation.");
        }
    }

    private void RequireCurrentOperator(WorkflowStructureAuthority authority) {
        var options = apiOptions.CurrentValue;
        if (authority.Principal.Kind != WorkflowLaunchActorKind.User ||
                authority.PolicyFingerprint != OperatorPolicyFingerprint(authority.OperatorSurface) ||
                authority.OperatorSurface == WorkflowStructureOperatorSurface.Api && (!options.Enabled ||
                    authority.Channel == WorkflowStructureAuthorityChannel.LocalOperator && options.Authorization.Enabled ||
                    authority.Channel == WorkflowStructureAuthorityChannel.AuthenticatedOperator &&
                        (!options.Authorization.Enabled || !authority.ExpiresAtUtc.HasValue)) ||
                authority.Channel == WorkflowStructureAuthorityChannel.AuthenticatedOperator &&
                    authority.OperatorSurface != WorkflowStructureOperatorSurface.Api) {
            throw Denied("The original Workflow operator authority expired or its current API policy changed.");
        }
    }

    private async Task RequireHeldForMutationAsync(WorkflowStructureAuthority authority, WorkflowStructureAuthorityUse use,
        WorkflowProjectLifetime? target, IAgentCatalogReadLease? held, ProcessExecutionDispatchAuthority? dispatch,
        CancellationToken cancellationToken) {
        if (use == WorkflowStructureAuthorityUse.Disclosure) {
            throw new InvalidOperationException("A Workflow disclosure lease cannot authorize a mutation.");
        }
        RequireCurrentSource(authority, use, target, held);
        RequireHeldProcessSource(dispatch, held, use);
        if (dispatch is not null) {
            var guard = processGuard ?? throw new InvalidOperationException("Workflow Process effects require the actual Process owner mutation guard.");
            if (use == WorkflowStructureAuthorityUse.Admission && authority.ProcessAuthority?.ToolInvocation is not null) {
                await guard.RequireDispatchAsync(dispatch, cancellationToken);
            } else {
                await guard.RequireForMutationAsync(dispatch, cancellationToken);
            }
        }
        if (authority.SchedulerAuthority is { } schedule) {
            await scheduledAuthority.RequireCurrentForMutationAsync(schedule, cancellationToken);
        }
        var scope = authority.ProjectScope!;
        var required = scope.Projects.Where(project => scope.AdmissionProjectIds.Contains(project.ProjectId))
            .Concat(target is null ? [] : new[] { target }).Distinct().Select(ToProjectAdmission).ToArray();
        await RequireProjectAdmissions().RequireManyUnderMutationGatesAsync(required, cancellationToken);
    }

    private void RequireHeldProcessSource(ProcessExecutionDispatchAuthority? dispatch, IAgentCatalogReadLease? held,
        WorkflowStructureAuthorityUse use) {
        if (dispatch?.SourceAuthority is { } original) {
            (processObservation as ProjectProcessLaunchAuthorityService
                ?? throw new InvalidOperationException("Workflow Process effects require the original source owner's held catalog policy."))
                .RequireHeldWorkflowSource(original, held, use != WorkflowStructureAuthorityUse.Disclosure);
        }
    }

    private sealed class WorkflowSourceLease(ProjectStructureWorkflowAuthorityService owner, WorkflowStructureAuthority authority,
        WorkflowStructureAuthorityUse use, WorkflowProjectLifetime? target, IAgentCatalogReadLease? held,
        ProcessExecutionDispatchAuthority? dispatch) : IWorkflowStructureSourceAuthorityLease {
        private int disposed;

        public Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            return owner.RequireHeldForMutationAsync(authority, use, target, held, dispatch, cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && held is not null) {
                await held.DisposeAsync();
            }
        }
    }
}
