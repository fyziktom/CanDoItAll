using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectProcessLaunchAuthorityService(
    ICanonicalRuntimeDatabase database,
    IAgentCatalogReadLeaseStore catalog,
    IAgentExecutionProfileGenerationSource generations,
    ProjectWriteAdmissionService projectAdmissions,
    ProjectProcessLaunchTargetQuery targets,
    IOptionsMonitor<ApiAccessOptions> apiOptions,
    TimeProvider clock) : IProcessLaunchAuthorityPolicy, IProcessLaunchOperatorAuthoritySource {
    public async Task<ProcessLaunchAuthority> CaptureLocalAsync(Guid? projectId, ProcessLaunchOperatorSurface surface,
        CancellationToken cancellationToken = default) {
        var authority = new ProcessLaunchAuthority(new ProcessLaunchPrincipal.LocalOperator(surface), database.Profile.Profile.Id,
            await CaptureProjectAsync(projectId, cancellationToken), true, true, OperatorFingerprint(surface));
        await RequireCurrentAsync(authority, cancellationToken);
        return authority;
    }

    public async Task<ProcessLaunchAuthority> CaptureAuthenticatedAsync(Guid? projectId, string subjectId,
        DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default) {
        var authority = new ProcessLaunchAuthority(new ProcessLaunchPrincipal.AuthenticatedOperator(subjectId, expiresAtUtc.ToUniversalTime()),
            database.Profile.Profile.Id, await CaptureProjectAsync(projectId, cancellationToken), true, true,
            OperatorFingerprint(ProcessLaunchOperatorSurface.Api));
        await RequireCurrentAsync(authority, cancellationToken);
        return authority;
    }

    public async Task<ProcessLaunchAuthority> CaptureAgentAsync(ProcessProjectAdmission expectedProject,
        AgentExecutionGovernanceSnapshot governance, ProcessLaunchAgentOperation operation,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expectedProject);
        ArgumentNullException.ThrowIfNull(governance);
        if (governance.DatabaseProfileId != database.Profile.Profile.Id || expectedProject.DatabaseProfileId != governance.DatabaseProfileId) {
            throw Denied("The process source authority belongs to a different database profile.");
        }
        await using var held = await catalog.AcquireAgentReadLeaseAsync(governance.AgentId, cancellationToken);
        RequireCatalogScope(held);
        var agent = held.Agent ?? throw Denied("The process source Agent no longer exists.");
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        var ceiling = new ProcessLaunchAgentCeiling(governance.AuthorityId.Value, governance.AgentId,
            governance.DatabaseProfileGeneration.Value, governance.WorkspaceScope.Kind switch {
                WorkspaceScopeKind.Organization => ProcessLaunchSourceScopeKind.Organization,
                WorkspaceScopeKind.Project => ProcessLaunchSourceScopeKind.Project,
                _ => throw Denied("This Agent authority scope cannot admit a project Process launch.")
            }, governance.WorkspaceScope.Key, governance.ReadAllowed, governance.MutationAllowed,
            governance.PolicyVersion, governance.PolicyFingerprint, governance.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            governance.AllowedCapabilityKeys.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            governance.WritableExternalTargetAliases.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            governance.ReadOnlyExternalTargetAliases.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            governance.AllowedManagedArtifactReadRefs.Order(StringComparer.OrdinalIgnoreCase).ToArray());
        var authority = new ProcessLaunchAuthority(new ProcessLaunchPrincipal.AgentExecution(ceiling, operation),
            governance.DatabaseProfileId, expectedProject,
            governance.MutationAllowed && ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(access) &&
                Allows(ceiling, ProjectStructureToolPolicy.ProjectStructureNodeCreate),
            governance.MutationAllowed && ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access) &&
                Allows(ceiling, ProjectStructureToolPolicy.ProjectStructureAssetCreate), governance.PolicyFingerprint);
        RequireSource(authority, authority, held);
        await projectAdmissions.RequireCurrentAsync(ToProject(expectedProject), cancellationToken);
        return authority;
    }

    public async Task<IProcessLaunchAuthorityLease> AcquireAsync(ProcessLaunchAuthority saved, ProcessLaunchAuthority currentCaller,
        CancellationToken cancellationToken = default) {
        RequireCommon(saved, currentCaller);
        IAgentCatalogReadLease? held = null;
        try {
            if (saved.Principal is ProcessLaunchPrincipal.AgentExecution source) {
                held = await catalog.AcquireAgentReadLeaseAsync(source.Ceiling.AgentId, cancellationToken);
            }
            RequireSource(saved, currentCaller, held);
            return new AuthorityLease(this, saved, currentCaller, held);
        } catch {
            if (held is not null) {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    public async Task RequireCurrentAsync(ProcessLaunchAuthority authority, CancellationToken cancellationToken = default) {
        await using var lease = await AcquireAsync(authority, authority, cancellationToken);
        if (authority.ProjectAdmission is { } project) {
            await projectAdmissions.RequireCurrentAsync(ToProject(project), cancellationToken);
        }
    }

    private async Task<ProcessProjectAdmission?> CaptureProjectAsync(Guid? projectId, CancellationToken cancellationToken) {
        if (projectId is null) {
            return null;
        }
        var captured = await projectAdmissions.CaptureAsync(projectId.Value, cancellationToken)
            ?? throw Denied("The process launch project is no longer available.");
        return new(captured.DatabaseProfileId, captured.ProjectId, captured.LifetimeId);
    }

    private void RequireCommon(ProcessLaunchAuthority saved, ProcessLaunchAuthority caller) {
        saved.Validate();
        caller.Validate();
        if (saved.DatabaseProfileId != database.Profile.Profile.Id || caller.DatabaseProfileId != saved.DatabaseProfileId ||
                caller.ProjectAdmission != saved.ProjectAdmission ||
                ProcessLaunchIntentFingerprint.CallerFingerprint(saved) != ProcessLaunchIntentFingerprint.CallerFingerprint(caller) ||
                saved.CanCreateTasks && !caller.CanCreateTasks || saved.CanCreateAssets && !caller.CanCreateAssets) {
            throw Denied("The saved process source or grant ceiling no longer matches this caller.");
        }
    }

    private void RequireSource(ProcessLaunchAuthority saved, ProcessLaunchAuthority caller, IAgentCatalogReadLease? held) {
        RequireCommon(saved, caller);
        if (saved.Principal is not ProcessLaunchPrincipal.AgentExecution source) {
            RequireOperator(saved);
            RequireOperator(caller);
            return;
        }
        if (caller.Principal is not ProcessLaunchPrincipal.AgentExecution current || held is null ||
                source.Operation != current.Operation || saved.ProjectAdmission is not { } project) {
            throw Denied("The process Agent source has no matching held project authority.");
        }
        RequireCatalogScope(held);
        RequireCeiling(source, project);
        RequireCeiling(current, project);
        var agent = held.Agent;
        if (agent is null || agent.Id != source.Ceiling.AgentId || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active ||
                !agent.Permissions.CanUseTools) {
            throw Denied("The process source Agent is no longer active or permitted to use tools.");
        }
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (!access.CanRead || !ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access) ||
                !access.AllowAllProjects && (!access.AllowedProjectIds.Contains(project.ProjectId) ||
                    !access.AllowedProjectLifetimes.Contains(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId))) ||
                saved.CanCreateTasks && (!ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(access) ||
                    !Allows(source.Ceiling, ProjectStructureToolPolicy.ProjectStructureNodeCreate) ||
                    !Allows(current.Ceiling, ProjectStructureToolPolicy.ProjectStructureNodeCreate)) ||
                saved.CanCreateAssets && (!Allows(source.Ceiling, ProjectStructureToolPolicy.ProjectStructureAssetCreate) ||
                    !Allows(current.Ceiling, ProjectStructureToolPolicy.ProjectStructureAssetCreate))) {
            throw Denied("Current Agent policy does not grant this exact project lifetime and Process operation.");
        }
    }

    private void RequireCeiling(ProcessLaunchPrincipal.AgentExecution source, ProcessProjectAdmission project) {
        var ceiling = source.Ceiling;
        var operation = source.Operation switch {
            ProcessLaunchAgentOperation.StructureStart => ProjectStructureToolPolicy.ProjectStructureNodeProcessStart,
            ProcessLaunchAgentOperation.SubprocessLaunch => ProjectStructureToolPolicy.ProjectStructureProcessSubprocessLaunch,
            _ => throw Denied("The process Agent source has an unsupported launch operation.")
        };
        if (!ceiling.ReadAllowed || !ceiling.MutationAllowed || ceiling.DatabaseProfileGeneration != generations.GetGeneration().Value ||
                !Allows(ceiling, operation) ||
                ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Project &&
                    (!Guid.TryParse(ceiling.WorkspaceScopeKey, out var sourceProject) || sourceProject != project.ProjectId) ||
                ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Organization &&
                    (!Guid.TryParse(ceiling.WorkspaceScopeKey, out var sourceProfile) || sourceProfile != project.DatabaseProfileId) ||
                ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Sandbox) {
            throw Denied("The admitted Agent ceiling does not permit this Process launch in the current profile generation.");
        }
    }

    private void RequireCatalogScope(IAgentCatalogReadLease held) {
        if (held.Scope != WorkspaceScopeDescriptor.Organization(database.Profile.Profile.Id.ToString("N"))) {
            throw Denied("The held Agent catalog belongs to a different profile or workspace scope.");
        }
    }

    private void RequireOperator(ProcessLaunchAuthority authority) {
        var options = apiOptions.CurrentValue;
        var surface = authority.Principal switch {
            ProcessLaunchPrincipal.LocalOperator local => local.Surface,
            ProcessLaunchPrincipal.AuthenticatedOperator => ProcessLaunchOperatorSurface.Api,
            _ => throw Denied("The Process operator authority has an unsupported source channel.")
        };
        if (authority.PolicyFingerprint != OperatorFingerprint(surface) ||
                surface == ProcessLaunchOperatorSurface.Api && (!options.Enabled ||
                    authority.Principal is ProcessLaunchPrincipal.LocalOperator && options.Authorization.Enabled ||
                    authority.Principal is ProcessLaunchPrincipal.AuthenticatedOperator authenticated &&
                        (!options.Authorization.Enabled || authenticated.ExpiresAtUtc <= clock.GetUtcNow()))) {
            throw Denied("The admitted Process operator authority has expired or its current policy denies the operation.");
        }
    }

    private string OperatorFingerprint(ProcessLaunchOperatorSurface surface) {
        if (surface == ProcessLaunchOperatorSurface.UserInterface) {
            return Hash("process-local-ui-v1");
        }
        var options = apiOptions.CurrentValue;
        return Hash(JsonSerializer.Serialize(new {
            options.Enabled,
            AuthorizationEnabled = options.Authorization.Enabled,
            options.Authorization.Issuer,
            options.Authorization.Audience,
            SigningKeyFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.Authorization.SigningKey)))
        }));
    }

    private async Task RequireForMutationAsync(ProcessLaunchAuthority saved, ProcessLaunchAuthority caller,
        IAgentCatalogReadLease? held, ProcessLaunchLinkTarget? linkTarget, CancellationToken cancellationToken) {
        RequireSource(saved, caller, held);
        if (saved.ProjectAdmission is { } project) {
            await projectAdmissions.RequireForMutationAsync(ToProject(project), cancellationToken);
        }
        if (linkTarget is not null) {
            if (saved.ProjectAdmission?.ProjectId != linkTarget.ProjectId) {
                throw Denied("The prepared native target belongs to a different admitted project.");
            }
            await targets.RequireForMutationAsync(linkTarget, cancellationToken);
        }
    }

    private static bool Allows(ProcessLaunchAgentCeiling ceiling, string operation)
        => ceiling.AllowedOperations.Count == 0 || ceiling.AllowedOperations.Contains(operation, StringComparer.OrdinalIgnoreCase);

    private static ProjectWriteAdmission ToProject(ProcessProjectAdmission project)
        => new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId);

    private static string Hash(string value) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static ProcessLaunchAuthorityRejectedException Denied(string message) => new(message);

    private sealed class AuthorityLease(ProjectProcessLaunchAuthorityService owner, ProcessLaunchAuthority saved,
        ProcessLaunchAuthority caller, IAgentCatalogReadLease? held) : IProcessLaunchAuthorityLease {
        private int disposed;

        public Task RequireForMutationAsync(ProcessLaunchLinkTarget? linkTarget, CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            return owner.RequireForMutationAsync(saved, caller, held, linkTarget, cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && held is not null) {
                await held.DisposeAsync();
            }
        }
    }
}
