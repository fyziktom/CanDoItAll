using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanAgentAdmission(IAgentCatalogReadLeaseStore catalog,
    ProjectAgentSourceMutationAuthority source, ProjectWriteAdmissionService projects,
    WorkspaceSettingsWriter settings, ICanonicalRuntimeDatabase database) {
    private const string SourceFormat = "workbench-task-plan-admission-source";

    internal async Task<AgentPlanningRunAdmission> PrepareAsync(AgentPlanningRunPreparation preparation, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(preparation);
        var governance = preparation.Governance;
        if (preparation.Agent.Id != governance.AgentId || !preparation.CatalogRevision.IsAssigned ||
            preparation.Source.SourceKind.Value != AgentChatTrustedSourceKinds.ProjectStructure ||
            governance.EffectiveSchemaVersion != AgentExecutionAuthorityRecord.CurrentSchemaVersion ||
            governance.SourceProjectLifetime is not { } lifetime ||
            !Guid.TryParse(preparation.Source.SourceId.Value, out var sourceId) || sourceId != lifetime.ProjectId ||
            governance.WorkspaceScope != WorkspaceScopeDescriptor.Project(lifetime.ProjectId.ToString("D")) ||
            governance.DatabaseProfileId != preparation.Profile.ProfileId || governance.DatabaseProfileGeneration != preparation.Profile.Generation ||
            lifetime.DatabaseProfileId != preparation.Profile.ProfileId) {
            throw ProjectTaskPlanAgentCodec.Denied("Planning requires the original current-schema Project Structure source and lifetime.");
        }
        RequireCurrentProfile(preparation.Profile);
        var capturedSettings = await settings.ReadV2Async(cancellationToken);
        if (capturedSettings.DecisionDataPolicy is not { } policy || capturedSettings.Expectation.Head is not { } head) {
            throw new AgentToolAdmissionException("planning.workspace-policy-missing", "Configure and save the Workspace decision-data policy before starting a plan.");
        }
        var project = new ProjectWriteAdmission(lifetime.DatabaseProfileId, lifetime.ProjectId, lifetime.LifetimeId);
        await using var held = await catalog.AcquireAgentReadLeaseAsync(preparation.Agent.Id, cancellationToken);
        if (held.Revision != preparation.CatalogRevision || held.Agent is not { } agent || agent.Id != preparation.Agent.Id) {
            throw new AgentToolAdmissionException("planning.catalog-changed", "The prepared Agent catalog changed before planning admission.");
        }
        var original = new ProjectDefinitionToolAgentSource(AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson), governance, project);
        var admission = new ProjectAgentMutationAdmission(agent.Id, preparation.Profile.ProfileId, original.Access, governance,
            ProjectAgentMutationDomain.Tasks, ProjectStructureToolPolicy.ProjectTaskPlanPublish, project);
        source.RequireSource(admission, new(ProjectMutationPurpose.ExistingWrite, project.ProjectId, [project]), held);
        await projects.RequireCurrentAsync(project, cancellationToken);
        RequireCurrentProfile(preparation.Profile);
        var provenance = new AgentPlanningPolicyProvenance(capturedSettings.Expectation.Namespace.Value, head.Revision,
            agent.Id, held.Revision.Value, policy, AgentDecisionDataPolicyMetadata.Read(agent.ConfigurationJson), null,
            ProjectTaskPlanAgentParticipant.Capabilities, []);
        var frozen = ProjectTaskPlanAgentCodec.Write(SourceFormat, original, AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        return new(preparation.Profile, governance.AuthorityId, provenance, session => {
            var captured = ProjectTaskPlanAgentCodec.Read<ProjectDefinitionToolAgentSource>(frozen, SourceFormat,
                AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
            return ProjectTaskPlanAgentCodec.Write(ProjectTaskPlanAgentCodec.TargetFormat, new ProjectTaskPlanAgentTarget(session, project, captured),
                AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        });
    }

    private void RequireCurrentProfile(AgentToolProfileBinding expected) {
        if (database.Profile.Profile.Id != expected.ProfileId || database.Generation != expected.Generation.Value ||
            database.Profile.Profile.Runtime.Fingerprint != expected.Fingerprint) {
            throw ProjectTaskPlanAgentCodec.Denied("The Workspace or database profile changed during planning admission.");
        }
    }
}
