using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class Maf122WorkflowProcessIntegrationTests(ITestOutputHelper output) {
    private const string DefinitionKey = "maf122-workflow-outcome";
    private const string StepKey = "workflow-outcome";
    private const string RoleKey = "workflow-owner";

    public enum Scenario { Complete, Fail, WaitThenCancel }

    [Theory]
    [InlineData(Scenario.Complete)]
    [InlineData(Scenario.Fail)]
    [InlineData(Scenario.WaitThenCancel)]
    public async Task Real_workflow_child_maps_terminal_state_and_reuses_persisted_identity(Scenario scenario) {
        await using var environment = CanDoItAllTestEnvironment.Create("maf122-workflow-process");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var packRoot = WritePack(environment.RootPath);
        await using var app = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment,
            ActiveProfile = profile,
            ConfigureServices = services => services.Replace(ServiceDescriptor.Singleton(new ProcessTemplatePackLoader(packRoot)))
        });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workflow = await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(
            Id: null, ExpectedVersionId: null, "MAF process outcome", "Real built-in executor acceptance.",
            WorkflowLifecycleStatus.Active, CreateGraph(scenario), new(
                WorkflowRuntimeBackendKind.InProcess, AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false, ExposeAzureFunctionsStatusEndpoint: false, ExposeAzureFunctionsMcpTool: false)));
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId,
            new() { Name = "MAF workflow process acceptance" })).IsSuccess);
        var authority = await services.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(projectId, ProcessLaunchOperatorSurface.UserInterface);
        var launched = await services.GetRequiredService<ProcessLaunchApplicationService>().LaunchAsync(new(
            DefinitionKey, null, null, projectId, null, "maf122-acceptance", new Dictionary<string, string>(), true, false) {
            Authority = authority, ProjectAdmission = authority.ProjectAdmission, CallerIntentId = new(Guid.NewGuid()),
            ExecutorOverrides = [new(StepKey, RoleKey, ProcessLaunchExecutorKinds.Workflow,
                workflow.Id.Value.ToString("D"), workflow.Name, "Real saved workflow") {
                WorkflowBinding = new(new(workflow.Id.Value), new(workflow.VersionId.Value))
            }]
        });
        Assert.NotNull(launched.RunId);
        var processRun = launched.RunId.Value;
        var originalAssignment = await services.GetRequiredService<IProcessRuntimeStepAssignmentStore>()
            .LoadAsync(processRun, Assert.Single(launched.LaunchPlan.Steps).StepInstanceId);
        Assert.NotNull(originalAssignment);
        async Task DispatchAsync(IServiceProvider requestServices) {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try {
                var dispatched = await requestServices.GetRequiredService<ProcessRuntimeDispatchApplicationService>()
                    .ExecuteReadyAsync(processRun, "maf122-acceptance", deadline.Token);
                output.WriteLine($"Dispatch status={dispatched.Status}; {string.Join("; ", dispatched.Diagnostics)}");
            } catch {
                await using var readScope = app.Services.CreateAsyncScope();
                var retained = await readScope.ServiceProvider.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(processRun);
                output.WriteLine($"Interrupted dispatch: process={retained?.Status}, steps={string.Join(',', retained?.Steps.Select(step => step.Status) ?? [])}, receipts={retained?.AppliedResults.Count}");
                throw;
            }
        }
        await DispatchAsync(services);
        var runtime = services.GetRequiredService<IWorkflowRuntimeManager>();
        var child = Assert.Single(await runtime.ListRunsAsync(workflow.Id));
        var origin = Assert.IsType<WorkflowLaunchOrigin.ProcessDispatchAssignment>(child.Origin);
        Assert.Equal(processRun.Value, origin.Dispatch.ProcessRun.Value);
        Assert.Equal(Assert.Single(launched.LaunchPlan.Steps).StepInstanceId.Value, origin.Dispatch.Assignment.Value);
        if (scenario == Scenario.WaitThenCancel) {
            Assert.Equal(WorkflowRunState.WaitingForInput, child.State);
            await using (var resumedScope = app.Services.CreateAsyncScope()) {
                await DispatchAsync(resumedScope.ServiceProvider);
            }
            Assert.Equal(child.RunId, Assert.Single(await runtime.ListRunsAsync(workflow.Id)).RunId);
            var cancelled = await runtime.RequestCancellationAsync(child.RunId);
            Assert.Equal(WorkflowRunState.Cancelled, cancelled.Run!.State);
            await using var observationScope = app.Services.CreateAsyncScope();
            var resume = await observationScope.ServiceProvider.GetRequiredService<ProcessRuntimeOperatorApplicationService>()
                .ExecuteAsync(new(processRun, Assert.Single(launched.LaunchPlan.Steps).StepInstanceId,
                    ProcessRuntimeOperatorActionKind.RequestRework, "maf122-acceptance",
                    "Observe the existing cancelled workflow child without launching another child."));
            Assert.True(resume.Succeeded, string.Join("; ", resume.Diagnostics));
            var resumedAssignment = await observationScope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>()
                .LoadAsync(processRun, Assert.Single(launched.LaunchPlan.Steps).StepInstanceId);
            Assert.NotNull(resumedAssignment);
            Assert.Equal(ProcessLaunchExecutorKinds.Workflow, resumedAssignment.ExecutorKind);
            Assert.Equal(workflow.Id.Value, resumedAssignment.WorkflowBinding!.WorkflowId.Value);
            Assert.Equal(originalAssignment.Prompt, resumedAssignment.Prompt);
            Assert.Equal(originalAssignment.WorkflowBinding, resumedAssignment.WorkflowBinding);
            await DispatchAsync(observationScope.ServiceProvider);
        }
        var state = await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(processRun);
        Assert.NotNull(state);
        child = Assert.Single(await runtime.ListRunsAsync(workflow.Id));
        var expectedWorkflowState = scenario switch {
            Scenario.Complete => WorkflowRunState.Completed,
            Scenario.Fail => WorkflowRunState.Failed,
            Scenario.WaitThenCancel => WorkflowRunState.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        Assert.Equal(expectedWorkflowState, child.State);
        output.WriteLine($"Process={processRun} Child={child.RunId} WorkflowState={child.State} ProcessState={state.Status}");
        var receipt = Assert.Single(state.AppliedResults);
        if (scenario == Scenario.Complete) {
            Assert.Equal(ProcessRuntimeStatus.Completed, state.Status);
            Assert.Equal("The built-in JSON transform produced the declared outcome.", receipt.UserSafeSummary);
            var events = await runtime.ListEventsAsync(child.RunId);
            Assert.Contains(events, item => item.Kind == WorkflowEventKind.ExecutorCompleted);
        } else {
            Assert.Equal(scenario == Scenario.Fail ? ProcessRuntimeStatus.Failed : ProcessRuntimeStatus.Cancelled, state.Status);
            Assert.Contains(receipt.Diagnostics, item => item.Code == (scenario == Scenario.Fail
                ? "process.adapter.workflow_child_failed" : "process.adapter.workflow_child_cancelled"));
        }
        await DispatchAsync(services);
        Assert.Equal(child.RunId, Assert.Single(await runtime.ListRunsAsync(workflow.Id)).RunId);
    }

    private static WorkflowGraph CreateGraph(Scenario scenario) {
        var descriptor = BuiltInWorkflowExecutorDescriptors.JsonTransform;
        var result = JsonSerializer.Serialize(new ProcessStepOutcomeResult {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The built-in JSON transform produced the declared outcome.",
            EvidenceRefs = ["workflow-executor:outcome"],
            HumanReadableSummaryMarkdown = "Workflow outcome produced."
        }, AgentOutputJson.SerializerOptions);
        var settings = new WorkflowJsonTransformExecutorSettings {
            Operations = [scenario == Scenario.Fail
                ? new() { Operation = WorkflowJsonTransformOperation.Select, Path = "$.missingAcceptanceInput" }
                : new() { Operation = WorkflowJsonTransformOperation.Set, DestinationPath = "$", ValueJson = result }]
        };
        var middle = scenario == Scenario.WaitThenCancel
            ? Node("outcome", WorkflowNodeKind.HumanInput) with {
                Settings = new(null, null, null, WorkflowExternalRequestKind.HumanInput,
                    "Wait for the acceptance decision.", descriptor.InputShape, descriptor.ResultShape)
            }
            : Node("outcome", WorkflowNodeKind.Executor) with {
                Settings = new(null, null, null, null, string.Empty, descriptor.InputShape, descriptor.ResultShape) {
                    ExecutorId = descriptor.Id,
                    ExecutorSettingsJson = JsonSerializer.Serialize(settings, AgentOutputJson.SerializerOptions),
                    ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
                }
            };
        return new(new("start"), [Node("start", WorkflowNodeKind.Start), middle, Node("end", WorkflowNodeKind.End)],
            [Edge("start", "outcome"), Edge("outcome", "end")]);
    }

    private static WorkflowNode Node(string id, WorkflowNodeKind kind) => new(new(id), kind, id, [],
        new(null, null, null, null, string.Empty,
            kind == WorkflowNodeKind.End ? BuiltInWorkflowExecutorDescriptors.JsonTransform.ResultShape : WorkflowValueShape.Text,
            kind == WorkflowNodeKind.Start ? WorkflowValueShape.Text : BuiltInWorkflowExecutorDescriptors.JsonTransform.ResultShape));

    private static WorkflowEdge Edge(string source, string target) => new(new(source + "-" + target), new(source), null,
        new(target), null, WorkflowEdgeKind.Direct, string.Empty) { Routing = WorkflowEdgeRouting.Always };

    private static string WritePack(string root) {
        var packRoot = Path.Combine(root, "workflow-process-pack");
        var definitionRoot = Path.Combine(packRoot, DefinitionKey);
        Directory.CreateDirectory(definitionRoot);
        var definition = new ProcessTemplateDefinitionDocument {
            Key = DefinitionKey, DisplayName = "Workflow outcome", Summary = "Map one real workflow outcome.",
            Criticality = "Low", OperatingMode = "GovernedLive", AutonomyLevel = "Guarded",
            RoleUsages = [new() { Key = RoleKey, RoleResourceKey = RoleKey, DisplayName = "Workflow owner",
                PreferredExecutorKind = ProcessLaunchExecutorKinds.Workflow, IsRequired = true }],
            Steps = [new() { Key = StepKey, Title = "Workflow outcome", StepKind = "Start",
                Notes = "Return a typed outcome from the configured workflow.",
                AllowedOperations = [ProcessOperationContractNames.ReadProcessContext],
                OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetReadOnly,
                RoleAssignments = [new() { RoleKey = RoleKey, ResponsibilityKind = "Responsible", IsRequired = true }]
            }]
        };
        var manifest = new ProcessTemplatePackManifest {
            PackKey = DefinitionKey, Name = "MAF workflow acceptance", Version = "1.0.0", GeneratedAtUtc = DateTimeOffset.UtcNow,
            Processes = [new() { Key = DefinitionKey, RelativePath = DefinitionKey }]
        };
        File.WriteAllText(Path.Combine(packRoot, "manifest.json"), JsonSerializer.Serialize(manifest));
        File.WriteAllText(Path.Combine(definitionRoot, "definition.json"), JsonSerializer.Serialize(definition));
        return packRoot;
    }
}
