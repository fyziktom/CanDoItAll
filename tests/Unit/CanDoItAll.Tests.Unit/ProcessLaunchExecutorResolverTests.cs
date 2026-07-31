using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessLaunchExecutorResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly ProcessInstancePlanId PlanId = new(new Guid("04991f6e-a37d-45f7-9a65-362c2cb4fef4"));
    private static readonly ProcessStepInstanceId StepId = new(new Guid("f0fcfb5c-4b35-475a-a9a7-bfe3c7bc270d"));
    private static readonly ProcessStrategyBindingSnapshot Binding = new(
        new DriverId("driver.test"),
        new StrategyId("strategy.test"),
        "1.0.0",
        "factory.1.0.0",
        "runtime.1",
        "runtime.1",
        "sha256:binding",
        []);

    [Fact]
    public async Task ResolveAsync_binds_process_local_role_through_shared_role_resource_key()
    {
        var providerId = Guid.Parse("0a7675bb-d7f5-46d0-8c7f-40fdf22df893");
        var agent = CreateAgent(providerId);
        var workspace = new ResolverWorkspaceService([agent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = new AgentFrameworkProcessLaunchExecutorResolver(
            CreateReferenceDataProvider(workspace),
            new ProcessMockAgentCatalogService(
                workspaceFactory,
                new NoOpAiTechnicalAgentBridge(),
                Options.Create(new ProcessMockAgentOptions { Enabled = false })),
            new ProviderProfileService(),
            new ResolverWorkflowCatalog());

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateDefinition(),
            CreatePlan(),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("record-runtime-commands", binding.StepKey);
        Assert.Equal("runtime-command-recorder", binding.RoleKey);
        Assert.Equal(ProcessLaunchExecutorKinds.Agent, binding.ExecutorKind);
        Assert.Equal(agent.Id.ToString("D"), binding.ExecutorId);
        Assert.Contains("delivery-manager", binding.AssignmentReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_binds_only_the_explicit_workflow_and_latest_active_version()
    {
        var workflowId = WorkflowId.New();
        var active = CreateWorkflowDefinition(workflowId, WorkflowVersionId.New(), WorkflowLifecycleStatus.Active);
        var catalog = new ResolverWorkflowCatalog
        {
            LatestActive = new WorkflowDefinitionDetail(active, WorkflowValidationResult.Success)
        };
        var workspace = new ResolverWorkspaceService([], []);
        var resolver = CreateResolver(new ResolverWorkspaceFactory(workspace), catalog);
        var definition = CreateDefinition();
        definition.RoleUsages[0].PreferredExecutorKind = ProcessLaunchExecutorKinds.Workflow;
        definition.RoleUsages[0].WorkflowBinding = new ProcessWorkflowExecutorBinding(
            new ProcessWorkflowId(workflowId.Value));

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan(),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal(ProcessLaunchExecutorKinds.Workflow, binding.ExecutorKind);
        Assert.Equal(workflowId.Value, binding.WorkflowBinding?.WorkflowId.Value);
        Assert.Null(binding.WorkflowBinding?.WorkflowVersionId);
        Assert.Equal(workflowId, catalog.LatestActiveWorkflowId);
        Assert.False(catalog.ListDefinitionsCalled);
    }

    [Fact]
    public async Task ResolveAsync_rejects_exact_workflow_version_that_is_not_active_and_runnable()
    {
        var workflowId = WorkflowId.New();
        var versionId = WorkflowVersionId.New();
        var draft = CreateWorkflowDefinition(workflowId, versionId, WorkflowLifecycleStatus.Draft);
        var catalog = new ResolverWorkflowCatalog
        {
            Exact = new WorkflowDefinitionDetail(draft, WorkflowValidationResult.Success)
        };
        var workspace = new ResolverWorkspaceService([], []);
        var resolver = CreateResolver(new ResolverWorkspaceFactory(workspace), catalog);
        var definition = CreateDefinition();
        definition.RoleUsages[0].PreferredExecutorKind = ProcessLaunchExecutorKinds.Workflow;
        definition.RoleUsages[0].WorkflowBinding = new ProcessWorkflowExecutorBinding(
            new ProcessWorkflowId(workflowId.Value),
            new ProcessWorkflowVersionId(versionId.Value));

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan(),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.Empty(result.Bindings);
        Assert.Contains(result.Findings, finding =>
            finding.Code == "process.launch.workflow_not_runnable" &&
            finding.Severity == ProcessLaunchReadinessSeverity.Error);
        Assert.Equal((workflowId, versionId), catalog.ExactSelection);
    }

    [Fact]
    public async Task ResolveAsync_rejects_workflow_kind_without_explicit_workflow_instead_of_selecting_any_active()
    {
        var catalog = new ResolverWorkflowCatalog
        {
            LatestActive = new WorkflowDefinitionDetail(
                CreateWorkflowDefinition(WorkflowId.New(), WorkflowVersionId.New(), WorkflowLifecycleStatus.Active),
                WorkflowValidationResult.Success)
        };
        var workspace = new ResolverWorkspaceService([], []);
        var resolver = CreateResolver(new ResolverWorkspaceFactory(workspace), catalog);
        var definition = CreateDefinition();
        definition.RoleUsages[0].PreferredExecutorKind = ProcessLaunchExecutorKinds.Workflow;

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan(),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.Empty(result.Bindings);
        Assert.Contains(result.Findings, finding =>
            finding.Code == "process.launch.workflow_selection_required" &&
            finding.Severity == ProcessLaunchReadinessSeverity.Error);
        Assert.Null(catalog.LatestActiveWorkflowId);
        Assert.False(catalog.ListDefinitionsCalled);
    }

    [Fact]
    public async Task ResolveAsync_surfaces_management_only_skill_suppression_without_changing_agent_settings()
    {
        var providerId = Guid.Parse("632a32cd-bcc7-4467-96c3-4f5292f5db42");
        var agent = CreateAgent(
            providerId,
            Guid.Parse("a70383fc-68d7-4032-b052-c63ed53b6d36"),
            "Technical Delivery Manager",
            "Delivery manager with engineering background",
            "Coordinates delivery governance and can also support engineering work when a process step allows it.",
            AgentWorkloadKind.Management,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["delivery-manager", "dotnet", "programming"]);
        var workspace = new ResolverWorkspaceService([agent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = CreateDefinition();
        definition.Steps[0].CapabilityScope = new ProcessCapabilityScope
        {
            Directives =
            [
                new ProcessCapabilityScopeDirective
                {
                    Kind = ProcessCapabilityScopeDirectiveKind.Deny,
                    Target = new ProcessCapabilityScopeTarget
                    {
                        Kind = ProcessCapabilityScopeTargetKind.CapabilityKind,
                        Value = CapabilityKind.Skill.ToString()
                    },
                    Reason = "Management-only step."
                }
            ]
        };

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan(),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal(agent.Id.ToString("D"), binding.ExecutorId);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Info &&
            finding.Code == "process.launch.capability_suppressed" &&
            finding.Message.Contains("CapabilityKind:Skill", StringComparison.Ordinal) &&
            finding.Message.Contains("Management-only", StringComparison.Ordinal));
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Info &&
            finding.Code == "process.launch.readiness_ok");
    }

    [Fact]
    public async Task ResolveAsync_binds_required_scoped_workspace_tool_when_profile_exposes_it()
    {
        var providerId = Guid.Parse("91caa057-c4e7-44d6-9796-100adcf2dd93");
        var agent = CreateAgent(providerId);
        var workspace = new ResolverWorkspaceService([agent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = CreateDefinition();
        definition.Steps[0].CapabilityScope = CreateRequiredWorkspaceToolScope("workspace-analyze-image");

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan(),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal(agent.Id.ToString("D"), binding.ExecutorId);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Info &&
            finding.Code == "process.launch.capability_required" &&
            finding.Message.Contains("CapabilityIdentity:Tool/workspace-analyze-image", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_rejects_required_scoped_workspace_tool_when_profile_cannot_expose_it()
    {
        var providerId = Guid.Parse("ba3bc84f-7e95-4e5a-a93d-af2fe164ebf1");
        var agent = CreateAgent(
            providerId,
            Guid.Parse("5de5fb45-aa57-4ea0-ae9f-472a963f1cd8"),
            "Read-only Delivery Manager",
            "Delivery Manager",
            "Coordinates delivery governance with read-only workspace access.",
            AgentWorkloadKind.Management,
            AgentWorkspaceToolProfileKind.ReadOnly,
            ["delivery-manager"]);
        var workspace = new ResolverWorkspaceService([agent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = CreateDefinition();
        definition.Steps[0].CapabilityScope = CreateRequiredWorkspaceToolScope("workspace-analyze-image");

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan(),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.Empty(result.Bindings);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "agent.readiness.required-capability-missing" &&
            finding.Message.Contains("workspace-analyze-image", StringComparison.Ordinal) &&
            finding.Message.Contains(agent.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_selects_execution_ready_engineering_agent_for_mutable_blazor_step()
    {
        var providerId = Guid.Parse("4ac3d05d-d0e5-4ab9-85f3-76d2366c7226");
        var deliveryAgent = CreateAgent(providerId);
        var developerAgent = CreateAgent(
            providerId,
            Guid.Parse("d24b3b45-25f5-43b1-9a08-343159756aa8"),
            "Programming Workspace Analyst",
            ".NET Developer",
            "Builds Blazor and .NET application features with workspace validation.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["lead-engineer", "blazor-engineer", "dotnet-developer"]);
        var workspace = new ResolverWorkspaceService([deliveryAgent, developerAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateBlazorImplementationDefinition(),
            CreatePlan("implement-blazor-change"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("implement-blazor-change", binding.StepKey);
        Assert.Equal("blazor-engineer", binding.RoleKey);
        Assert.Equal(developerAgent.Id.ToString("D"), binding.ExecutorId);
        Assert.NotEqual(deliveryAgent.Id.ToString("D"), binding.ExecutorId);
        Assert.Contains("workspace tool readiness", binding.AssignmentReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_allows_tool_capable_local_provider_for_governed_process_finalizer()
    {
        var providerId = Guid.Parse("1e7f1a94-42b6-46e3-8f24-0342cb31c980");
        var developerAgent = CreateAgent(
            providerId,
            Guid.Parse("b327b3f0-0d70-4119-bf53-2cd6c7b4a9f6"),
            ".NET Application Developer",
            ".NET implementation specialist",
            "Implements C#, ASP.NET Core, and Blazor deliverables with real source changes, focused tests, and runnable proof.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["dotnet", "programming", "blazor"]);
        var localProvider = CreateProvider(providerId, ProviderKind.Ollama);
        var featureMatrix = new ProviderProfileService().ResolveFeatureMatrix(localProvider);
        var workspace = new ResolverWorkspaceService([developerAgent], [localProvider]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateBlazorImplementationDefinition(),
            CreatePlan("implement-blazor-change"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.False(featureMatrix.SupportsStructuredOutput);
        Assert.True(featureMatrix.SupportsFunctionTools);
        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("implement-blazor-change", binding.StepKey);
        Assert.Equal("blazor-engineer", binding.RoleKey);
        Assert.Equal(developerAgent.Id.ToString("D"), binding.ExecutorId);
    }

    [Fact]
    public async Task ResolveAsync_rejects_local_provider_without_tool_finalizer_support()
    {
        var providerId = Guid.Parse("388d2d44-e423-4d26-8b92-7d260089a0b0");
        var developerAgent = CreateAgent(
            providerId,
            Guid.Parse("7987805f-cf9c-4089-9b84-5eb26f0459d8"),
            ".NET Application Developer",
            ".NET implementation specialist",
            "Implements C#, ASP.NET Core, and Blazor deliverables with real source changes, focused tests, and runnable proof.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["dotnet", "programming", "blazor"]);
        var localProvider = CreateProvider(providerId, ProviderKind.Ollama, supportsTools: false);
        var workspace = new ResolverWorkspaceService([developerAgent], [localProvider]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateBlazorImplementationDefinition(),
            CreatePlan("implement-blazor-change"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.Empty(result.Bindings);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "process.launch.agent_missing" &&
            finding.Message.Contains("governed-output-capable provider", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolveAsync_selects_dotnet_developer_for_software_engineer_solution_scaffold_step()
    {
        var providerId = Guid.Parse("c90be131-77c4-4393-89d6-3e2b29128950");
        var deliveryAgent = CreateAgent(providerId);
        var developerAgent = CreateAgent(
            providerId,
            Guid.Parse("3c7983c4-15cc-4e4e-9296-f181396d7a79"),
            ".NET Application Developer",
            ".NET implementation specialist",
            "Implements C#, ASP.NET Core, and Blazor deliverables with real source changes, focused tests, and runnable proof.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["dotnet", "programming", "blazor"]);
        var workspace = new ResolverWorkspaceService([deliveryAgent, developerAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateSoftwareEngineerScaffoldDefinition(),
            CreatePlan("create-dotnet-project"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("create-dotnet-project", binding.StepKey);
        Assert.Equal("software-engineer", binding.RoleKey);
        Assert.Equal(developerAgent.Id.ToString("D"), binding.ExecutorId);
        Assert.NotEqual(deliveryAgent.Id.ToString("D"), binding.ExecutorId);
    }

    [Fact]
    public async Task ResolveAsync_prefers_dotnet_developer_over_generic_programming_agent_for_dotnet_setup()
    {
        var providerId = Guid.Parse("1560d4c3-ee36-40c0-b7c0-3a2d48c40f55");
        var programmingAgent = CreateAgent(
            providerId,
            Guid.Parse("2483c010-f722-4a62-bbb8-ee6b340afbea"),
            "Programming Workspace Analyst",
            "Programming and repository worker",
            "Implements C# and Blazor changes with bounded source inspection, concrete validation, and real UI proof when needed.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["programming", "workspace", "approval"]);
        var dotnetDeveloperAgent = CreateAgent(
            providerId,
            Guid.Parse("4905245d-89b0-40c4-b28f-8fae42783a56"),
            ".NET Application Developer",
            ".NET implementation specialist",
            "Implements C#, ASP.NET Core, and Blazor deliverables with real source changes, focused tests, and runnable proof.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["dotnet", "programming", "blazor"],
            CreateDotNetSetupToolCapabilities());
        var workspace = new ResolverWorkspaceService([programmingAgent, dotnetDeveloperAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = LoadTemplateDefinition("dotnet-solution-setup");

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan("create-dotnet-project"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("create-dotnet-project", binding.StepKey);
        Assert.Equal("software-engineer", binding.RoleKey);
        Assert.Equal(dotnetDeveloperAgent.Id.ToString("D"), binding.ExecutorId);
        Assert.NotEqual(programmingAgent.Id.ToString("D"), binding.ExecutorId);
    }

    [Fact]
    public async Task ResolveAsync_uses_template_specialization_tags_to_select_dotnet_qa_over_generic_qa()
    {
        var providerId = Guid.Parse("b7cc83e6-2d22-43a8-8b71-5c4320231ff8");
        var genericQaAgent = CreateAgent(
            providerId,
            Guid.Parse("f653b0a9-c090-4be8-898d-2b82bc581963"),
            "Delivery QA Observer",
            "QA lead",
            "Reviews generic delivery evidence.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.QualityValidation,
            ["qa"]);
        var dotnetQaAgent = CreateAgent(
            providerId,
            Guid.Parse("280d3904-33e5-4207-9524-c4b50cd6dff6"),
            ".NET QA Review Lead",
            ".NET QA specialist",
            "Reviews .NET product evidence.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.QualityValidation,
            ["dotnet", "qa"]);
        var workspace = new ResolverWorkspaceService([genericQaAgent, dotnetQaAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = new ProcessTemplateDefinitionDocument
        {
            Key = "specialized-qa-test",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "qa-lead",
                    RoleResourceKey = "qa-lead",
                    DisplayName = "QA lead",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "QA",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "validate",
                    Title = "Validate product",
                    StepKind = "Review",
                    ExecutorPreferredSpecializationTags = ["dotnet", "qa"],
                    AllowedOperations = [ProcessOperationContractNames.ReadProcessContext],
                    OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetReadOnly,
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "qa-lead",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true
                        }
                    ]
                }
            ]
        };

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan("validate"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal(dotnetQaAgent.Id.ToString("D"), binding.ExecutorId);
        Assert.NotEqual(genericQaAgent.Id.ToString("D"), binding.ExecutorId);
        Assert.Contains("preferred specialization", binding.AssignmentReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_rejects_dotnet_setup_template_when_agent_lacks_required_tool_capability()
    {
        var providerId = Guid.Parse("52ee790f-23d9-432c-8e8e-c15ca95115fb");
        var dotnetDeveloperAgent = CreateAgent(
            providerId,
            Guid.Parse("95e9dcc1-577c-42b5-82e4-1d46414872a0"),
            ".NET Application Developer",
            ".NET implementation specialist",
            "Implements C#, ASP.NET Core, and Blazor deliverables with real source changes, focused tests, and runnable proof.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["dotnet", "programming", "blazor"]);
        var workspace = new ResolverWorkspaceService([dotnetDeveloperAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = LoadTemplateDefinition("dotnet-solution-setup");

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan("create-dotnet-project"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.Empty(result.Bindings);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "agent.readiness.required-tool-capability-missing" &&
            finding.Message.Contains("workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolveAsync_prefers_dotnet_developer_over_generic_programming_agent_for_dotnet_feature_code_change()
    {
        var providerId = Guid.Parse("d1363d57-2a40-47d2-b78b-d2f3ad72f92b");
        var programmingAgent = CreateAgent(
            providerId,
            Guid.Parse("24f95fe0-329a-4f6c-9a37-f4bbbc133be9"),
            "Programming Workspace Analyst",
            "Programming and repository worker",
            "Implements C# and Blazor changes with bounded source inspection, concrete validation, and real UI proof when needed.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["programming", "workspace", "approval"]);
        var dotnetDeveloperAgent = CreateAgent(
            providerId,
            Guid.Parse("40b80c73-c8db-49bd-bfae-128df8f62f49"),
            ".NET Application Developer",
            ".NET implementation specialist",
            "Implements C#, ASP.NET Core, and Blazor deliverables with real source changes, focused tests, and runnable proof.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["dotnet", "programming", "blazor"]);
        var workspace = new ResolverWorkspaceService([programmingAgent, dotnetDeveloperAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = LoadTemplateDefinition("dotnet-feature-function-implementation");

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan("code-change"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("code-change", binding.StepKey);
        Assert.Equal("software-engineer", binding.RoleKey);
        Assert.Equal(dotnetDeveloperAgent.Id.ToString("D"), binding.ExecutorId);
        Assert.NotEqual(programmingAgent.Id.ToString("D"), binding.ExecutorId);
    }

    [Fact]
    public async Task ResolveAsync_allows_read_only_feature_handoff_and_escalation_steps()
    {
        var providerId = Guid.Parse("54d0bbeb-bd24-4289-a89e-1d3b89c71837");
        var deliveryAgent = CreateAgent(providerId);
        var workspace = new ResolverWorkspaceService([deliveryAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = LoadTemplateDefinition("dotnet-feature-function-implementation");

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlanForSteps(
                "feature-handoff",
                "feature-handoff-after-repair",
                "feature-repair-escalation"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding =>
            finding.Code == "process.launch.step_operation_contract_invalid");
        Assert.Equal(3, result.Bindings.Count);
        Assert.All(result.Bindings, binding => Assert.Equal("delivery-manager", binding.RoleKey));
    }

    [Fact]
    public async Task ResolveAsync_uses_role_identity_not_step_title_as_delivery_manager_family_gate()
    {
        var providerId = Guid.Parse("9f7eac2d-9d8f-4a14-9e29-0d22f5da73aa");
        var deliveryAgent = CreateAgent(providerId);
        var qaAgent = CreateAgent(
            providerId,
            Guid.Parse("83ed0fa9-75e8-4c38-9883-ec1ba1ad806e"),
            ".NET QA Review Lead",
            ".NET QA Review Lead",
            "Reviews .NET architecture handoffs and validation evidence.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["qa-lead", "dotnet", "architecture", "review"]);
        var workspace = new ResolverWorkspaceService([deliveryAgent, qaAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateDeliveryManagerArchitectureHandoffDefinition(),
            CreatePlan("architecture-handoff"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("architecture-handoff", binding.StepKey);
        Assert.Equal("delivery-manager", binding.RoleKey);
        Assert.Equal(deliveryAgent.Id.ToString("D"), binding.ExecutorId);
        Assert.NotEqual(qaAgent.Id.ToString("D"), binding.ExecutorId);
    }

    [Fact]
    public void Software_delivery_template_keeps_technical_subprocess_steps_owned_by_technical_roles()
    {
        var templateLoader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        var definition = templateLoader.LoadDefinition("software-delivery");

        AssertResponsibleRole(definition, "architecture-review", "solution-architect");
        AssertResponsibleRole(definition, "implementation", "lead-engineer");
        AssertReviewerRole(definition, "architecture-review", "delivery-manager");
        AssertReviewerRole(definition, "implementation", "delivery-manager");
    }

    [Fact]
    public async Task ResolveAsync_selects_architect_and_developer_for_parent_dotnet_subprocess_steps()
    {
        var providerId = Guid.Parse("0acc678e-cde0-4722-9e9e-914d253d27d6");
        var deliveryAgent = CreateAgent(providerId);
        var architectAgent = CreateAgent(
            providerId,
            Guid.Parse("9f1a2e24-b4b2-4eba-bbfb-0430dbe01931"),
            ".NET Architect",
            ".NET Architect",
            "Designs .NET architecture, reviews boundaries, and prepares implementation constraints.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["solution-architect", "dotnet-architect", "architecture"]);
        var codeReviewLead = CreateAgent(
            providerId,
            Guid.Parse("8a1a6873-3c60-4815-b4b7-0cdb1310f52c"),
            "Code Review Lead",
            "Code reviewer",
            "Owns code-review lane selection, quality normalization, and escalation discipline.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.QualityValidation,
            ["review", "code", "quality"],
            [
                new AgentCapabilityAssignment(
                    Guid.Parse("306bb943-3dc0-4c44-a703-226cf2b58df0"),
                    "architecture-source-rag",
                    CapabilityKind.Rag,
                    CapabilityProofStatus.Verified,
                    Now,
                    "Available for architecture source lookup.")
            ]);
        var qaAgent = CreateAgent(
            providerId,
            Guid.Parse("5b408530-7f41-4f5c-aacf-fb4807bcd6b2"),
            ".NET QA Review Lead",
            ".NET QA Review Lead",
            "Reviews .NET architecture handoffs and validation evidence.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["qa-lead", "dotnet", "architecture", "review"]);
        var developerAgent = CreateAgent(
            providerId,
            Guid.Parse("2975f892-3ceb-44b9-80e7-19327551a10f"),
            ".NET Developer",
            ".NET Developer",
            "Implements .NET features, validates builds, and writes managed process artifacts.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["lead-engineer", "dotnet-developer", "implementation"]);
        var workspace = new ResolverWorkspaceService([deliveryAgent, codeReviewLead, qaAgent, architectAgent, developerAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateTechnicalSubprocessDefinition(),
            CreateTechnicalSubprocessPlan(),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        Assert.Collection(
            result.Bindings.OrderBy(binding => binding.StepKey, StringComparer.Ordinal).ToArray(),
            binding =>
            {
                Assert.Equal("architecture-review", binding.StepKey);
                Assert.Equal("solution-architect", binding.RoleKey);
                Assert.Equal(architectAgent.Id.ToString("D"), binding.ExecutorId);
                Assert.NotEqual(deliveryAgent.Id.ToString("D"), binding.ExecutorId);
                Assert.NotEqual(codeReviewLead.Id.ToString("D"), binding.ExecutorId);
                Assert.NotEqual(qaAgent.Id.ToString("D"), binding.ExecutorId);
            },
            binding =>
            {
                Assert.Equal("implementation", binding.StepKey);
                Assert.Equal("lead-engineer", binding.RoleKey);
                Assert.Equal(developerAgent.Id.ToString("D"), binding.ExecutorId);
                Assert.NotEqual(deliveryAgent.Id.ToString("D"), binding.ExecutorId);
            });
    }

    [Fact]
    public async Task ResolveAsync_prefers_software_architect_over_business_strategist_for_dotnet_architecture()
    {
        var providerId = Guid.Parse("25b46f25-e394-4c8e-88d5-f59c7393f17e");
        var businessStrategist = CreateAgent(
            providerId,
            Guid.Parse("7e01b68d-e14a-45b0-9e1d-92571f79d778"),
            "Business Strategist",
            "Business planning specialist",
            "Creates grounded business plans, operating assumptions, risk views, and cross-functional handoffs for non-code processes.",
            AgentWorkloadKind.Management,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["business", "strategy", "planning"]);
        var architectAgent = CreateAgent(
            providerId,
            Guid.Parse("6e2c83ef-d4fd-46f2-b917-c9d0c503eb04"),
            ".NET Solution Architect",
            ".NET architecture specialist",
            "Designs maintainable C#, ASP.NET Core, and Blazor project structures with explicit boundaries and validation plans.",
            AgentWorkloadKind.Research,
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            ["dotnet", "architecture", "blazor"]);
        var workspace = new ResolverWorkspaceService([businessStrategist, architectAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateTechnicalSubprocessDefinition(),
            CreatePlan("architecture-review"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("architecture-review", binding.StepKey);
        Assert.Equal("solution-architect", binding.RoleKey);
        Assert.Equal(architectAgent.Id.ToString("D"), binding.ExecutorId);
        Assert.NotEqual(businessStrategist.Id.ToString("D"), binding.ExecutorId);
    }

    [Fact]
    public async Task ResolveAsync_allows_structurally_valid_subprocess_contract_regardless_of_role_or_title_text()
    {
        var providerId = Guid.Parse("e785895d-a164-4175-a28f-9cc12fdf7fa5");
        var architectAgent = CreateAgent(
            providerId,
            Guid.Parse("d30e8b65-5c70-4ab6-9f94-3313ea3d3185"),
            ".NET Solution Architect",
            ".NET architecture specialist",
            "Designs maintainable C#, ASP.NET Core, and Blazor project structures with explicit boundaries and validation plans.",
            AgentWorkloadKind.Research,
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            ["dotnet", "architecture", "blazor"]);
        var workspace = new ResolverWorkspaceService([architectAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = CreateTechnicalSubprocessDefinition();
        var architectureStep = Assert.Single(definition.Steps, step => string.Equals(step.Key, "architecture-review", StringComparison.Ordinal));
        var architectureRole = Assert.Single(definition.RoleUsages, role => string.Equals(role.Key, "solution-architect", StringComparison.Ordinal));
        architectureRole.DisplayName = "Řídící koordinátor 合意";
        architectureStep.Title = "Příprava provozního návrhu";
        architectureStep.AllowedOperations =
        [
            .. architectureStep.AllowedOperations,
            ProcessOperationContractNames.LaunchRuntime,
            ProcessOperationContractNames.CaptureRuntimeProof
        ];

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan("architecture-review"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding =>
            finding.Code == "process.launch.step_operation_contract_invalid");
    }

    [Fact]
    public async Task ResolveAsync_rejects_subprocess_contract_without_child_execution_operation_or_controlled_scope()
    {
        var providerId = Guid.Parse("e8bca6d2-5ef8-41a7-a9a1-ec856eb7e1f4");
        var workspace = new ResolverWorkspaceService([CreateAgent(providerId)], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = CreateTechnicalSubprocessDefinition();
        var architectureStep = Assert.Single(definition.Steps, step => string.Equals(step.Key, "architecture-review", StringComparison.Ordinal));
        architectureStep.Title = "Spuštění 子流程";
        architectureStep.AllowedOperations =
        [
            ProcessOperationContractNames.ReadProcessContext,
            ProcessOperationContractNames.WriteManagedProcessArtifacts
        ];
        architectureStep.OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetReadOnly;

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan("architecture-review"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.Empty(result.Bindings);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "process.launch.step_operation_contract_invalid" &&
            finding.Message.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparison.Ordinal));
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "process.launch.step_operation_contract_invalid" &&
            finding.Message.Contains(ProcessOperationContractNames.ExternalActionControlled, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolveAsync_allows_screenshot_subprocess_contract_with_runtime_proof_tools()
    {
        var providerId = Guid.Parse("5b51541e-96a7-4d9f-a5fe-0afc27bbd24c");
        var qaAgent = CreateAgent(
            providerId,
            Guid.Parse("0454bfbf-00b6-4d1d-9cf6-e9e27f889b26"),
            ".NET QA Review Lead",
            ".NET QA Review Lead",
            "Captures UI screenshot proof and records validation evidence.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["qa-lead", "dotnet", "screenshots", "validation"]);
        var workspace = new ResolverWorkspaceService([qaAgent], [CreateProvider(providerId, ProviderKind.Ollama)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateScreenshotSubprocessDefinition(),
            CreatePlan("capture-ui-screenshots"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding =>
            finding.Code == "process.launch.step_operation_contract_invalid");
    }

    [Fact]
    public async Task ResolveAsync_rejects_required_workspace_tool_when_agent_lacks_capability_assignment()
    {
        var providerId = Guid.Parse("a7e02a87-0f51-49e4-a38a-4253eac29c18");
        var qaAgent = CreateAgent(
            providerId,
            Guid.Parse("b98eb57d-d908-45c3-8f0b-a162fa2f98f4"),
            ".NET QA Review Lead",
            ".NET QA Review Lead",
            "Captures UI screenshot proof and records validation evidence.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.QualityValidation,
            ["qa-lead", "dotnet", "screenshots", "validation"],
            [
                new AgentCapabilityAssignment(
                    Guid.Parse("5ec990bf-7f6b-489e-98b2-fb224da5b730"),
                    "workspace-dotnet-build",
                    CapabilityKind.Tool,
                    CapabilityProofStatus.Verified,
                    Now,
                    "Build tool is available.")
            ]);
        var workspace = new ResolverWorkspaceService([qaAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["capture-ui-screenshots"] = ["workspace_dotnet_run"]
        });

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateScreenshotSubprocessDefinition(),
            CreatePlan("capture-ui-screenshots"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = requiredToolReceiptMap
            }));

        Assert.Empty(result.Bindings);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "agent.readiness.required-tool-capability-missing" &&
            finding.Message.Contains("workspace_dotnet_run", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolveAsync_keeps_unconditional_receipts_and_ignores_branch_scoped_receipts_before_branch_selection()
    {
        var providerId = Guid.Parse("8b431ac3-88f5-4803-9069-57869fc8488d");
        var agentWithoutUnconditionalTools = CreateAgent(
            providerId,
            Guid.Parse("f8fb741a-c67c-4676-b58c-e2a36afc4bcf"),
            "A .NET QA Review Lead",
            ".NET QA Review Lead",
            "Captures UI screenshot proof and records validation evidence.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.QualityValidation,
            ["qa-lead", "dotnet", "screenshots", "validation"]);
        var agentWithUnconditionalTools = CreateAgent(
            providerId,
            Guid.Parse("85736e79-687e-413d-ae1b-a4fe196f9e5d"),
            "Z .NET QA Review Lead",
            ".NET QA Review Lead",
            "Captures UI screenshot proof and records validation evidence.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.QualityValidation,
            ["qa-lead", "dotnet", "screenshots", "validation"],
            [
                CreateToolCapability("workspace-dotnet-build"),
                CreateToolCapability("workspace-dotnet-test")
            ]);
        var workspace = new ResolverWorkspaceService(
            [agentWithoutUnconditionalTools, agentWithUnconditionalTools],
            [CreateProvider(providerId)]);
        var resolver = CreateResolver(new ResolverWorkspaceFactory(workspace));
        var definition = CreateScreenshotSubprocessDefinition();
        definition.Steps[0].CapabilityScope = new ProcessCapabilityScope
        {
            RequiredReceipts =
            [
                new ProcessRequiredToolReceipt
                {
                    Key = "unconditional-test",
                    ToolName = "workspace_dotnet_test"
                },
                new ProcessRequiredToolReceipt
                {
                    Key = "accepted-branch-restore",
                    ToolName = "workspace_dotnet_restore",
                    ApplicableBranchOutcomeKeys = ["quality-accepted"]
                }
            ]
        };
        var requiredToolReceiptMap = JsonSerializer.Serialize(
            new Dictionary<string, object[]>(StringComparer.Ordinal)
            {
                ["capture-ui-screenshots"] =
                [
                    new
                    {
                        ToolName = "workspace_dotnet_build"
                    },
                    new
                    {
                        ToolName = "workspace_dotnet_run",
                        ApplicableBranchOutcomeKeys = new[] { "quality-accepted" }
                    }
                ]
            });

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan("capture-ui-screenshots"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = requiredToolReceiptMap
            }));

        Assert.DoesNotContain(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal(agentWithUnconditionalTools.Id.ToString("D"), binding.ExecutorId);
        Assert.NotEqual(agentWithoutUnconditionalTools.Id.ToString("D"), binding.ExecutorId);
    }

    [Fact]
    public async Task ResolveAsync_rejects_required_project_structure_write_tool_when_agent_lacks_write_access()
    {
        var providerId = Guid.Parse("791c85f9-88ab-41d0-a093-a14bfd8ac1b6");
        var qaAgent = CreateAgent(
            providerId,
            Guid.Parse("b1e1e6f1-1bd6-4c29-ae41-0dfebbc64c85"),
            "Delivery QA Observer",
            "QA lead and browser-proof reviewer",
            "Captures UI screenshot proof and records validation evidence.",
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.QualityValidation,
            ["qa-lead", "browser", "screenshots", "validation"]) with
        {
            ConfigurationJson = AgentProjectStructureAccessMetadata.Write(
                AgentWorkspaceToolAccessMetadata.Write(
                    "{}",
                    AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.QualityValidation)),
                new AgentProjectStructureAccessSettings
                {
                    CanRead = true,
                    CanWrite = false,
                    AllowAllProjects = true
                })
        };
        var workspace = new ResolverWorkspaceService([qaAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var requiredToolReceiptMap = JsonSerializer.Serialize(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["store-ui-screenshots"] = ["project_structure_node_create", "project_structure_asset_create"]
        });

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateScreenshotStorageDefinition(),
            CreatePlan("store-ui-screenshots"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] = requiredToolReceiptMap
            }));

        Assert.Empty(result.Bindings);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "agent.readiness.required-project-structure-write-missing" &&
            finding.Message.Contains("project_structure_asset_create", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResolveAsync_allows_activity_contract_regardless_of_localized_role_or_title_text()
    {
        var providerId = Guid.Parse("b4309322-2334-4b52-805b-5fa16e290043");
        var developerAgent = CreateAgent(
            providerId,
            Guid.Parse("f73f240c-5feb-4067-a67b-0572b4fbd52a"),
            ".NET Developer",
            ".NET Developer",
            "Implements .NET features, validates builds, and writes managed process artifacts.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["lead-engineer", "dotnet-developer", "implementation"]);
        var workspace = new ResolverWorkspaceService([developerAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);
        var definition = CreateLocalizedActivityDefinition();

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            definition,
            CreatePlan("doplnit-実装"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding =>
            finding.Code == "process.launch.step_operation_contract_invalid");
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("doplnit-実装", binding.StepKey);
        Assert.Equal("vývojář-検証", binding.RoleKey);
        Assert.Equal(developerAgent.Id.ToString("D"), binding.ExecutorId);
    }

    [Fact]
    public async Task Runtime_assignment_repair_rebinds_stale_qa_reviewer_to_dotnet_architect()
    {
        var providerId = Guid.Parse("f14f722f-6562-4fa4-a29a-68fe7f6892ef");
        var qaReviewLead = CreateAgent(
            providerId,
            Guid.Parse("752665f3-bf7d-0b54-9208-7809282cd415"),
            ".NET QA Review Lead",
            string.Empty,
            string.Empty,
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.QualityValidation,
            ["dotnet", "qa", "browser"]);
        var architectAgent = CreateAgent(
            providerId,
            Guid.Parse("484b504d-5f6e-40ea-9af2-0bc18b1395a1"),
            ".NET Solution Architect",
            string.Empty,
            string.Empty,
            AgentWorkloadKind.Research,
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            ["dotnet", "architecture", "blazor"]);
        var workspace = new ResolverWorkspaceService([qaReviewLead, architectAgent], [CreateProvider(providerId)]);
        var repairService = new AgentFrameworkProcessRuntimeStepAssignmentRepairService(
            CreateReferenceDataProvider(workspace),
            new ProviderProfileService());
        var assignment = CreateArchitectureReviewAssignment(qaReviewLead);

        var result = await repairService.RepairAsync(assignment, "Operator approved manager-guided rework.");

        Assert.True(result.Repaired);
        Assert.Equal(architectAgent.Id.ToString("D"), result.Assignment.ExecutorId);
        Assert.Equal(architectAgent.Name, result.Assignment.ExecutorDisplayName);
        Assert.Contains("Reassigned step 'architecture-review'", result.Summary, StringComparison.Ordinal);
        Assert.Contains(architectAgent.Name, result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_assignment_repair_keeps_unconditional_receipts_and_ignores_branch_scoped_receipts_before_branch_selection()
    {
        var providerId = Guid.Parse("6f0b8b79-58e9-4b52-a991-0be90ea34b5e");
        var qaReviewLead = CreateAgent(
            providerId,
            Guid.Parse("58e96050-c0da-4dd3-98dd-435d1843bd6d"),
            ".NET QA Review Lead",
            string.Empty,
            string.Empty,
            AgentWorkloadKind.Qa,
            AgentWorkspaceToolProfileKind.QualityValidation,
            ["dotnet", "qa", "browser"]);
        var architectWithoutUnconditionalTools = CreateAgent(
            providerId,
            Guid.Parse("63141ad3-66a9-46cb-b252-1e2d0f8cbdcc"),
            "A .NET Solution Architect",
            ".NET Solution Architect",
            "Reviews .NET solution architecture.",
            AgentWorkloadKind.Research,
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            ["dotnet", "architecture", "blazor"]);
        var architectWithUnconditionalTools = CreateAgent(
            providerId,
            Guid.Parse("c217dd86-134a-469d-bbd5-9ad63101f105"),
            "Z .NET Solution Architect",
            ".NET Solution Architect",
            "Reviews .NET solution architecture.",
            AgentWorkloadKind.Research,
            AgentWorkspaceToolProfileKind.ArchitectureReview,
            ["dotnet", "architecture", "blazor"],
            [
                CreateToolCapability("workspace-read-file"),
                CreateToolCapability("workspace-stat-path")
            ]);
        var workspace = new ResolverWorkspaceService(
            [qaReviewLead, architectWithoutUnconditionalTools, architectWithUnconditionalTools],
            [CreateProvider(providerId)]);
        var repairService = new AgentFrameworkProcessRuntimeStepAssignmentRepairService(
            CreateReferenceDataProvider(workspace),
            new ProviderProfileService());
        var assignment = CreateArchitectureReviewAssignment(qaReviewLead) with
        {
            LaunchVariables = new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] =
                    JsonSerializer.Serialize(new object[]
                    {
                        new
                        {
                            ToolName = "workspace_read_file"
                        },
                        new
                        {
                            ToolName = "workspace_git_status",
                            ApplicableBranchOutcomeKeys = new[] { "architecture-accepted" }
                        }
                    })
            },
            CapabilityScope = new ProcessCapabilityScope
            {
                RequiredReceipts =
                [
                    new ProcessRequiredToolReceipt
                    {
                        Key = "unconditional-stat",
                        ToolName = "workspace_stat_path"
                    },
                    new ProcessRequiredToolReceipt
                    {
                        Key = "accepted-branch-search",
                        ToolName = "workspace_search",
                        ApplicableBranchOutcomeKeys = ["architecture-accepted"]
                    }
                ]
            }
        };

        var result = await repairService.RepairAsync(
            assignment,
            "Operator approved manager-guided rework.");

        Assert.True(result.Repaired);
        Assert.Equal(architectWithUnconditionalTools.Id.ToString("D"), result.Assignment.ExecutorId);
        Assert.NotEqual(architectWithoutUnconditionalTools.Id.ToString("D"), result.Assignment.ExecutorId);
    }

    [Fact]
    public async Task ResolveAsync_rejects_manual_override_when_agent_lacks_role_and_tools()
    {
        var providerId = Guid.Parse("3957ec37-15f0-4754-a75e-4ad0b32c9899");
        var deliveryAgent = CreateAgent(providerId);
        var developerAgent = CreateAgent(
            providerId,
            Guid.Parse("9115a47d-65f9-409e-8790-5901ec09fc95"),
            "Programming Workspace Analyst",
            ".NET Developer",
            "Builds Blazor and .NET application features with workspace validation.",
            AgentWorkloadKind.Programming,
            AgentWorkspaceToolProfileKind.SoftwareDevelopment,
            ["lead-engineer", "blazor-engineer", "dotnet-developer"]);
        var workspace = new ResolverWorkspaceService([deliveryAgent, developerAgent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = CreateResolver(workspaceFactory);

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateBlazorImplementationDefinition(),
            CreatePlan("implement-blazor-change"),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>())
        {
            ExecutorOverrides =
            [
                new ProcessLaunchExecutorOverride(
                    "implement-blazor-change",
                    "blazor-engineer",
                    ProcessLaunchExecutorKinds.Agent,
                    deliveryAgent.Id.ToString("D"),
                    deliveryAgent.Name,
                    "Selected during project-structure launch review.")
            ]
        });

        Assert.Empty(result.Bindings);
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "agent.readiness.role-family-mismatch");
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "agent.readiness.workspace-validation-missing");
        Assert.Contains(result.Findings, finding =>
            finding.Severity == ProcessLaunchReadinessSeverity.Error &&
            finding.Code == "agent.readiness.workspace-scaffold-missing");
    }

    private static AgentFrameworkProcessLaunchExecutorResolver CreateResolver(
        ResolverWorkspaceFactory workspaceFactory,
        IWorkflowCatalogService? workflowCatalog = null)
    {
        return new AgentFrameworkProcessLaunchExecutorResolver(
            CreateReferenceDataProvider(workspaceFactory.WorkspaceService),
            new ProcessMockAgentCatalogService(
                workspaceFactory,
                new NoOpAiTechnicalAgentBridge(),
                Options.Create(new ProcessMockAgentOptions { Enabled = false })),
            new ProviderProfileService(),
            workflowCatalog ?? new ResolverWorkflowCatalog());
    }

    private static IAgentReferenceDataProvider CreateReferenceDataProvider(IAgentFrameworkWorkspaceService workspaceService)
    {
        return new WorkspaceBackedAgentReferenceDataProvider(workspaceService, new AgentReferenceDataCache());
    }

    private static void AssertResponsibleRole(
        ProcessTemplateDefinitionDocument definition,
        string stepKey,
        string expectedRoleKey)
    {
        var step = Assert.Single(definition.Steps, step => string.Equals(step.Key, stepKey, StringComparison.OrdinalIgnoreCase));
        var assignment = Assert.Single(step.RoleAssignments, assignment =>
            string.Equals(assignment.ResponsibilityKind, "Responsible", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectedRoleKey, assignment.RoleKey);
        Assert.DoesNotContain(step.RoleAssignments, assignment =>
            string.Equals(assignment.RoleKey, "delivery-manager", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(assignment.ResponsibilityKind, "Responsible", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertReviewerRole(
        ProcessTemplateDefinitionDocument definition,
        string stepKey,
        string expectedRoleKey)
    {
        var step = Assert.Single(definition.Steps, step => string.Equals(step.Key, stepKey, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(step.RoleAssignments, assignment =>
            string.Equals(assignment.RoleKey, expectedRoleKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(assignment.ResponsibilityKind, "Reviewer", StringComparison.OrdinalIgnoreCase));
    }

    private static ProcessCapabilityScope CreateRequiredWorkspaceToolScope(string capabilityKey)
    {
        return new ProcessCapabilityScope
        {
            Directives =
            [
                new ProcessCapabilityScopeDirective
                {
                    Kind = ProcessCapabilityScopeDirectiveKind.Require,
                    Target = new ProcessCapabilityScopeTarget
                    {
                        Kind = ProcessCapabilityScopeTargetKind.CapabilityIdentity,
                        Value = CapabilityKind.Tool.ToString(),
                        SecondaryValue = capabilityKey
                    },
                    Reason = "Scoped process step requires this workspace tool."
                }
            ]
        };
    }

    private static ProcessTemplateDefinitionDocument CreateDefinition()
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = "dotnet-runtime-command-writeback",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "runtime-command-recorder",
                    RoleResourceKey = "delivery-manager",
                    DisplayName = "Runtime command recorder",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "Manager",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "record-runtime-commands",
                    StepKind = "Activity",
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "runtime-command-recorder",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true
                        }
                    ]
                }
            ]
        };
    }

    private static WorkflowDefinition CreateWorkflowDefinition(
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        WorkflowLifecycleStatus status)
    {
        var start = new WorkflowNode(
            new WorkflowNodeId("start"),
            WorkflowNodeKind.Start,
            "Start",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                    InputShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON input"),
                    ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON result")));
        return new WorkflowDefinition(
            workflowId,
            versionId,
            "Process workflow",
            "Workflow selected explicitly by a process assignment.",
            status,
            new WorkflowGraph(start.Id, [start], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            Now,
            Now);
    }

    private static ProcessTemplateDefinitionDocument CreateTechnicalSubprocessDefinition()
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = "software-delivery-parent",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "delivery-manager",
                    RoleResourceKey = "delivery-manager",
                    DisplayName = "Architecture subprocess manager",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "Manager",
                    IsRequired = true
                },
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "solution-architect",
                    RoleResourceKey = "solution-architect",
                    DisplayName = ".NET Architect",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "Architect",
                    IsRequired = true
                },
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "lead-engineer",
                    RoleResourceKey = "lead-engineer",
                    DisplayName = ".NET Developer",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "Developer",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "architecture-review",
                    Title = "Run .NET architecture design and review subprocess",
                    StepKind = "Subprocess",
                    AllowedOperations =
                    [
                        ProcessOperationContractNames.ReadProcessContext,
                        ProcessOperationContractNames.ReadProjectStructure,
                        ProcessOperationContractNames.ReadUpstreamArtifacts,
                        ProcessOperationContractNames.WriteManagedProcessArtifacts,
                        ProcessOperationContractNames.ExecuteExternalAction
                    ],
                    OperationTargetScope = ProcessOperationContractNames.ExternalActionControlled,
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "solution-architect",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true,
                            FallbackOrder = 0
                        },
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "delivery-manager",
                            ResponsibilityKind = "Reviewer",
                            IsRequired = true,
                            FallbackOrder = 0
                        }
                    ]
                },
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "implementation",
                    Title = "Run .NET implementation slice subprocess",
                    StepKind = "Subprocess",
                    AllowedOperations =
                    [
                        ProcessOperationContractNames.ReadProcessContext,
                        ProcessOperationContractNames.ReadProjectStructure,
                        ProcessOperationContractNames.ReadUpstreamArtifacts,
                        ProcessOperationContractNames.WriteManagedProcessArtifacts,
                        ProcessOperationContractNames.ExecuteExternalAction
                    ],
                    OperationTargetScope = ProcessOperationContractNames.ExternalActionControlled,
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "lead-engineer",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true,
                            FallbackOrder = 0
                        },
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "delivery-manager",
                            ResponsibilityKind = "Reviewer",
                            IsRequired = true,
                            FallbackOrder = 0
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessTemplateDefinitionDocument CreateBlazorImplementationDefinition()
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = "blazor-app-delivery",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "blazor-engineer",
                    RoleResourceKey = "lead-engineer",
                    DisplayName = "Blazor implementation engineer",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "Developer",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "implement-blazor-change",
                    Title = "Build Blazor application",
                    StepKind = "Activity",
                    AllowedOperations =
                    [
                        ProcessOperationContractNames.MutateProductTarget,
                        ProcessOperationContractNames.RunValidation,
                        ProcessOperationContractNames.LaunchRuntime,
                        ProcessOperationContractNames.CaptureRuntimeProof,
                        ProcessOperationContractNames.WriteManagedProcessArtifacts
                    ],
                    OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetMutable,
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "blazor-engineer",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessTemplateDefinitionDocument CreateDeliveryManagerArchitectureHandoffDefinition()
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = "architecture-delivery-handoff",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "delivery-manager",
                    RoleResourceKey = "delivery-manager",
                    DisplayName = "Delivery Manager",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "Manager",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "architecture-handoff",
                    Title = "Finalize .NET architecture handoff",
                    StepKind = "Activity",
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "delivery-manager",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessTemplateDefinitionDocument CreateSoftwareEngineerScaffoldDefinition()
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = "dotnet-solution-setup",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "software-engineer",
                    RoleResourceKey = "software-engineer",
                    DisplayName = "Generic .NET scaffold engineer",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "Developer",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "create-dotnet-project",
                    Title = "Create solution and .NET app project",
                    StepKind = "Activity",
                    AllowedOperations =
                    [
                        ProcessOperationContractNames.ReadProcessContext,
                        ProcessOperationContractNames.ReadProjectStructure,
                        ProcessOperationContractNames.ReadUpstreamArtifacts,
                        ProcessOperationContractNames.MutateProductTarget,
                        ProcessOperationContractNames.RunValidation,
                        ProcessOperationContractNames.WriteManagedProcessArtifacts
                    ],
                    OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetMutable,
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "software-engineer",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessTemplateDefinitionDocument CreateLocalizedActivityDefinition()
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = "obecny-activity-contract",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "vývojář-検証",
                    RoleResourceKey = "lead-engineer",
                    DisplayName = "Разработчик browser implementation",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "Developer",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "doplnit-実装",
                    Title = "Browser implementation repair",
                    StepKind = "Activity",
                    AllowedOperations =
                    [
                        ProcessOperationContractNames.ReadProcessContext,
                        ProcessOperationContractNames.ReadProjectStructure,
                        ProcessOperationContractNames.ReadUpstreamArtifacts,
                        ProcessOperationContractNames.WriteManagedProcessArtifacts
                    ],
                    OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetReadOnly,
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "vývojář-検証",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessTemplateDefinitionDocument CreateScreenshotSubprocessDefinition()
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = "software-delivery-parent",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "qa-lead",
                    RoleResourceKey = "qa-lead",
                    DisplayName = ".NET QA Review Lead",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "QA",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "capture-ui-screenshots",
                    Title = "Capture UI screenshots",
                    StepKind = "Subprocess",
                    AllowedOperations =
                    [
                        ProcessOperationContractNames.ReadProcessContext,
                        ProcessOperationContractNames.ReadProjectStructure,
                        ProcessOperationContractNames.ReadUpstreamArtifacts,
                        ProcessOperationContractNames.WriteManagedProcessArtifacts,
                        ProcessOperationContractNames.ExecuteExternalAction,
                        ProcessOperationContractNames.LaunchRuntime,
                        ProcessOperationContractNames.CaptureRuntimeProof
                    ],
                    OperationTargetScope = ProcessOperationContractNames.ExternalActionControlled,
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "qa-lead",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessTemplateDefinitionDocument CreateScreenshotStorageDefinition()
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = "dotnet-ui-screenshot-writeback",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "screenshot-review-storage-agent",
                    RoleResourceKey = "qa-lead",
                    DisplayName = "Screenshot review and storage agent",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "AiAgent",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "store-ui-screenshots",
                    Title = "Store screenshots under process run node",
                    StepKind = "Review",
                    AllowedOperations =
                    [
                        ProcessOperationContractNames.ReadProcessContext,
                        ProcessOperationContractNames.ReadProjectStructure,
                        ProcessOperationContractNames.ReadUpstreamArtifacts,
                        ProcessOperationContractNames.CaptureRuntimeProof,
                        ProcessOperationContractNames.WriteManagedProcessArtifacts,
                        ProcessOperationContractNames.ExecuteExternalAction
                    ],
                    OperationTargetScope = ProcessOperationContractNames.ExternalActionControlled,
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "screenshot-review-storage-agent",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessInstancePlan CreateTechnicalSubprocessPlan()
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(PlanId, PlanId, null, null, "processes.instance-plan.v1", Now, 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                "template/1",
                "template/1",
                [],
                [],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([Binding], [], [], []),
            [
                new StepInstancePlan(ProcessStepInstanceId.New(), ProcessStepDefinitionId.New(), "architecture-review", ProcessStepKind.Activity, true, false, Binding),
                new StepInstancePlan(ProcessStepInstanceId.New(), ProcessStepDefinitionId.New(), "implementation", ProcessStepKind.Activity, true, false, Binding)
            ],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:plan");
    }

    private static ProcessRuntimeStepAssignment CreateArchitectureReviewAssignment(AgentDefinition executor)
    {
        return new ProcessRuntimeStepAssignment(
            new ProcessRunId(Guid.Parse("7760d755-3051-4ebe-a859-eac27a4d73cb")),
            PlanId,
            StepId,
            "architecture-review",
            "solution-architect",
            "solution-architect",
            "Solution architect",
            ProcessLaunchExecutorKinds.Agent,
            executor.Id.ToString("D"),
            executor.Name,
            "Prepare architecture handoff.",
            "sha256:stale",
            $"Resolved active agent '{executor.Name}' before HR role repair.",
            [],
            [],
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.ExecuteExternalAction,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof
            ],
            ProcessOperationContractNames.ExternalActionControlled,
            new Dictionary<string, string>(),
            BranchGate: null,
            Now);
    }

    private static ProcessInstancePlan CreatePlan(string stepKey = "record-runtime-commands")
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(PlanId, PlanId, null, null, "processes.instance-plan.v1", Now, 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                "template/1",
                "template/1",
                [],
                [],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([Binding], [], [], []),
            [new StepInstancePlan(StepId, ProcessStepDefinitionId.New(), stepKey, ProcessStepKind.Activity, true, false, Binding)],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:plan");
    }

    private static ProcessInstancePlan CreatePlanForSteps(params string[] stepKeys)
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(PlanId, PlanId, null, null, "processes.instance-plan.v1", Now, 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                "template/1",
                "template/1",
                [],
                [],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([Binding], [], [], []),
            stepKeys
                .Select(stepKey => new StepInstancePlan(
                    ProcessStepInstanceId.New(),
                    ProcessStepDefinitionId.New(),
                    stepKey,
                    ProcessStepKind.Activity,
                    true,
                    false,
                    Binding))
                .ToArray(),
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:plan");
    }

    private static AgentDefinition CreateAgent(Guid providerId)
        => CreateAgent(
            providerId,
            Guid.Parse("e650d9a2-ea73-4e73-a0b1-1ac8f3d2a155"),
            "Delivery Manager",
            "Delivery Manager",
            "Coordinates delivery readiness and runtime command handoff.",
            AgentWorkloadKind.Management,
            AgentWorkspaceToolProfileKind.BusinessAnalysis,
            ["delivery-manager"]);

    private static AgentDefinition CreateAgent(
        Guid providerId,
        Guid agentId,
        string name,
        string roleTitle,
        string summary,
        AgentWorkloadKind workload,
        AgentWorkspaceToolProfileKind workspaceToolProfile,
        IReadOnlyList<string> tags,
        IReadOnlyList<AgentCapabilityAssignment>? capabilities = null)
    {
        return new AgentDefinition(
            Id: agentId,
            Name: name,
            RoleTitle: roleTitle,
            Summary: summary,
            Instructions: "Resolve delivery governance tasks.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: providerId,
            Model: string.Empty,
            Workload: workload,
            ChatHistoryMode: AgentChatHistoryMode.ProviderDefault,
            Temperature: 0d,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                AgentWorkspaceToolAccessProfiles.CreateSettings(workspaceToolProfile)),
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: capabilities ?? [],
            Tags: tags,
            CreatedAtUtc: Now,
            UpdatedAtUtc: Now);
    }

    private static IReadOnlyList<AgentCapabilityAssignment> CreateDotNetSetupToolCapabilities()
    {
        return
        [
            CreateToolCapability("workspace-create-directory"),
            CreateToolCapability("workspace-dotnet-new"),
            CreateToolCapability("workspace-write-file"),
            CreateToolCapability("workspace-stat-path"),
            CreateToolCapability("workspace-pwsh-run-script"),
            CreateToolCapability("workspace-read-file")
        ];
    }

    private static AgentCapabilityAssignment CreateToolCapability(string capabilityKey)
    {
        return new AgentCapabilityAssignment(
            Guid.NewGuid(),
            capabilityKey,
            CapabilityKind.Tool,
            CapabilityProofStatus.Verified,
            Now,
            "Capability is available in this unit-test fixture.");
    }

    private static ProviderProfile CreateProvider(
        Guid providerId,
        ProviderKind kind = ProviderKind.OpenAi,
        ProviderTransportKind transport = ProviderTransportKind.Responses,
        bool supportsTools = true)
    {
        var isOllama = kind == ProviderKind.Ollama;
        var effectiveTransport = isOllama ? ProviderTransportKind.ChatCompletions : transport;
        var defaultModel = isOllama ? "gptoss32k:latest" : "gpt-5-mini";
        IReadOnlyList<string> suggestedModels = isOllama ? ["gptoss32k:latest"] : ["gpt-5-mini"];

        return new ProviderProfile(
            Id: providerId,
            Name: isOllama ? "Remote Ollama" : "OpenAI default",
            Kind: kind,
            BaseUrl: isOllama ? "http://localhost:11434" : "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable: isOllama ? string.Empty : "OPENAI_API_KEY",
            DefaultModel: defaultModel,
            Transport: effectiveTransport,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: supportsTools,
            PreferFrameworkManagedChatHistory: isOllama || effectiveTransport != ProviderTransportKind.Responses,
            SupportsBackgroundResponses: !isOllama && effectiveTransport == ProviderTransportKind.Responses,
            ConfigurationJson: "{}",
            Notes: "Unit-test provider.",
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: suggestedModels);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private static ProcessTemplateDefinitionDocument LoadTemplateDefinition(string definitionKey)
    {
        var templateLoader = new ProcessTemplatePackLoader(Path.Combine(FindRepositoryRoot(), "Templates", "Processes"));
        return templateLoader.LoadDefinition(definitionKey);
    }

    private sealed class ResolverWorkspaceFactory : ICanDoItAllAgentWorkspaceFactory
    {
        public ResolverWorkspaceFactory(IAgentFrameworkWorkspaceService workspaceService)
        {
            WorkspaceService = workspaceService;
        }

        public IAgentFrameworkWorkspaceService WorkspaceService { get; }

        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => WorkspaceService;

        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope) => WorkspaceService;

        public WorkspaceScopeDescriptor GetOrganizationScope() => WorkspaceScopeDescriptor.Organization("unit-test");

        public string GetWorkspaceRoot() => Path.GetTempPath();
    }

    private sealed class ResolverWorkflowCatalog : IWorkflowCatalogService
    {
        public WorkflowDefinitionDetail? Exact { get; init; }

        public WorkflowDefinitionDetail? LatestActive { get; init; }

        public (WorkflowId WorkflowId, WorkflowVersionId VersionId)? ExactSelection { get; private set; }

        public WorkflowId? LatestActiveWorkflowId { get; private set; }

        public bool ListDefinitionsCalled { get; private set; }

        public Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
            CancellationToken cancellationToken = default)
        {
            ListDefinitionsCalled = true;
            return Task.FromResult<IReadOnlyList<WorkflowCatalogItem>>([]);
        }

        public Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
        {
            if (versionId is not { } exactVersionId)
            {
                throw new InvalidOperationException("Resolver workflow tests require an exact version for GetDefinitionAsync.");
            }

            ExactSelection = (workflowId, exactVersionId);
            return Task.FromResult(Exact);
        }

        public Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
            WorkflowId workflowId,
            WorkflowLifecycleStatus status,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(WorkflowLifecycleStatus.Active, status);
            LatestActiveWorkflowId = workflowId;
            return Task.FromResult(LatestActive);
        }

        public Task<WorkflowDefinition> SaveDefinitionAsync(
            WorkflowDefinitionSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
            WorkflowDefinitionStatusChangeRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
            WorkflowId workflowId,
            WorkflowVersionId? versionId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowDefinition> ImportDefinitionAsync(
            WorkflowDefinitionImportRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteDefinitionAsync(
            WorkflowId workflowId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowValidationResult> ValidateDefinitionAsync(
            WorkflowDefinition definition,
            CancellationToken cancellationToken = default)
            => Task.FromResult(WorkflowValidationResult.Success);
    }

    private sealed class ResolverWorkspaceService(
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyList<ProviderProfile> providers) : IAgentFrameworkWorkspaceService
    {
        public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(bool includeTemplates = true, CancellationToken cancellationToken = default)
            => Task.FromResult(agents);

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(providers);

        public Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentOverviewSnapshot> GetAgentOverviewAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentUsageDetailSnapshot> GetAgentUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderUsageDetailSnapshot> GetProviderUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ModelUsageDetailSnapshot> GetModelUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(Guid providerId, ProviderTestChatRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(Guid providerId, ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(bool includeTemplates = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(Guid agentId, Guid? preferredSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> RenameChatSessionAsync(Guid agentId, Guid chatSessionId, string title, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ExecuteRunAsync(ExecutionRunRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, AgentExecutionOperationId activityOperationId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            AgentChatRunOptions options,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? attachmentPaths = null)
            => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(Guid agentId, Guid chatSessionId, AgentExecutionOperationId activityOperationId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(ExecutionRunQuery query, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
            AgentExecutionReportQuery query,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        private static InvalidOperationException Unused()
            => new("This fake workspace method is not used by the resolver test.");
    }

    private sealed class NoOpAiTechnicalAgentBridge : IAiTechnicalAgentBridge
    {
        public Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(AiAgentProfileEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        private static InvalidOperationException Unused()
            => new("This fake bridge method is not used by the resolver test.");
    }
}
