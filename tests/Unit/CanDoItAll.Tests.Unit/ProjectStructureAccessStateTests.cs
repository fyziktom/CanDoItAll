using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureAccessStateTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Captured_governance_keeps_the_original_read_and_mutation_ceiling_despite_broader_configuration(bool readAllowed) {
        var projectId = Guid.NewGuid();
        var governance = new AgentExecutionGovernanceSnapshot(AgentExecutionAuthorityId.Create(), Guid.NewGuid(), Guid.NewGuid(),
            new(1), WorkspaceScopeDescriptor.Project(projectId.ToString("D")), readAllowed, false, "test-policy", "original-ceiling");
        var state = Create(projectId, new() {
            CanRead = true, CanWrite = true, CanWriteTasks = true, CanCreateProjects = true, CanCreateSubprojects = true,
            AllowedProjectIds = [projectId]
        }, governance: governance);

        Assert.Equal(readAllowed, state.CanRead);
        Assert.False(state.CanWrite);
        Assert.False(state.CanWriteStructureUnscoped);
        Assert.False(state.CanWriteTasksUnscoped);
        Assert.False(state.CanCreateProjects);
        Assert.False(state.CanCreateSubprojects);
        Assert.Same(governance, state.Governance);
        if (readAllowed) {
            state.EnsureProjectReadAllowed(projectId);
        } else {
            Assert.Equal("ProjectStructureReadDenied", Assert.Throws<ProjectStructureAgentException>(() => state.EnsureProjectReadAllowed(projectId)).ErrorCode);
        }
        Assert.Equal("ProjectStructureWriteDenied", Assert.Throws<ProjectStructureAgentException>(() => state.EnsureProjectWriteAllowed(projectId)).ErrorCode);
        Assert.Equal("ProjectTaskWriteDenied", Assert.Throws<ProjectStructureAgentException>(() => state.EnsureProjectTaskWriteAllowed(projectId)).ErrorCode);
        Assert.Equal("ProjectCreationDenied", Assert.Throws<ProjectStructureAgentException>(state.EnsureProjectCreationAllowed).ErrorCode);
        Assert.Equal("SubprojectCreationDenied", Assert.Throws<ProjectStructureAgentException>(() =>
            state.EnsureSubprojectCreationAllowed(ProjectProcessProjectOperation.CreateChild)).ErrorCode);
    }

    [Fact]
    public void Task_only_grant_keeps_task_mutation_without_general_Structure_or_hierarchy_mutation() {
        var projectId = Guid.NewGuid();
        var state = Create(projectId, new() { CanRead = true, CanWriteTasks = true, AllowedProjectIds = [projectId] });

        state.EnsureProjectReadAllowed(projectId);
        state.EnsureProjectTaskWriteAllowed(projectId);
        Assert.Equal("ProjectStructureWriteDenied", Assert.Throws<ProjectStructureAgentException>(state.EnsureWriteAllowed).ErrorCode);
        Assert.Equal("ProjectStructureWriteDenied", Assert.Throws<ProjectStructureAgentException>(() => state.EnsureProjectWriteAllowed(projectId)).ErrorCode);
        Assert.Equal("SubprojectCreationDenied", Assert.Throws<ProjectStructureAgentException>(() =>
            state.EnsureSubprojectCreationAllowed(ProjectProcessProjectOperation.ChangeHierarchy)).ErrorCode);
        Assert.Equal("ProjectStructureContextProjectDenied", Assert.Throws<ProjectStructureAgentException>(() =>
            state.EnsureProjectTaskWriteAllowed(Guid.NewGuid())).ErrorCode);
    }

    [Fact]
    public void Scoped_Process_access_does_not_inherit_the_assigned_executors_global_project_creation_or_task_grants() {
        var projectId = Guid.NewGuid();
        var scoped = new ProjectStructureScopedProcessAccess(projectId, "saved-process", "saved-step", true, true, null, null);
        var state = Create(projectId, new() {
            CanRead = true, CanWrite = true, CanWriteTasks = true, CanCreateProjects = true, CanCreateSubprojects = true,
            AllowAllProjects = true
        }, AgentRuntimeToolProviderPurpose.GovernedProcessAutomation, scoped);

        state.EnsureProjectReadAllowed(projectId);
        Assert.Same(scoped, state.EnsureScopedProcessExternalActionAllowed());
        Assert.False(state.AllowAllProjects);
        Assert.Equal(projectId, Assert.Single(state.AllowedProjectIds));
        Assert.False(state.CanWriteTasksUnscoped);
        Assert.False(state.CanCreateProjects);
        Assert.False(state.CanCreateSubprojects);
        Assert.False(state.AllowsGovernedProjectOperation(ProjectProcessProjectOperation.CreateChild));
        Assert.False(state.AllowsGovernedProjectOperation(ProjectProcessProjectOperation.ChangeHierarchy));
        Assert.False(state.AllowsGovernedProjectOperation(ProjectProcessProjectOperation.MoveToChild));
        Assert.Equal("ProjectTaskWriteDenied", Assert.Throws<ProjectStructureAgentException>(() => state.EnsureProjectTaskWriteAllowed(projectId)).ErrorCode);
        Assert.Equal("ProjectStructureProjectDenied", Assert.Throws<ProjectStructureAgentException>(() => state.EnsureProjectReadAllowed(Guid.NewGuid())).ErrorCode);
    }

    private static ProjectStructureAccessState Create(Guid projectId, AgentProjectStructureAccessSettings settings,
        AgentRuntimeToolProviderPurpose purpose = AgentRuntimeToolProviderPurpose.InteractiveChat,
        ProjectStructureScopedProcessAccess? scoped = null, AgentExecutionGovernanceSnapshot? governance = null) {
        var intent = AgentRuntimeContextIntent.Empty with {
            SourceKind = ProjectStructureAccessState.ProjectStructureSourceKind, SourceId = projectId.ToString("D")
        };
        return new(settings, scoped, intent, purpose, new ProjectStructureInvocationSnapshotReadContext(purpose, intent, [], []), governance);
    }
}
