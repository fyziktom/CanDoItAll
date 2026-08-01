using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureAgentRuntimeToolProviderTests
{
    [Fact]
    public void ShouldAttachForContext_returns_false_for_plain_agent_chat()
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "chat-session",
            IsGovernedProcessStep = false,
            AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure]
        };

        Assert.False(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
    }

    [Fact]
    public void ShouldAttachForContext_returns_true_for_project_structure_chat()
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "project-structure"
        };

        Assert.True(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
    }

    [Fact]
    public void ShouldAttachForContext_returns_true_for_projects_portfolio_chat()
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "projects"
        };

        Assert.True(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
    }

    [Theory]
    [InlineData(ProcessOperationContractNames.ReadProjectStructure)]
    [InlineData(ProcessOperationContractNames.StartProjectNodeProcess)]
    [InlineData(ProcessOperationContractNames.ExecuteExternalAction)]
    public void ShouldAttachForContext_returns_true_for_governed_project_structure_operations(string operation)
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "process-step",
            IsGovernedProcessStep = true,
            AllowedOperations = [operation]
        };

        Assert.True(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
    }

    [Fact]
    public void ShouldAttachForContext_returns_false_for_governed_non_project_structure_operations()
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "process-step",
            IsGovernedProcessStep = true,
            AllowedOperations = [ProcessOperationContractNames.ReadUpstreamArtifacts]
        };

        Assert.False(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
    }

    [Fact]
    public void Portfolio_architect_style_access_allows_non_task_structure_mutations_only()
    {
        var access = new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            CanWrite = false,
            CanWriteNonTaskStructure = true,
            CanWriteTasks = false,
            CanCreateProjects = true,
            CanCreateSubprojects = true,
            AllowAllProjects = true
        };

        Assert.True(ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access));
        Assert.False(ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(access));
    }

    [Fact]
    public void Project_structure_chat_allows_only_its_active_project_despite_agent_wide_access()
    {
        var activeProjectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "project-structure",
            SourceId = activeProjectId.ToString("D")
        };
        IReadOnlySet<Guid> configuredProjectIds = new HashSet<Guid> { otherProjectId };
        IReadOnlySet<Guid> sessionCreatedProjectIds = new HashSet<Guid>();

        ProjectStructureAgentRuntimeToolProvider.EnsureProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            activeProjectId);

        Assert.True(ProjectStructureAgentRuntimeToolProvider.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            activeProjectId));
        Assert.False(ProjectStructureAgentRuntimeToolProvider.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            otherProjectId));

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRuntimeToolProvider.EnsureProjectAllowedForContext(
                AgentRuntimeToolProviderPurpose.InteractiveChat,
                intent,
                allowAllProjects: true,
                configuredProjectIds,
                sessionCreatedProjectIds,
                otherProjectId));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("ProjectStructureContextProjectDenied", exception.ErrorCode);
        Assert.Contains(activeProjectId.ToString("D"), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_structure_chat_allows_projects_created_during_the_same_session()
    {
        var activeProjectId = Guid.NewGuid();
        var createdProjectId = Guid.NewGuid();
        var unrelatedProjectId = Guid.NewGuid();
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "project-structure",
            SourceId = activeProjectId.ToString("D")
        };
        IReadOnlySet<Guid> allowedProjectIds = new HashSet<Guid>
        {
            activeProjectId,
            createdProjectId,
            unrelatedProjectId
        };
        IReadOnlySet<Guid> sessionCreatedProjectIds = new HashSet<Guid> { createdProjectId };

        ProjectStructureAgentRuntimeToolProvider.EnsureProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: false,
            allowedProjectIds,
            sessionCreatedProjectIds,
            createdProjectId);

        Assert.True(ProjectStructureAgentRuntimeToolProvider.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: false,
            allowedProjectIds,
            sessionCreatedProjectIds,
            createdProjectId));
        Assert.False(ProjectStructureAgentRuntimeToolProvider.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: false,
            allowedProjectIds,
            sessionCreatedProjectIds,
            unrelatedProjectId));
    }

    [Fact]
    public void Portfolio_context_preserves_agent_wide_project_access()
    {
        var projectId = Guid.NewGuid();
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "projects",
            SourceId = Guid.NewGuid().ToString("D")
        };
        IReadOnlySet<Guid> configuredProjectIds = new HashSet<Guid>();
        IReadOnlySet<Guid> sessionCreatedProjectIds = new HashSet<Guid>();

        ProjectStructureAgentRuntimeToolProvider.EnsureProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            projectId);

        Assert.True(ProjectStructureAgentRuntimeToolProvider.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            projectId));
    }

    [Fact]
    public void Governed_process_access_is_not_restricted_by_interactive_project_context()
    {
        var processProjectId = Guid.NewGuid();
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "project-structure",
            SourceId = Guid.NewGuid().ToString("D"),
            IsGovernedProcessStep = true
        };
        IReadOnlySet<Guid> configuredProjectIds = new HashSet<Guid>();
        IReadOnlySet<Guid> sessionCreatedProjectIds = new HashSet<Guid>();

        ProjectStructureAgentRuntimeToolProvider.EnsureProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            processProjectId);

        Assert.True(ProjectStructureAgentRuntimeToolProvider.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            processProjectId));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(500)]
    public void Project_structure_agent_exception_is_not_agent_visible_by_default(
        int statusCode)
    {
        var exception = new ProjectStructureAgentException(
            statusCode,
            "ProjectStructureFailure",
            "Safe agent-facing message.",
            new { Secret = "must-not-be-projected" });

        Assert.False(exception.IsSafeToExpose);
        Assert.False(exception.CanRetryWithCorrectedInput);
    }

    [Fact]
    public void Reviewed_project_structure_failure_can_opt_into_agent_visible_recovery()
    {
        var exception = ProjectStructureAgentException.CreateAgentVisible(
            400,
            "InvalidProjectObjectMetadata",
            "request.metadataJson has an incompatible value at '$.workflow'.",
            canRetryWithCorrectedInput: true,
            diagnosticDetails: new { Secret = "must-not-be-projected" });

        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Equal(
            "request.metadataJson has an incompatible value at '$.workflow'.",
            exception.SafeMessage);
    }
}
