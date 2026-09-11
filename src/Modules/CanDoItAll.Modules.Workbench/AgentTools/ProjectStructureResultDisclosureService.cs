using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureResultDisclosureService(
    ProjectWriteAdmissionService projects,
    ProjectWriteSelectionQuery selections,
    ICanonicalRuntimeDatabase database,
    IDatabaseRuntimeState runtimeState,
    IAgentToolAdmissionVerifier admissions,
    ISandboxWorkspaceCatalogStore catalog,
    IAgentCatalogReadLeaseStore catalogLeases,
    IAgentExecutionAuthorityResolver authorityResolver,
    ProjectProcessExecutionMutationService? processAccess = null) {
    internal static bool UsesJournal(AgentRuntimeToolProviderContext context)
        => context.AdmittedToolSession is not null && context.ToolAdmissionSupport == AgentToolAdmissionSupport.Recoverable;

    internal static bool Handles(AgentRuntimeToolProviderContext context, string toolName)
        => toolName != ProjectStructureToolPolicy.ProjectStructureNodeProcessStart &&
            !(context.Purpose == AgentRuntimeToolProviderPurpose.GovernedProcessAutomation &&
                context.AdmittedToolSession?.BackgroundSource is not null &&
                toolName is ProjectStructureToolPolicy.ProjectStructureAssetCreate or ProjectStructureToolPolicy.ProjectStructureAssetCreateRevision);

    internal IReadOnlyList<AITool> Wrap(AgentRuntimeToolProviderContext context, IReadOnlyList<AITool> tools,
        ProjectWriteAdmission? sourceAdmission) => tools.Select(tool =>
            tool is AIFunction function && Handles(context, tool.Name)
                ? (AITool)new DisclosureFunction(function, this, context, sourceAdmission)
                : tool).ToArray();

    internal IReadOnlyList<AgentRuntimeToolMetadata> Metadata(AgentRuntimeToolProviderContext context, string providerKey)
        => ProjectStructureToolPolicy.Capabilities.Where(policy => Handles(context, policy.Name)).Select(policy =>
            new AgentRuntimeToolMetadata(providerKey, policy.Name,
                policy.IsStateChanging ? AgentRuntimeToolOperationKind.Mutation : AgentRuntimeToolOperationKind.Read,
                policy.RequiresApprovalByDefault, ["project-structure"]) {
                RecoveryPolicy = policy.Name == ProjectStructureToolPolicy.ProjectStructureAssetImageAnalyze
                    ? AgentRuntimeToolRecoveryPolicy.ReconcileBeforeRetry
                    : AgentRuntimeToolRecoveryPolicy.Default,
                AuthorizeResultDisclosureAsync = (disclosure, token) => AuthorizeAsync(context, disclosure, token)
            }).ToArray();

    internal async Task<ProjectStructureResultEvidenceScope> BeginAsync(AgentRuntimeToolProviderContext context,
        string toolName, AIFunctionArguments arguments, ProjectWriteAdmission? sourceAdmission, CancellationToken cancellationToken) {
        var session = await RequireSessionAsync(context, cancellationToken);
        var all = CapturesCollection(toolName);
        var initial = all ? await ReadAllAsync(cancellationToken) : new Dictionary<Guid, ProjectWriteAdmission>();
        var capture = new ProjectStructureResultEvidenceScope(toolName, session.Profile.ProfileId, session.AgentId, projects.CaptureAsync, initial, all);
        if (sourceAdmission is not null) {
            capture.Record(sourceAdmission);
        } else if (context.Governance?.WorkspaceScope is { Kind: WorkspaceScopeKind.Project } scope &&
                Guid.TryParse(scope.Key, out var sourceProject)) {
            await capture.CaptureAsync(sourceProject, cancellationToken);
        }
        await capture.CaptureAsync(ReadGuid(arguments, "projectId"), cancellationToken);
        await capture.CaptureAsync(ReadGuid(arguments, "parentProjectId"), cancellationToken);
        foreach (var key in new[] { "nodeId", "parentNodeId", "taskNodeId" }) {
            await CaptureNodeKeyAsync(capture, ReadString(arguments, key), cancellationToken);
        }
        foreach (var key in new[] { "request", "scope" }) {
            if (ReadArgument<ProjectStructureRequestTargets>(arguments, key) is not { } targets) {
                continue;
            }
            foreach (var id in new[] { targets.ProjectId, targets.TargetProjectId, targets.ChildProjectId, targets.CurrentParentProjectId }) {
                await capture.CaptureAsync(id, cancellationToken);
            }
            foreach (var node in (targets.NodeIds ?? []).Concat(targets.SourceNodeIds ?? []).Concat(targets.SubtreeRootIds ?? []).Concat(new[] {
                    targets.NodeId, targets.SourceNodeId, targets.TargetNodeId, targets.ParentNodeKey, targets.DestinationParentNodeId })) {
                await CaptureNodeKeyAsync(capture, node, cancellationToken);
            }
        }
        return capture;
    }

    internal async Task CompleteAsync(ProjectStructureResultEvidenceScope capture, CancellationToken cancellationToken) {
        var current = await ReadTargetsAsync(capture.TargetIds, cancellationToken);
        AgentToolInvocationEffectScope.RecordDisclosureEvidence(ProjectStructureDisclosureEvidenceCodec.Write(capture.Finish(current)));
    }

    internal async ValueTask<IAsyncDisposable?> AuthorizeAsync(AgentRuntimeToolProviderContext context,
        AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        var session = await RequireSessionAsync(context, cancellationToken);
        if (disclosure.Evidence is null) {
            throw Unavailable();
        }
        ProjectStructureDisclosureEvidence evidence;
        try {
            evidence = ProjectStructureDisclosureEvidenceCodec.Read(disclosure.Evidence);
        } catch (Exception exception) when (exception is InvalidDataException or JsonException or ArgumentException) {
            throw new AgentToolAdmissionException("tool-admission.disclosure-authorization-unavailable",
                "The original Structure result authority cannot be recovered safely.");
        }
        if (evidence.State != ProjectStructureDisclosureState.Complete) {
            throw Unavailable();
        }
        if (evidence.AgentId != session.AgentId || evidence.DatabaseProfileId != session.Profile.ProfileId ||
                evidence.ToolName != disclosure.Payload.ToolName || !Handles(context, evidence.ToolName)) {
            throw Denied();
        }
        SandboxWorkspaceCatalogSnapshot? planCatalog = evidence.ToolName == ProjectStructureToolPolicy.ProjectPlanSummaryGet
            ? await catalog.LoadCatalogSnapshotAsync(cancellationToken) : null;
        ProjectWriteAdmission? processProject = null;
        if (session.Reference.BackgroundSource is { } background) {
            var current = processAccess is null ? null : await processAccess.ReadAccessAsync(session.Reference.ExecutionRunId, cancellationToken);
            if (current is not { CanRead: true, Dispatch.SourceAuthority.ProjectAdmission: { } project } ||
                    current.Dispatch.Evidence.ExecutorAgentId != session.AgentId ||
                    current.Dispatch.Evidence.ExecutionRunId != session.Reference.ExecutionRunId ||
                    AgentToolProtocolEnvelope.ComputeDigest(current.Dispatch.OwnerFingerprint) != background.OwnerFingerprint) {
                throw Denied();
            }
            processProject = new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId);
        }
        await RequireSessionAsync(context, cancellationToken);
        var held = await catalogLeases.AcquireAgentReadLeaseAsync(session.AgentId, cancellationToken);
        try {
            var actor = held.Agent;
            if (held.Scope != WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N")) ||
                    actor is null || actor.Id != session.AgentId || actor.IsTemplate || actor.Status != AgentLifecycleStatus.Active ||
                    !actor.Permissions.CanUseTools || planCatalog is not null &&
                    (held.Revision != planCatalog.Revision || !ProjectPlanAgentAuthorizationPolicy.IsPlanSummaryAuthorized(actor, planCatalog.Catalog.Capabilities))) {
                throw Denied();
            }
            var access = AgentProjectStructureAccessMetadata.Read(actor.ConfigurationJson);
            var directTargets = evidence.Targets.Where(target => evidence.DirectAccessProjectIds.Contains(target.ProjectId));
            if (processProject is null ? !access.CanRead || directTargets.Any(target => !Allows(access, target))
                    : directTargets.Any(target => target != processProject)) {
                throw Denied();
            }
            if (evidence.ToolName == ProjectStructureToolPolicy.ProjectStructureAssetImageAnalyze &&
                    !AgentWorkspaceToolAccessMetadata.Read(actor.ConfigurationJson).CanTransformArtifacts) {
                throw Denied();
            }
            var current = await ReadTargetsAsync(evidence.Targets.Select(target => target.ProjectId).ToHashSet(), cancellationToken);
            if (evidence.Targets.Any(target => !current.TryGetValue(target.ProjectId, out var active) || active != target)) {
                throw Denied();
            }
            return held;
        } catch {
            await held.DisposeAsync();
            throw;
        }
    }

    private async Task<AgentToolSessionAdmission> RequireSessionAsync(AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken) {
        if (!UsesJournal(context)) {
            throw Denied();
        }
        var observation = await admissions.RequireSessionObservationAsync(context.AdmittedToolSession!, cancellationToken);
        var session = observation.Session;
        var profile = runtimeState.GetSnapshot();
        if (session.Reference != context.AdmittedToolSession || session.AgentId != context.Agent.Id ||
                session.Profile.ProfileId != database.Profile.Profile.Id || session.Profile.ProfileId != profile.ActiveProfileId ||
                session.Profile.Fingerprint != profile.ActiveFingerprint || session.Profile.Generation.Value != profile.Generation) {
            throw Denied();
        }
        if (session.Reference.BackgroundSource is not null) {
            if (session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                    context.Purpose != AgentRuntimeToolProviderPurpose.GovernedProcessAutomation) {
                throw Denied();
            }
            return session;
        }
        var source = observation.TurnContext;
        var original = observation.Governance;
        if (source is null || original is not { ReadAllowed: true } ||
                original.AgentId != session.AgentId || original.AuthorityId != session.Reference.AuthorityId ||
                original.DatabaseProfileId != session.Profile.ProfileId || original.DatabaseProfileGeneration != session.Profile.Generation ||
                context.Governance is not { } supplied || supplied.AuthorityId != original.AuthorityId ||
                supplied.WorkspaceScope != original.WorkspaceScope) {
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
        return session;
    }

    private static bool Allows(AgentProjectStructureAccessSettings access, ProjectWriteAdmission target)
        => access.AllowAllProjects || access.AllowedProjectIds.Contains(target.ProjectId) &&
            access.AllowedProjectLifetimes.Contains(new(target.DatabaseProfileId, target.ProjectId, target.LifetimeId));

    private async Task<Dictionary<Guid, ProjectWriteAdmission>> ReadAllAsync(CancellationToken cancellationToken)
        => (await selections.ListAsync(cancellationToken: cancellationToken)).ToDictionary(item => item.Id, item => item.Admission);

    private async Task<Dictionary<Guid, ProjectWriteAdmission>> ReadTargetsAsync(IReadOnlySet<Guid> ids, CancellationToken cancellationToken) {
        if (ids.Count == 0) {
            return [];
        }
        if (ids.Count == 1) {
            var admission = await projects.CaptureAsync(ids.Single(), cancellationToken);
            return admission is null ? [] : new() { [admission.ProjectId] = admission };
        }
        var current = await ReadAllAsync(cancellationToken);
        return current.Where(pair => ids.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static bool CapturesCollection(string toolName) => toolName is
        ProjectStructureToolPolicy.ProjectStructureProjectsList or ProjectStructureToolPolicy.ProjectStructureHierarchyGet or
        ProjectStructureToolPolicy.ProjectStructureRead or ProjectStructureToolPolicy.ProjectStructureChecklist or
        ProjectStructureToolPolicy.ProjectStructureDependenciesQuery or ProjectStructureToolPolicy.ProjectStructureNodeWorkflowAddOptions;

    private static Guid? ReadGuid(AIFunctionArguments arguments, string key) {
        if (!arguments.TryGetValue(key, out var value)) {
            return null;
        }
        return value switch {
            Guid id when id != Guid.Empty => id,
            string text when Guid.TryParse(text, out var id) && id != Guid.Empty => id,
            JsonElement { ValueKind: JsonValueKind.String } json when json.TryGetGuid(out var id) && id != Guid.Empty => id,
            _ => null
        };
    }

    private static string? ReadString(AIFunctionArguments arguments, string key) {
        if (!arguments.TryGetValue(key, out var value)) {
            return null;
        }
        return value switch {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            _ => null
        };
    }

    private static Task CaptureNodeKeyAsync(ProjectStructureResultEvidenceScope capture, string? key, CancellationToken cancellationToken)
        => key is not null && ProjectWorkbenchGraphConventions.TryResolveProjectHierarchyNode(key, out _, out var projectId)
            ? capture.CaptureAsync(projectId, cancellationToken, requiresDirectAccess: false) : Task.CompletedTask;

    private static T? ReadArgument<T>(AIFunctionArguments arguments, string key) where T : class {
        if (!arguments.TryGetValue(key, out var value) || value is null) {
            return null;
        }
        if (value is T typed) {
            return typed;
        }
        var json = value is JsonElement element ? element : JsonSerializer.SerializeToElement(value);
        return json.Deserialize<T>(ArgumentOptions);
    }

    private static readonly JsonSerializerOptions ArgumentOptions = new(JsonSerializerDefaults.Web);

    private sealed record ProjectStructureRequestTargets(Guid? ProjectId, Guid? TargetProjectId, Guid? ChildProjectId,
        Guid? CurrentParentProjectId, string? NodeId, string? SourceNodeId, string? TargetNodeId, string? ParentNodeKey,
        string? DestinationParentNodeId, IReadOnlyList<string>? NodeIds, IReadOnlyList<string>? SourceNodeIds,
        IReadOnlyList<string>? SubtreeRootIds);

    private static AgentToolAdmissionException Unavailable() => new("tool-admission.disclosure-authorization-unavailable",
        "The saved Structure result lacks complete original lifetime provenance. It cannot be disclosed or reexecuted by recovery.");

    private static AgentToolAdmissionException Denied() => new("project-structure.result-disclosure-denied",
        "Current read authority does not permit the original Structure result and project lifetimes.");

    private sealed class DisclosureFunction(AIFunction inner, ProjectStructureResultDisclosureService owner,
        AgentRuntimeToolProviderContext context, ProjectWriteAdmission? sourceAdmission) : DelegatingAIFunction(inner) {
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) {
            var capture = await owner.BeginAsync(context, Name, arguments, sourceAdmission, cancellationToken);
            using var bound = capture.Bind();
            object? result;
            try {
                result = await base.InvokeCoreAsync(arguments, cancellationToken);
            } catch (Exception failure) when (failure is IAgentToolFailureEffectEvidence {
                IsSafeToExpose: true, EffectState: AgentToolEffectState.None or AgentToolEffectState.NotCommitted
            }) {
                capture.RecordKnownFailure();
                try {
                    await owner.CompleteAsync(capture, cancellationToken);
                } catch (Exception completionFailure) when (completionFailure is not OperationCanceledException) {
                    throw new AggregateException("The failed Structure operation has no completed disclosure checkpoint.",
                        failure, completionFailure);
                }
                throw;
            }
            await owner.CompleteAsync(capture, cancellationToken);
            return result;
        }
    }
}
