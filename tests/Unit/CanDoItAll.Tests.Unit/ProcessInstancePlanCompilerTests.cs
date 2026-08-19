using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessInstancePlanCompilerTests
{
    private static readonly ProcessDefinitionId DefinitionId = new(new Guid("4f56eddf-1057-4c7d-9211-2f960d82a874"));
    private static readonly ProcessDefinitionVersionId DefinitionVersionId = new(new Guid("f04c8792-74ab-4e7c-b0a7-d3ed4d4c29fd"));
    private static readonly ProcessDefinitionId ChildDefinitionId = new(new Guid("33fe5739-f949-4ddd-9356-2ea38c719734"));
    private static readonly ProcessDefinitionVersionId ChildDefinitionVersionId = new(new Guid("7651bf65-0c65-4d31-8c63-7f78552fce97"));
    private static readonly ProcessStepDefinitionId StartStepId = new(new Guid("52b4af51-3fd6-4439-b2d8-d3a7a696e573"));
    private static readonly ProcessStepDefinitionId ActivityStepId = new(new Guid("5a238f2d-8cfc-41fb-84fd-27a79c85bdfb"));
    private static readonly ProcessStepDefinitionId BranchStepId = new(new Guid("247594fe-9794-4efa-a3d9-8a76654352ac"));
    private static readonly ProcessStepDefinitionId EndStepId = new(new Guid("91624443-04b2-469b-bcda-7ea2544dc2b4"));
    private static readonly ArtifactDefinitionId ArtifactDefinitionId = new(new Guid("55979011-12bf-4b45-8729-03858fb8f692"));
    private static readonly ArtifactSlotId ArtifactSlotId = new(new Guid("80d2be21-9d97-4772-a240-fcb8832e021f"));
    private static readonly ArtifactInstanceId ArtifactInstanceId = new(new Guid("9b886925-24c9-4448-8402-d7e86a2b15f3"));
    private static readonly TemplateComponentId TemplateComponentId = new(new Guid("f437268b-6e7d-478d-9718-ff945da763a4"));

    [Fact]
    public void Compile_creates_golden_immutable_plan()
    {
        var compiler = new ProcessInstancePlanCompiler();
        var request = NewRequest();

        var result = compiler.Compile(request);
        var secondResult = compiler.Compile(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Plan);
        Assert.NotNull(secondResult.Plan);
        var plan = result.Plan;
        var secondPlan = secondResult.Plan;
        Assert.StartsWith("sha256:", plan.PlanHash, StringComparison.Ordinal);
        Assert.Equal(plan.PlanHash, secondPlan.PlanHash);
        Assert.Equal(DefinitionId, plan.Definition.DefinitionId);
        Assert.Single(plan.Definition.TemplateComponents);
        Assert.Single(plan.DriverStack.Drivers);
        Assert.Equal(4, plan.Steps.Count);
        Assert.Equal(2, plan.Steps.Count(step => step.IsExecutable));
        Assert.Equal(2, plan.Strategies.ExecutionBindings.Count);
        Assert.NotNull(plan.Manager.ManagerStrategyBinding);
        Assert.Single(plan.ArtifactPlan.Slots);
        Assert.Single(plan.ArtifactPlan.InitialLedgerEntries);
        Assert.Equal(2, plan.Branches.Routes.Count);
        Assert.Single(plan.Budgets.LoopBudgets);
        Assert.True(plan.Monitoring.Enabled);
        Assert.Equal(new[] { "approval.architect", "approval.security" }, plan.Security.RequiredApprovalKeys);
    }

    [Fact]
    public void Compile_fails_when_executable_step_has_no_strategy()
    {
        var compiler = new ProcessInstancePlanCompiler();
        var request = NewRequest(definition: NewDefinition(includeActivityStrategy: false));

        var result = compiler.Compile(request);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Builder.StrategyMissing");
    }

    [Fact]
    public void Compile_fails_on_driver_capability_conflict()
    {
        var conflictTag = new CapabilityTag("capability.execution");
        var first = NewPackage("driver.first", ProcessDriverLayer.Framework, Tags(conflictTag), NewStrategies());
        var second = NewPackage("driver.second", ProcessDriverLayer.Scenario, Tags(conflictTag), []);
        var catalog = new ProcessDriverCatalog([first, second]);
        var request = NewRequest(
            catalog: catalog,
            capabilityRequest: new ProcessCapabilityRequest(
                Tags(conflictTag),
                NoTags(),
                Tags(conflictTag)));

        var result = new ProcessInstancePlanCompiler().Compile(request);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Builder.DriverConflict");
    }

    [Fact]
    public void Compile_fails_on_subprocess_cycle()
    {
        var childRequest = NewRequest();
        var request = NewRequest(subprocesses:
        [
            new SubprocessCompileRequest(
                DefinitionId,
                DefinitionVersionId,
                ActivityStepId,
                childRequest.Source,
                "sha256:parent-child",
                "sha256:child-parent",
                "sha256:cancel",
                "sha256:escalate")
        ]);

        var result = new ProcessInstancePlanCompiler().Compile(request);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Builder.SubprocessCycle");
    }

    [Fact]
    public void Compile_fails_when_subprocess_depth_exceeds_budget()
    {
        var childRequest = NewRequest(definition: NewDefinition(ChildDefinitionId, ChildDefinitionVersionId));
        var request = NewRequest(
            subprocesses:
            [
                new SubprocessCompileRequest(
                    DefinitionId,
                    DefinitionVersionId,
                    ActivityStepId,
                    childRequest.Source,
                    "sha256:parent-child",
                    "sha256:child-parent",
                    "sha256:cancel",
                    "sha256:escalate")
            ],
            maximumSubprocessDepth: 0);

        var result = new ProcessInstancePlanCompiler().Compile(request);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "Builder.SubprocessDepthExceeded");
    }

    [Fact]
    public void Compile_creates_recursive_subprocess_plan_reference()
    {
        var childRequest = NewRequest(definition: NewDefinition(ChildDefinitionId, ChildDefinitionVersionId));
        var request = NewRequest(subprocesses:
        [
            new SubprocessCompileRequest(
                DefinitionId,
                DefinitionVersionId,
                ActivityStepId,
                childRequest.Source,
                "sha256:parent-child",
                "sha256:child-parent",
                "sha256:cancel",
                "sha256:escalate")
        ]);

        var result = new ProcessInstancePlanCompiler().Compile(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Plan);
        var subprocess = Assert.Single(result.Plan.Subprocesses);
        Assert.Equal(1, subprocess.HierarchyDepth);
        Assert.StartsWith("sha256:", subprocess.ChildPlanHash, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_fails_for_backward_branch_without_loop_budget()
    {
        var definition = NewDefinition(includeBackwardBudget: false);
        var request = NewRequest(definition: definition);

        var result = new ProcessInstancePlanCompiler().Compile(request);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "BranchRoute.BackwardMissingBudget");
    }

    [Fact]
    public void Plan_hash_changes_when_security_policy_changes()
    {
        var compiler = new ProcessInstancePlanCompiler();
        var first = compiler.Compile(NewRequest(securityHash: "sha256:security-a"));
        var second = compiler.Compile(NewRequest(securityHash: "sha256:security-b"));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Plan);
        Assert.NotNull(second.Plan);
        Assert.NotEqual(first.Plan.PlanHash, second.Plan.PlanHash);
    }

    [Fact]
    public void Plan_hash_changes_when_runtime_tool_contract_changes()
    {
        var compiler = new ProcessInstancePlanCompiler();
        var first = compiler.Compile(NewRequest(
            definition: NewDefinition(activityRuntimeToolNames: ["workspace_python_run_file"])));
        var second = compiler.Compile(NewRequest(
            definition: NewDefinition(activityRuntimeToolNames: ["workspace_dotnet_build"])));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Plan);
        Assert.NotNull(second.Plan);
        Assert.Equal(
            ["workspace_python_run_file"],
            first.Plan.Steps.Single(step => step.StepDefinitionId == ActivityStepId).RequiredRuntimeToolNames);
        Assert.NotEqual(first.Plan.PlanHash, second.Plan.PlanHash);
    }

    [Fact]
    public void Compile_fails_when_active_strategy_host_capability_is_unavailable()
    {
        var strategies = NewStrategies()
            .Select(strategy => strategy.StrategyId == new StrategyId("strategy.execute")
                ? strategy with
                {
                    RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId>
                    {
                        ProcessHostCapabilityIds.PythonRuntime
                    }
                }
                : strategy)
            .ToArray();
        var request = NewRequest(
            catalog: NewCatalog(strategies),
            capabilityRequest: NewCapabilityRequest(new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [
                    new ProcessHostCapabilityFact(
                        ProcessHostCapabilityIds.PythonRuntime,
                        ProcessHostCapabilityAvailability.Unavailable,
                        ProcessHostCapabilityReason.DependencyMissing,
                        ProcessHostExecutionPort.None)
                ])));

        var result = new ProcessInstancePlanCompiler().Compile(request);

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "Builder.StrategyHostCapabilityMissing");
        Assert.Contains(ProcessHostCapabilityIds.PythonRuntime.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("linux", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_does_not_block_on_unselected_strategy_host_capability()
    {
        var unusedStrategy = new ProcessStrategyDescriptor(
            new StrategyId("strategy.unused-docker"),
            "1.0.0",
            ProcessStrategyKind.StepExecution,
            Tags("capability.execution"))
        {
            RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId>
            {
                ProcessHostCapabilityIds.Docker
            }
        };
        var request = NewRequest(catalog: NewCatalog([.. NewStrategies(), unusedStrategy]));

        var result = new ProcessInstancePlanCompiler().Compile(request);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "Builder.StrategyHostCapabilityMissing");
    }

    [Fact]
    public void Compile_records_and_hashes_only_bounded_host_capability_facts()
    {
        var strategies = NewStrategies()
            .Select(strategy => strategy.StrategyId == new StrategyId("strategy.execute")
                ? strategy with
                {
                    RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId>
                    {
                        ProcessHostCapabilityIds.PythonRuntime
                    }
                }
                : strategy)
            .ToArray();
        var managedHostFact = new ProcessHostCapabilityFact(
            ProcessHostCapabilityIds.PythonRuntime,
            ProcessHostCapabilityAvailability.Available,
            ProcessHostCapabilityReason.Ready,
            ProcessHostExecutionPort.ManagedProcessHost);
        var compiler = new ProcessInstancePlanCompiler();

        var managed = compiler.Compile(NewRequest(
            catalog: NewCatalog(strategies),
            capabilityRequest: NewCapabilityRequest(new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [managedHostFact]))));
        var alternateProfile = compiler.Compile(NewRequest(
            catalog: NewCatalog(strategies),
            capabilityRequest: NewCapabilityRequest(new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux-alternate"),
                [managedHostFact]))));

        Assert.True(managed.Succeeded);
        Assert.True(alternateProfile.Succeeded);
        Assert.NotNull(managed.Plan);
        Assert.NotNull(alternateProfile.Plan);
        var binding = Assert.Single(
            managed.Plan.Strategies.ExecutionBindings,
            binding => binding.StrategyId == new StrategyId("strategy.execute"));
        Assert.Equal(new ProcessHostProfileId("linux"), binding.HostProfileId);
        Assert.Equal([managedHostFact], binding.HostCapabilities);
        Assert.NotEqual(managed.Plan.PlanHash, alternateProfile.Plan.PlanHash);
        var serialized = System.Text.Json.JsonSerializer.Serialize(managed.Plan);
        Assert.DoesNotContain("/home/sentinel", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret:", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_rejects_structurally_invalid_host_capability_snapshot()
    {
        var duplicate = new ProcessHostCapabilityFact(
            ProcessHostCapabilityIds.PythonRuntime,
            ProcessHostCapabilityAvailability.Available,
            ProcessHostCapabilityReason.Ready,
            ProcessHostExecutionPort.ManagedProcessHost);

        var result = new ProcessInstancePlanCompiler().Compile(NewRequest(
            capabilityRequest: NewCapabilityRequest(new ProcessHostCapabilitySnapshot(
                new ProcessHostProfileId("linux"),
                [duplicate, duplicate]))));

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "Builder.HostCapabilitySnapshotInvalid");
    }

    [Fact]
    public void Compile_rejects_selected_driver_stack_with_more_than_32_host_capabilities()
    {
        var requiredCapabilities = NewHostCapabilities(33);
        var first = NewPackage(
            "driver.first",
            ProcessDriverLayer.Platform,
            Tags("capability.execution"),
            NewStrategies(),
            requiredCapabilities.Take(16).ToHashSet());
        var second = NewPackage(
            "driver.second",
            ProcessDriverLayer.Platform,
            Tags("capability.branch", "capability.manager"),
            NewStrategies(),
            requiredCapabilities.Skip(16).ToHashSet());
        var request = NewRequest(
            catalog: new ProcessDriverCatalog([first, second]),
            capabilityRequest: NewCapabilityRequest(NewAvailableHostSnapshot(
                requiredCapabilities.Take(ProcessHostCapabilitySnapshot.MaximumCapabilities).ToHashSet())));

        var result = new ProcessInstancePlanCompiler().Compile(request);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "Builder.DriverHostCapabilityLimitExceeded");
        Assert.Null(result.Plan);
    }

    [Fact]
    public void Compile_rejects_effective_step_with_more_than_32_strategy_and_declared_host_capabilities()
    {
        var requiredCapabilities = NewHostCapabilities(33);
        var strategyCapabilities = requiredCapabilities.Take(16).ToHashSet();
        var stepCapabilities = requiredCapabilities.Skip(16).ToArray();
        var strategies = NewStrategies()
            .Select(strategy => strategy.StrategyId == new StrategyId("strategy.execute")
                ? strategy with { RequiredHostCapabilities = strategyCapabilities }
                : strategy)
            .ToArray();
        var definition = NewDefinition();
        definition = definition with
        {
            Steps = definition.Steps.Select(step =>
                step.Id == ActivityStepId
                    ? step with
                    {
                        RequiredHostCapabilities = stepCapabilities
                            .Select(capability => capability.Value)
                            .ToArray()
                    }
                    : step).ToArray()
        };
        var request = NewRequest(
            definition: definition,
            catalog: NewCatalog(strategies),
            capabilityRequest: NewCapabilityRequest(NewAvailableHostSnapshot(
                requiredCapabilities.Take(ProcessHostCapabilitySnapshot.MaximumCapabilities).ToHashSet())));

        var result = new ProcessInstancePlanCompiler().Compile(request);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "Builder.StepHostCapabilityLimitExceeded");
        Assert.Null(result.Plan);
    }

    private static ProcessInstancePlanCompileRequest NewRequest(
        ProcessDefinitionKernel? definition = null,
        ProcessDriverCatalog? catalog = null,
        ProcessCapabilityRequest? capabilityRequest = null,
        IReadOnlyList<SubprocessCompileRequest>? subprocesses = null,
        string securityHash = "sha256:security",
        int maximumSubprocessDepth = 8)
    {
        var component = new ProcessTemplateComponentReference(
            TemplateComponentId,
            "component.role.author",
            "1.0.0",
            "sha256:component");

        return new ProcessInstancePlanCompileRequest(
            "template/1.0",
            "template/1.0",
            definition ?? NewDefinition(),
            "sha256:definition",
            catalog ?? NewCatalog(),
            capabilityRequest ?? new ProcessCapabilityRequest(
                Tags(
                    "capability.execution",
                    "capability.branch",
                    "capability.manager"),
                NoTags(),
                NoTags()),
            [component],
            [component],
            [],
            new HashSet<string>(StringComparer.Ordinal),
            [
                new ProcessArtifactReference(
                    ArtifactSlotId,
                    ArtifactInstanceId,
                    ProcessArtifactScope.Local,
                    "sha256:artifact")
            ],
            new ProcessManagerPlanRequest(
                new StrategyId("strategy.manager"),
                [],
                [],
                "sha256:manager-policy"),
            new ProcessMonitoringPlanRequest(true, "sha256:monitoring"),
            new ProcessSecurityPlanRequest(
                securityHash,
                ["approval.security", "approval.architect"]),
            subprocesses ?? [],
            null,
            maximumSubprocessDepth);
    }

    private static ProcessDefinitionKernel NewDefinition(
        ProcessDefinitionId? definitionId = null,
        ProcessDefinitionVersionId? versionId = null,
        StrategyId? activityStrategyId = null,
        bool includeActivityStrategy = true,
        bool includeBackwardBudget = true,
        IReadOnlyList<string>? activityRuntimeToolNames = null)
    {
        var resolvedActivityStrategyId = includeActivityStrategy
            ? activityStrategyId ?? new StrategyId("strategy.execute")
            : (StrategyId?)null;
        var branchStrategyId = new StrategyId("strategy.branch");
        return new ProcessDefinitionKernel(
            definitionId ?? DefinitionId,
            versionId ?? DefinitionVersionId,
            [
                new ProcessGraphNode(StartStepId, "start", ProcessStepKind.Start),
                new ProcessGraphNode(ActivityStepId, "activity", ProcessStepKind.Activity, resolvedActivityStrategyId)
                {
                    RequiredRuntimeToolNames = activityRuntimeToolNames ?? []
                },
                new ProcessGraphNode(BranchStepId, "branch", ProcessStepKind.Branch, branchStrategyId),
                new ProcessGraphNode(EndStepId, "end", ProcessStepKind.End)
            ],
            [
                new ProcessGraphEdge(StartStepId, ActivityStepId),
                new ProcessGraphEdge(ActivityStepId, BranchStepId),
                new ProcessGraphEdge(BranchStepId, EndStepId)
            ],
            [
                new ProcessArtifactDefinition(
                    ArtifactDefinitionId,
                    "artifact.brief",
                    ProcessArtifactSensitivity.Normal)
            ],
            [
                new ProcessArtifactSlotDefinition(
                    ArtifactSlotId,
                    "slot.brief",
                    ArtifactDefinitionId,
                    ProcessArtifactRequirementMode.Required,
                    ProcessArtifactScope.Local,
                    false)
            ],
            [
                new ProcessBranchDefinition(
                    BranchStepId,
                    new BranchFamilyId("branch.family.review"),
                    [],
                    NewBranchOutcomes(includeBackwardBudget))
            ]);
    }

    private static IReadOnlyList<BranchOutcomeDefinition> NewBranchOutcomes(bool includeBackwardBudget)
    {
        return
        [
            new BranchOutcomeDefinition(
                new BranchOutcomeId("outcome.complete"),
                "Complete",
                BranchOutcomeCategory.Complete,
                new ProcessRouteTarget(ProcessRouteTargetKind.CompleteRun)),
            new BranchOutcomeDefinition(
                new BranchOutcomeId("outcome.repeat"),
                "Repeat",
                BranchOutcomeCategory.Repeat,
                new ProcessRouteTarget(ProcessRouteTargetKind.PreviousStep),
                includeBackwardBudget
                    ? new LoopBudgetDefinition(
                        2,
                        new LoopFingerprintPolicyId("loop.review"),
                        new ProcessRouteTarget(ProcessRouteTargetKind.Escalate))
                    : null)
        ];
    }

    private static ProcessDriverCatalog NewCatalog(
        IReadOnlyList<ProcessStrategyDescriptor>? strategies = null)
    {
        return new ProcessDriverCatalog([
            NewPackage(
                "driver.generic",
                ProcessDriverLayer.Framework,
                Tags(
                    "capability.execution",
                    "capability.branch",
                    "capability.manager"),
                strategies ?? NewStrategies())
        ]);
    }

    private static ProcessCapabilityRequest NewCapabilityRequest(
        ProcessHostCapabilitySnapshot hostCapabilities)
    {
        return new ProcessCapabilityRequest(
            Tags(
                "capability.execution",
                "capability.branch",
                "capability.manager"),
            NoTags(),
            NoTags())
        {
            HostCapabilities = hostCapabilities
        };
    }

    private static IReadOnlyList<ProcessStrategyDescriptor> NewStrategies()
    {
        return
        [
            new ProcessStrategyDescriptor(
                new StrategyId("strategy.execute"),
                "1.0.0",
                ProcessStrategyKind.StepExecution,
                Tags("capability.execution")),
            new ProcessStrategyDescriptor(
                new StrategyId("strategy.branch"),
                "1.0.0",
                ProcessStrategyKind.BranchDecision,
                Tags("capability.branch")),
            new ProcessStrategyDescriptor(
                new StrategyId("strategy.manager"),
                "1.0.0",
                ProcessStrategyKind.ManagerDecision,
                Tags("capability.manager"))
        ];
    }

    private static ProcessDriverPackage NewPackage(
        string driverId,
        ProcessDriverLayer layer,
        IReadOnlySet<CapabilityTag> capabilities,
        IReadOnlyList<ProcessStrategyDescriptor> strategies,
        IReadOnlySet<ProcessHostCapabilityId>? requiredHostCapabilities = null)
    {
        var descriptor = new ProcessDriverDescriptor(
            new DriverId(driverId),
            driverId,
            "1.0.0",
            "runtime/1.0",
            "runtime/2.x",
            layer,
            capabilities,
            [],
            [],
            [],
            strategies)
        {
            RequiredHostCapabilities = requiredHostCapabilities ?? new HashSet<ProcessHostCapabilityId>()
        };

        return new ProcessDriverPackage(descriptor, [], [], [], [], [], []);
    }

    private static IReadOnlySet<CapabilityTag> Tags(params string[] values)
    {
        return values.Select(value => new CapabilityTag(value)).ToHashSet();
    }

    private static IReadOnlySet<CapabilityTag> Tags(params CapabilityTag[] values)
    {
        return values.ToHashSet();
    }

    private static IReadOnlySet<CapabilityTag> NoTags()
    {
        return new HashSet<CapabilityTag>();
    }

    private static IReadOnlySet<ProcessHostCapabilityId> NewHostCapabilities(int count)
    {
        return Enumerable.Range(0, count)
            .Select(index => new ProcessHostCapabilityId($"host.test.cap-{index:D2}"))
            .ToHashSet();
    }

    private static ProcessHostCapabilitySnapshot NewAvailableHostSnapshot(
        IReadOnlySet<ProcessHostCapabilityId> capabilities)
    {
        return new ProcessHostCapabilitySnapshot(
            new ProcessHostProfileId("test"),
            capabilities
                .Select(capability => new ProcessHostCapabilityFact(
                    capability,
                    ProcessHostCapabilityAvailability.Available,
                    ProcessHostCapabilityReason.Ready,
                    ProcessHostExecutionPort.ManagedProcessHost))
                .ToArray());
    }
}
