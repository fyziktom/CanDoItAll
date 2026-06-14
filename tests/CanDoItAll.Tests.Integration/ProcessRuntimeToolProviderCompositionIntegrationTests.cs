using System.Text.Json;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRuntimeToolProviderCompositionIntegrationTests
{
    private static readonly string[] ExpectedProcessToolNames =
    [
        "processes_definitions_list",
        "processes_definition_editor_get",
        "processes_definition_save",
        "processes_definition_role_add",
        "processes_definition_publish",
        "processes_definition_delete",
        "processes_definition_export",
        "processes_definition_import",
        "processes_runs_list",
        "processes_run_detail_get",
        "processes_analytics_get",
        "processes_run_start",
        "processes_step_transition",
        "processes_assignment_resolve",
        "processes_artifact_record",
        "processes_party_options_list",
        "processes_executor_options_list",
        "processes_templates_list",
        "processes_template_get",
        "processes_template_mermaid_get",
        "processes_template_import",
        "processes_template_baseline_scenarios_list",
        "processes_template_live_run_profiles_list"
    ];

    [Fact]
    public async Task ProjectStructureRuntimeToolProviderComposition_app_composition_registers_project_structure_provider_with_complete_tool_inventory()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var projectStructureProvider = Assert.Single(
            scope.ServiceProvider.GetServices<IAgentRuntimeToolProvider>()
                .OfType<ProjectStructureAgentRuntimeToolProvider>());

        Assert.Equal(900, projectStructureProvider.Order);
        Assert.Equal("project-structure.runtime-tools", projectStructureProvider.Descriptor?.ProviderKey);

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var agent = seededAgent with
        {
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    CanWrite = true,
                    AllowAllProjects = true
                })
        };

        var tools = await projectStructureProvider.CreateToolsAsync(
            new AgentRuntimeToolProviderContext(
                agent,
                provider,
                [],
                SuppressApprovalRequirements: false,
                AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
                RuntimeSessionKey: "scenario04-runtime-smoke",
                Tags: new Dictionary<string, string>
                {
                    ["proof"] = "Scenario04"
                }),
            CancellationToken.None);
        var toolNames = tools
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectedToolNames = AgentToolInvocationPolicyMetadata.ProjectStructureReadTools
            .Concat(AgentToolInvocationPolicyMetadata.ProjectStructureMutationTools)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expectedToolNames.Count, toolNames.Count);
        foreach (var toolName in expectedToolNames)
        {
            Assert.Contains(toolName, toolNames);
        }
    }

    [Fact]
    public async Task ProjectStructureRuntimeToolProvider_bounds_unfiltered_governed_process_structure_read()
    {
        const string storedStatus = "Stored";

        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var projectStructureProvider = Assert.Single(
            scope.ServiceProvider.GetServices<IAgentRuntimeToolProvider>()
                .OfType<ProjectStructureAgentRuntimeToolProvider>());
        var projectId = await CreateProjectAsync(
            scope.ServiceProvider.GetRequiredService<ProjectsService>(),
            "Governed process project-structure default read");
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var storedSeeds = Enumerable
            .Range(0, 95)
            .Select(index => new ProjectObjectSeedRequest(
                ProjectObjectType.File,
                $"Stored process output noise {index:000}",
                "Historical output",
                "Should not crowd the governed default read.",
                ObjectSubtype: "text"))
            .ToList();
        await workbenchService.SeedProjectObjectsAsync(projectId, storedSeeds);
        var storedNodeIds = (await workbenchService.GetStructureAsync(projectId))
            .Nodes
            .Where(node => node.Title.StartsWith("Stored process output noise ", StringComparison.Ordinal))
            .Select(node => node.Id)
            .ToList();
        await workbenchService.UpdateObjectStatusesAsync(projectId, storedNodeIds, storedStatus);

        for (var index = 0; index < 3; index++)
        {
            await workbenchService.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.WorkItem,
                    $"Visible draft work {index:000}",
                    "Current work",
                    "Should remain visible in the governed default read.",
                    $"project:{projectId:D}"));
        }

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var agent = seededAgent with
        {
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = false,
                    CanWrite = false,
                    AllowAllProjects = false
                })
        };
        using var auditScope = WorkspaceExecutionAuditContext.BeginScope(
            CreateTrustedProcessRun(agent.Id, projectId));
        var tools = await projectStructureProvider.CreateToolsAsync(
            CreateProjectScopedProviderContext(agent, provider, projectId),
            CancellationToken.None);
        var readTool = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(tools, item => string.Equals(item.Name, "project_structure_read", StringComparison.OrdinalIgnoreCase)));

        var defaultRead = ReadToolResult<ProjectStructureReadToolData>(await readTool.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["projectId"] = projectId
            })));
        var explicitStoredRead = ReadToolResult<ProjectStructureReadToolData>(await readTool.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
                ["request"] = new ProjectStructureReadRequest(
                    Statuses: [storedStatus],
                    Take: 120)
            })));

        Assert.DoesNotContain(defaultRead.Nodes, node => string.Equals(node.Status, storedStatus, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(defaultRead.Nodes, node => node.Title == "Visible draft work 000");
        Assert.Contains(
            defaultRead.Warnings,
            warning => warning.Contains("Governed process default applied", StringComparison.Ordinal));
        Assert.Equal(95, explicitStoredRead.Nodes.Count);
        Assert.All(explicitStoredRead.Nodes, node => Assert.Equal(storedStatus, node.Status));
        Assert.DoesNotContain(
            explicitStoredRead.Warnings,
            warning => warning.Contains("Governed process default applied", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProjectStructureRuntimeToolProvider_grants_governed_process_scoped_current_project_asset_write_without_global_project_write()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var projectStructureProvider = Assert.Single(
            scope.ServiceProvider.GetServices<IAgentRuntimeToolProvider>()
                .OfType<ProjectStructureAgentRuntimeToolProvider>());
        var projectId = await CreateProjectAsync(
            scope.ServiceProvider.GetRequiredService<ProjectsService>(),
            "Process scoped project-structure runtime provider");
        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var agent = seededAgent with
        {
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = false,
                    CanWrite = false,
                    AllowAllProjects = false
                })
        };
        using var auditScope = WorkspaceExecutionAuditContext.BeginScope(
            CreateTrustedProcessRun(agent.Id, projectId));
        var tools = await projectStructureProvider.CreateToolsAsync(
            CreateProjectScopedProviderContext(agent, provider, projectId),
            CancellationToken.None);
        var assetCreateTool = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(tools, item => string.Equals(item.Name, "project_structure_asset_create", StringComparison.OrdinalIgnoreCase)));

        var createdAsset = ReadToolResult<ProjectStructureNodeSummary>(await assetCreateTool.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
                ["request"] = new ProjectStructureAssetCreateInput(
                    ProjectObjectType.File,
                    "Scoped process evidence",
                    "Runtime provider access proof",
                    "Created by a process-scoped project-structure grant.",
                    new ProjectObjectMediaPayload(
                        "scoped-process-evidence.txt",
                        "text/plain",
                        Convert.ToBase64String(Encoding.UTF8.GetBytes("process scoped evidence"))),
                    ParentNodeKey: $"project:{projectId:D}",
                    ObjectSubtype: "text")
            })));

        Assert.Equal(ProjectObjectType.File, createdAsset.ObjectType);
        Assert.Equal("Scoped process evidence", createdAsset.Title);

        var projectCreateTool = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(tools, item => string.Equals(item.Name, "project_structure_project_create", StringComparison.OrdinalIgnoreCase)));
        var exception = await Record.ExceptionAsync(async () =>
            await projectCreateTool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["request"] = new ProjectStructureProjectSaveRequest(
                    "Unexpected project",
                    "Global write should remain denied.",
                    "Process-scoped project-structure access must not create projects.",
                    "Execution",
                    ProjectStatus.Active)
            })));
        var projectStructureException = AssertProjectStructureException(exception);

        Assert.Equal("ProjectStructureWriteDenied", projectStructureException.ErrorCode);
    }

    [Fact]
    public async Task ProjectStructureRuntimeToolProvider_reuses_project_structure_launch_agent_for_scoped_project_lease_reentry()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var projectStructureProvider = Assert.Single(
            scope.ServiceProvider.GetServices<IAgentRuntimeToolProvider>()
                .OfType<ProjectStructureAgentRuntimeToolProvider>());
        var projectId = await CreateProjectAsync(
            scope.ServiceProvider.GetRequiredService<ProjectsService>(),
            "Process scoped project-structure lease owner");
        var launchAgent = new ProjectStructureAgentIdentityDescriptor(
            "codex-launch-owner",
            "Codex Launch Owner",
            "LUCYSPOWER",
            @"C:\repositories\CanDoItAll",
            "maf-processes-refactor",
            "session-launch-owner");
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                "Process scoped launch owner regression",
                15),
            new ProjectStructureAgentContext(
                launchAgent.AgentId,
                launchAgent.AgentName,
                launchAgent.MachineName,
                launchAgent.RepositoryRoot,
                launchAgent.BranchName,
                launchAgent.SessionId),
            CancellationToken.None);

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var executingAgent = seededAgent with
        {
            Id = Guid.NewGuid(),
            Name = "Scoped process role agent",
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProjectStructureAccessSettings
                {
                    CanRead = false,
                    CanWrite = false,
                    AllowAllProjects = false
                })
        };
        using var auditScope = WorkspaceExecutionAuditContext.BeginScope(
            CreateTrustedProcessRun(executingAgent.Id, projectId, launchAgent));
        var tools = await projectStructureProvider.CreateToolsAsync(
            CreateProjectScopedProviderContext(executingAgent, provider, projectId),
            CancellationToken.None);
        var nodeCreateTool = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(tools, item => string.Equals(item.Name, "project_structure_node_create", StringComparison.OrdinalIgnoreCase)));

        var createdNode = ReadToolResult<ProjectStructureNodeSummary>(await nodeCreateTool.InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
                ["request"] = new ProjectStructureNodeCreateInput(
                    ProjectObjectType.WorkItem,
                    "Scoped process command writeback",
                    "Lease owner regression proof",
                    "Created while the active project lease is owned by the project-structure launch agent.",
                    $"project:{projectId:D}",
                    240,
                    160,
                    ObjectSubtype: "task")
            })));

        Assert.Equal(ProjectObjectType.WorkItem, createdNode.ObjectType);
        Assert.Equal("Scoped process command writeback", createdNode.Title);
    }

    [Fact]
    public async Task ProcessRuntimeProvider_app_composition_preserves_process_tool_exact_name_parity()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();

        var processProvider = Assert.Single(
            scope.ServiceProvider.GetServices<IAgentRuntimeToolProvider>()
                .OfType<ProcessAgentRuntimeToolProvider>());

        Assert.Equal(1000, processProvider.Order);

        var seed = SandboxWorkspaceSeedFactory.Create();
        var seededAgent = seed.Agents[0];
        var provider = Assert.Single(seed.Providers, item => item.Id == seededAgent.ProviderProfileId);
        var agent = seededAgent with
        {
            Permissions = AgentPermissionsPolicy.Default,
            ConfigurationJson = AgentProcessAccessMetadata.Write(
                seededAgent.ConfigurationJson,
                new AgentProcessAccessSettings
                {
                    CanRead = true,
                    CanWrite = true,
                    AllowAllDefinitions = true
                })
        };

        var tools = await processProvider.CreateToolsAsync(
            new AgentRuntimeToolProviderContext(
                agent,
                provider,
                [],
                SuppressApprovalRequirements: false,
                AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
                RuntimeSessionKey: "scenario07-runtime-smoke",
                Tags: new Dictionary<string, string>
                {
                    ["proof"] = "Scenario07"
                }),
            CancellationToken.None);
        var toolNames = tools
            .Select(item => item.Name)
            .ToList();

        Assert.Equal(ExpectedProcessToolNames.Length, toolNames.Count);
        Assert.Equal(
            ExpectedProcessToolNames.Length,
            toolNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var toolName in ExpectedProcessToolNames)
        {
            Assert.Contains(toolNames, item => string.Equals(item, toolName, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static AgentRuntimeToolProviderContext CreateProjectScopedProviderContext(
        AgentDefinition agent,
        ProviderProfile provider,
        Guid projectId)
    {
        return new AgentRuntimeToolProviderContext(
            agent,
            provider,
            [],
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            RuntimeSessionKey: "process-scoped-project-structure-write",
            Tags: new Dictionary<string, string>
            {
                ["workspaceScopeKind"] = WorkspaceScopeKind.Project.ToString(),
                ["workspaceScopeKey"] = projectId.ToString("D")
            });
    }

    private static ExecutionRunRecord CreateTrustedProcessRun(
        Guid agentId,
        Guid projectId,
        ProjectStructureAgentIdentityDescriptor? launchAgent = null)
    {
        var now = DateTimeOffset.UtcNow;
        var metadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            $$"""
            {
              "{{ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey}}": [
                "{{ProcessOperationContractNames.ReadProjectStructure}}",
                "{{ProcessOperationContractNames.ExecuteExternalAction}}"
              ]
            }
            """,
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")));
        metadataJson = ExecutionInvocationMetadata.ApplyProjectStructureLaunchAgent(metadataJson, launchAgent);
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: null,
            Title: "Process scoped project-structure provider test",
            SourceKind: "process-step",
            SourceId: Guid.NewGuid().ToString("D"),
            CorrelationId: Guid.NewGuid().ToString("D"),
            CausationId: string.Empty,
            RequestedBy: "process-runtime",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            InputSummary: "Test process-scoped project-structure access.",
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test",
            State: ExecutionState.Preparing,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: Guid.NewGuid().ToString("D"),
            ProcessStepId: Guid.NewGuid().ToString("D"));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution",
            Status = ProjectStatus.Active
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static ProjectStructureAgentException AssertProjectStructureException(Exception? exception)
    {
        if (exception is ProjectStructureAgentException projectStructureException)
        {
            return projectStructureException;
        }

        if (exception?.InnerException is ProjectStructureAgentException innerProjectStructureException)
        {
            return innerProjectStructureException;
        }

        throw new Xunit.Sdk.XunitException(
            $"Expected {nameof(ProjectStructureAgentException)}, got {exception?.GetType().FullName ?? "<null>"}.");
    }

    private static T ReadToolResult<T>(object? result)
    {
        if (result is T typed)
        {
            return typed;
        }

        if (result is JsonElement jsonElement)
        {
            return jsonElement.Deserialize<T>(AgentOutputJson.SerializerOptions)
                   ?? throw new Xunit.Sdk.XunitException($"Tool result JSON could not be deserialized as {typeof(T).Name}.");
        }

        throw new Xunit.Sdk.XunitException(
            $"Expected tool result {typeof(T).Name}, got {result?.GetType().FullName ?? "<null>"}.");
    }
}
