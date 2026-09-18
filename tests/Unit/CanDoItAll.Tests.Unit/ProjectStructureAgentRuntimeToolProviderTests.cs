using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.Projects;

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

        ProjectStructureAccessState.EnsureProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            activeProjectId);

        Assert.True(ProjectStructureAccessState.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            activeProjectId));
        Assert.False(ProjectStructureAccessState.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            otherProjectId));

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAccessState.EnsureProjectAllowedForContext(
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

        ProjectStructureAccessState.EnsureProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: false,
            allowedProjectIds,
            sessionCreatedProjectIds,
            createdProjectId);

        Assert.True(ProjectStructureAccessState.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: false,
            allowedProjectIds,
            sessionCreatedProjectIds,
            createdProjectId));
        Assert.False(ProjectStructureAccessState.IsProjectAllowedForContext(
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

        ProjectStructureAccessState.EnsureProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            projectId);

        Assert.True(ProjectStructureAccessState.IsProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            projectId));
    }

    [Theory]
    [InlineData("ReadProcessContext,ReadProjectStructure", true, false)]
    [InlineData("ReadProcessContext,ReadProjectStructure,ExecuteExternalAction", true, true)]
    [InlineData("ReadProcessContext,WriteManagedProcessArtifacts", false, false)]
    public void Tool_inventory_access_for_a_governed_step_follows_its_declared_operations(
        string allowedOperations,
        bool expectedCanRead,
        bool expectedCanWrite)
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "process-step",
            SourceId = "write-note-node",
            ProcessRunId = Guid.NewGuid().ToString("D"),
            ProcessStepId = Guid.NewGuid().ToString("D"),
            IsGovernedProcessStep = true,
            AllowedOperations = allowedOperations.Split(',')
        };

        var access = ProjectStructureScopedProcessAccess.ForToolInventory(intent);
        var purpose = AgentRuntimeToolProviderPurpose.GovernedProcessAutomation;
        var state = new ProjectStructureAccessState(
            new AgentProjectStructureAccessSettings(),
            access,
            intent,
            purpose,
            new ProjectStructureInvocationSnapshotReadContext(purpose, intent, [], []));

        Assert.Equal(Guid.Empty, access.ProjectId);
        Assert.Equal(intent.ProcessRunId, access.ProcessRunId);
        Assert.Equal(intent.ProcessStepId, access.ProcessStepId);
        Assert.Null(access.ExpectedProjectAdmission);
        Assert.Null(access.ProcessMutationAdmission);
        Assert.Equal(expectedCanRead, state.CanRead);
        Assert.Equal(expectedCanWrite, state.CanWrite);
        Assert.False(state.IsProjectAllowed(Guid.NewGuid()));
    }

    [Fact]
    public void Tool_inventory_access_allows_no_project_not_even_the_empty_placeholder()
    {
        var intent = CreateInventoryIntent("ReadProcessContext,ReadProjectStructure,ExecuteExternalAction");
        var state = CreateInventoryState(
            intent,
            new AgentProjectStructureAccessSettings { CanRead = true, CanWrite = true, AllowAllProjects = true });

        Assert.True(state.CanRead);
        Assert.True(state.CanWrite);
        Assert.True(state.ScopedProcessAccess!.IsToolInventory);
        Assert.False(state.AllowAllProjects);
        Assert.Empty(state.AllowedProjectIds);
        Assert.False(state.IsProjectAllowed(Guid.Empty));
        Assert.False(state.IsProjectAllowed(Guid.NewGuid()));
        var denied = Assert.Throws<ProjectStructureAgentException>(() => state.EnsureProjectReadAllowed(Guid.Empty));
        Assert.Equal(403, denied.StatusCode);
        Assert.Equal("ProjectStructureProjectDenied", denied.ErrorCode);
    }

    [Theory]
    [InlineData("ReadProcessContext,ReadProjectStructure,ExecuteExternalAction", true)]
    [InlineData("ReadProcessContext,ReadProjectStructure", false)]
    public void Tool_inventory_reports_configured_task_and_project_tools_without_a_saved_admission(
        string allowedOperations,
        bool expected)
    {
        // The saved launch authority narrows these at dispatch; discovery reports what the agent's configuration and
        // the step's declared operations could compose, without inventing an admission.
        var intent = CreateInventoryIntent(allowedOperations);
        var state = CreateInventoryState(intent, CreateFullAccessSettings());

        Assert.Null(state.ScopedProcessAccess!.ProcessMutationAdmission);
        Assert.Equal(expected, state.CanWriteTasksUnscoped);
        Assert.Equal(expected, state.CanCreateProjects);
        Assert.Equal(expected, state.CanCreateSubprojects);
        Assert.Equal(expected, state.AllowsGovernedProjectOperation(ProjectProcessProjectOperation.CreateChild));
        Assert.Equal(expected, state.AllowsGovernedProjectOperation(ProjectProcessProjectOperation.MoveToChild));
    }

    [Fact]
    public void Saved_dispatch_without_an_admission_still_withholds_task_and_project_tools()
    {
        // The inventory relaxation must not leak into a real dispatch: a saved scope without a mutation admission
        // keeps the task and project ceilings closed.
        var intent = CreateInventoryIntent("ReadProcessContext,ReadProjectStructure,ExecuteExternalAction");
        var access = new ProjectStructureScopedProcessAccess(
            Guid.NewGuid(),
            intent.ProcessRunId,
            intent.ProcessStepId,
            CanRead: true,
            CanWrite: true,
            AgentContext: null,
            ProcessNodeContext: null);
        var purpose = AgentRuntimeToolProviderPurpose.GovernedProcessAutomation;
        var state = new ProjectStructureAccessState(
            CreateFullAccessSettings(),
            access,
            intent,
            purpose,
            new ProjectStructureInvocationSnapshotReadContext(purpose, intent, [], []));

        Assert.False(access.IsToolInventory);
        Assert.True(state.CanWrite);
        Assert.False(state.CanWriteTasksUnscoped);
        Assert.False(state.CanCreateProjects);
        Assert.False(state.CanCreateSubprojects);
        Assert.False(state.AllowsGovernedProjectOperation(ProjectProcessProjectOperation.CreateChild));
        Assert.True(state.IsProjectAllowed(access.ProjectId));
    }

    [Fact]
    public async Task Inventory_only_tools_keep_their_contract_but_refuse_every_invocation()
    {
        var invocations = 0;
        var function = AIFunctionFactory.Create(
            (Guid projectId, string title) =>
            {
                invocations++;
                return $"{projectId:D}:{title}";
            },
            ProjectStructureToolPolicy.ProjectStructureNodeCreate,
            "Creates a node.");

        var tools = ProjectStructureInventoryOnlyTool.WrapAll([function]);

        var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(tools));
        Assert.Equal(function.Name, tool.Name);
        Assert.Equal(function.Description, tool.Description);
        Assert.Equal(function.JsonSchema.GetRawText(), tool.JsonSchema.GetRawText());
        var denied = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => tool
            .InvokeAsync(new AIFunctionArguments { ["projectId"] = Guid.NewGuid(), ["title"] = "Inventory probe" })
            .AsTask());
        Assert.Equal(403, denied.StatusCode);
        Assert.Equal(ProjectStructureInventoryOnlyTool.ErrorCode, denied.ErrorCode);
        Assert.Equal(0, invocations);
    }

    private static AgentRuntimeContextIntent CreateInventoryIntent(string allowedOperations)
        => AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "process-step",
            SourceId = "write-note-node",
            ProcessRunId = Guid.NewGuid().ToString("D"),
            ProcessStepId = Guid.NewGuid().ToString("D"),
            IsGovernedProcessStep = true,
            AllowedOperations = allowedOperations.Split(',')
        };

    private static ProjectStructureAccessState CreateInventoryState(
        AgentRuntimeContextIntent intent,
        AgentProjectStructureAccessSettings settings)
    {
        var purpose = AgentRuntimeToolProviderPurpose.GovernedProcessAutomation;
        return new ProjectStructureAccessState(
            settings,
            ProjectStructureScopedProcessAccess.ForToolInventory(intent),
            intent,
            purpose,
            new ProjectStructureInvocationSnapshotReadContext(purpose, intent, [], []));
    }

    private static AgentProjectStructureAccessSettings CreateFullAccessSettings()
        => new()
        {
            CanRead = true,
            CanWrite = true,
            CanWriteNonTaskStructure = true,
            CanWriteTasks = true,
            CanCreateProjects = true,
            CanCreateSubprojects = true,
            AllowAllProjects = true
        };

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

        ProjectStructureAccessState.EnsureProjectAllowedForContext(
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            intent,
            allowAllProjects: true,
            configuredProjectIds,
            sessionCreatedProjectIds,
            processProjectId);

        Assert.True(ProjectStructureAccessState.IsProjectAllowedForContext(
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
