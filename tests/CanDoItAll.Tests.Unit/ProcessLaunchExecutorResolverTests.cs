using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

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
            workspaceFactory,
            new ProcessMockAgentCatalogService(
                workspaceFactory,
                new NoOpAiTechnicalAgentBridge(),
                Options.Create(new ProcessMockAgentOptions { Enabled = false })),
            new ProviderProfileService());

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
            ["dotnet", "programming", "blazor"]);
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
            new ResolverWorkspaceFactory(workspace),
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

    private static AgentFrameworkProcessLaunchExecutorResolver CreateResolver(ResolverWorkspaceFactory workspaceFactory)
    {
        return new AgentFrameworkProcessLaunchExecutorResolver(
            workspaceFactory,
            new ProcessMockAgentCatalogService(
                workspaceFactory,
                new NoOpAiTechnicalAgentBridge(),
                Options.Create(new ProcessMockAgentOptions { Enabled = false })),
            new ProviderProfileService());
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
                        ProcessOperationContractNames.ExecuteExternalAction,
                        ProcessOperationContractNames.MutateProductTarget,
                        ProcessOperationContractNames.RunValidation
                    ],
                    OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetMutable,
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

    private static ProviderProfile CreateProvider(Guid providerId)
    {
        return new ProviderProfile(
            Id: providerId,
            Name: "OpenAI default",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
            DefaultModel: "gpt-5-mini",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: "Unit-test provider.",
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-5-mini"]);
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

    private sealed class ResolverWorkspaceFactory(IAgentFrameworkWorkspaceService workspaceService) : ICanDoItAllAgentWorkspaceFactory
    {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => workspaceService;

        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope) => workspaceService;

        public WorkspaceScopeDescriptor GetOrganizationScope() => WorkspaceScopeDescriptor.Organization("unit-test");

        public string GetWorkspaceRoot() => Path.GetTempPath();
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

        public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(Guid providerId, OllamaModelfileRequest request, CancellationToken cancellationToken = default) => throw Unused();

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

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(Guid agentId, Guid? chatSessionId, string prompt, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(Guid agentId, Guid chatSessionId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(ExecutionRunQuery query, CancellationToken cancellationToken = default) => throw Unused();

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
