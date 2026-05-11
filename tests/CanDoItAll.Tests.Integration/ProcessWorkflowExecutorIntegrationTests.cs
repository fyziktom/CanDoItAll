using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessWorkflowExecutorIntegrationTests
{
    [Fact]
    public async Task CreateLaunchPlanAsync_includes_workflow_candidate_for_workflow_role()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var components = scope.ServiceProvider.GetRequiredService<IWorkflowComponentLibraryService>();
        var processes = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var workflow = await SaveLlmWorkflowAsync(workflows, components, "Launch workflow");
        var definitionId = await SaveAndPublishProcessAsync(
            processes,
            BuildWorkflowProcessDefinition("Workflow launch proof", workflow));

        var launchResult = await processes.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = definitionId,
            LaunchName = "Workflow launch plan",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate workflow launch candidates.",
            RequestedBy = "integration-tests"
        });

        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));
        var details = await processes.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);
        var role = Assert.Single(details!.Roles);
        var candidate = Assert.Single(role.Candidates);

        Assert.Equal(ProcessLaunchCandidateKind.Workflow, candidate.CandidateKind);
        Assert.Equal(workflow.Id.Value, candidate.WorkflowDefinitionId);
        Assert.Equal(workflow.VersionId.Value, candidate.WorkflowVersionId);
        Assert.Equal(ProcessExecutorKindNames.Workflow, candidate.ExecutorKind);
        Assert.False(candidate.AllowsDirectMessaging);
        Assert.True(candidate.IsRecommended);
        Assert.True(role.IsResolved);
    }

    [Fact]
    public async Task DispatchAsync_runs_workflow_assignment_and_projects_process_link()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var components = scope.ServiceProvider.GetRequiredService<IWorkflowComponentLibraryService>();
        var processes = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dispatch = scope.ServiceProvider.GetRequiredService<IProcessRunAutomationDispatchService>();

        var workflow = await SaveLlmWorkflowAsync(workflows, components, "Completion workflow");
        var definitionId = await SaveAndPublishProcessAsync(
            processes,
            BuildWorkflowProcessDefinition("Workflow completion proof", workflow));
        var runId = await StartProcessRunAsync(processes, definitionId, "Workflow completion run");
        var step = Assert.Single(await processes.ListStepRunsAsync(runId));

        await dispatch.DispatchAsync(runId, step.Id, "integration-workflow-dispatch");

        var details = await processes.GetRunDetailsAsync(runId);
        var completedStep = Assert.Single(details.StepRuns);
        var assignment = Assert.Single(details.Assignments);
        Assert.True(
            details.WorkflowRuns.Count == 1,
            $"Expected one workflow run but found {details.WorkflowRuns.Count}. Step status: {completedStep.Status}; blocked: {completedStep.BlockedReason}; exception: {completedStep.ExceptionSummary}; assignment: {assignment.ExecutorKind}, workflow {assignment.WorkflowDefinitionId:D}/{assignment.WorkflowVersionId:D}; artifacts: {details.Artifacts.Count}.");
        var workflowRun = Assert.Single(details.WorkflowRuns);

        Assert.Equal(ProcessStepRunStatus.Completed, completedStep.Status);
        Assert.Equal(ProcessExecutorKindNames.Workflow, assignment.ExecutorKind);
        Assert.Equal(workflow.Id.Value, assignment.WorkflowDefinitionId);
        Assert.Equal(workflow.VersionId.Value, assignment.WorkflowVersionId);
        Assert.Equal(WorkflowRunState.Completed, workflowRun.State);
        Assert.Equal(workflow.Id.Value, workflowRun.WorkflowDefinitionId);
        Assert.Contains(details.Artifacts, artifact =>
            artifact.ExternalReferenceKey.StartsWith($"workflow-run:{workflowRun.WorkflowRunId:D}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DispatchAsync_maps_human_input_workflow_to_waiting_approval()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var processes = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dispatch = scope.ServiceProvider.GetRequiredService<IProcessRunAutomationDispatchService>();

        var workflow = await SaveHumanInputWorkflowAsync(workflows, "Human input workflow");
        var definitionId = await SaveAndPublishProcessAsync(
            processes,
            BuildWorkflowProcessDefinition("Workflow human input proof", workflow));
        var runId = await StartProcessRunAsync(processes, definitionId, "Workflow human input run");
        var step = Assert.Single(await processes.ListStepRunsAsync(runId));

        await dispatch.DispatchAsync(runId, step.Id, "integration-workflow-human-input");

        var details = await processes.GetRunDetailsAsync(runId);
        var waitingStep = Assert.Single(details.StepRuns);
        Assert.True(
            details.WorkflowRuns.Count == 1,
            $"Expected one workflow run but found {details.WorkflowRuns.Count}. Step status: {waitingStep.Status}; blocked: {waitingStep.BlockedReason}; exception: {waitingStep.ExceptionSummary}; artifacts: {details.Artifacts.Count}.");
        var workflowRun = Assert.Single(details.WorkflowRuns);

        Assert.Equal(ProcessStepRunStatus.WaitingApproval, waitingStep.Status);
        Assert.Equal(WorkflowRunState.WaitingForInput, workflowRun.State);
        Assert.Equal(1, workflowRun.PendingRequestCount);
    }

    [Fact]
    public async Task Process_run_detail_api_includes_workflow_run_links()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        Guid runId;
        WorkflowDefinition workflow;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
            var components = scope.ServiceProvider.GetRequiredService<IWorkflowComponentLibraryService>();
            var processes = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var dispatch = scope.ServiceProvider.GetRequiredService<IProcessRunAutomationDispatchService>();

            workflow = await SaveLlmWorkflowAsync(workflows, components, "API process detail workflow");
            var definitionId = await SaveAndPublishProcessAsync(
                processes,
                BuildWorkflowProcessDefinition("API process detail workflow proof", workflow));
            runId = await StartProcessRunAsync(processes, definitionId, "API process detail workflow run");
            var step = Assert.Single(await processes.ListStepRunsAsync(runId));
            await dispatch.DispatchAsync(runId, step.Id, "api-process-workflow-detail");
        }

        var response = await host.Client.GetAsync(
            $"/api/processes/runs/{runId:D}?includeExecutionRuns=false&includeDirectMessages=false");
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);

        using var payload = JsonDocument.Parse(responseBody);
        var workflowRun = Assert.Single(payload.RootElement.GetProperty("workflowRuns").EnumerateArray().ToList());

        Assert.Equal(workflow.Id.Value, workflowRun.GetProperty("workflowDefinitionId").GetGuid());
        Assert.Equal(workflow.VersionId.Value, workflowRun.GetProperty("workflowVersionId").GetGuid());
        Assert.Equal((int)WorkflowRunState.Completed, workflowRun.GetProperty("state").GetInt32());
        Assert.True(workflowRun.GetProperty("artifactCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task Process_assignment_resolution_api_preserves_workflow_target_ids()
    {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        Guid runId;
        Guid roleId;
        Guid stepDefinitionId;
        WorkflowDefinition workflow;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
            var components = scope.ServiceProvider.GetRequiredService<IWorkflowComponentLibraryService>();
            var processService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

            workflow = await SaveLlmWorkflowAsync(workflows, components, "API workflow assignment");
            var editor = BuildWorkflowProcessDefinition("API workflow assignment proof", workflow);
            roleId = editor.Roles.Single().Id.GetValueOrDefault();
            stepDefinitionId = editor.Steps.Single().Id.GetValueOrDefault();
            var definitionId = await SaveAndPublishProcessAsync(processService, editor);
            runId = await StartProcessRunAsync(processService, definitionId, "API workflow assignment run");
        }

        var response = await host.Client.PostAsJsonAsync(
            $"/api/processes/runs/{runId:D}/assignments/resolve",
            new
            {
                roleRequirementId = roleId,
                stepDefinitionId,
                displayName = "API workflow executor",
                executorKind = ProcessExecutorKindNames.Workflow,
                workflowDefinitionId = workflow.Id.Value,
                workflowVersionId = workflow.VersionId.Value,
                bindingReason = "Bound through scoped process assignment API.",
                isFallback = false,
                allowsDirectMessaging = true
            });
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);

        await using var verificationScope = host.App.Services.CreateAsyncScope();
        var verificationProcessService = verificationScope.ServiceProvider.GetRequiredService<ProcessesService>();
        var assignment = Assert.Single(
            await verificationProcessService.ListAssignmentsAsync(runId),
            item => item.RoleRequirementId == roleId && item.StepDefinitionId == stepDefinitionId);

        Assert.Equal(ProcessExecutorKindNames.Workflow, assignment.ExecutorKind);
        Assert.Equal(workflow.Id.Value, assignment.WorkflowDefinitionId);
        Assert.Equal(workflow.VersionId.Value, assignment.WorkflowVersionId);
        Assert.False(assignment.AllowsDirectMessaging);
    }

    private static async Task<Guid> SaveAndPublishProcessAsync(
        ProcessesService processes,
        ProcessDefinitionEditorModel editor)
    {
        var saveResult = await processes.SaveAsync(editor);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processes.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));
        return saveResult.Value;
    }

    private static async Task<Guid> StartProcessRunAsync(
        ProcessesService processes,
        Guid definitionId,
        string runName)
    {
        var runResult = await processes.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = definitionId,
            RunName = runName,
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate workflow role execution."
        });

        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));
        return runResult.Value;
    }

    private static ProcessDefinitionEditorModel BuildWorkflowProcessDefinition(
        string name,
        WorkflowDefinition workflow)
    {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        return new ProcessDefinitionEditorModel
        {
            Name = name,
            Summary = "Process workflow executor integration proof.",
            ValueStatement = "Workflow roles can be selected and dispatched from process runs.",
            CustomerName = "Integration tests",
            OwnerName = "Integration tests",
            GovernancePolicySummary = "Processes remain the outer orchestrator.",
            ChangeSummary = "Workflow executor validation.",
            ConstitutionRuleSummary = "Workflow execution remains observable from the process context.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Deterministic integration proof.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "workflow-role",
                    DisplayName = "Workflow role",
                    Purpose = "Run the configured MAF workflow.",
                    StaffingIntent = "Use workflow execution instead of an AI agent for this role.",
                    PreferredExecutorKind = ProcessExecutorKindNames.Workflow,
                    PreferredWorkflowDefinitionId = workflow.Id.Value,
                    PreferredWorkflowVersionId = workflow.VersionId.Value,
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = stepId,
                    Key = "workflow-step",
                    Title = "Run workflow step",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Use the process work brief as workflow input.",
                    OutputContractSummary = "Workflow result is linked back to the process run.",
                    EvidenceContractSummary = "Workflow run link and artifacts are process evidence.",
                    DecisionRightsSummary = "Workflow runtime owns node execution; process owns orchestration.",
                    ExceptionPolicySummary = "Workflow failure fails the process step.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static async Task<WorkflowDefinition> SaveLlmWorkflowAsync(
        IWorkflowCatalogService workflows,
        IWorkflowComponentLibraryService components,
        string name)
    {
        var component = await components.SaveComponentAsync(new LlmCallComponentSaveRequest(
            Id: null,
            Name: $"{name} component",
            ProviderProfileId: null,
            Model: "gpt-5.4",
            Modality: WorkflowModality.Text,
            ModelSettings: new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            Instructions: "Return a short deterministic summary of the input.",
            InputShape: WorkflowValueShape.Text,
            ResultShape: WorkflowValueShape.Text,
            Permissions: AgentPermissionsPolicy.Default));

        return await workflows.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: name,
            Description: "Active workflow used by process executor integration tests.",
            Status: WorkflowLifecycleStatus.Active,
            Graph: CreateLlmGraph(component.Id),
            RuntimePolicy: InProcessRuntimePolicy()));
    }

    private static async Task<WorkflowDefinition> SaveHumanInputWorkflowAsync(
        IWorkflowCatalogService workflows,
        string name)
    {
        return await workflows.SaveDefinitionAsync(new WorkflowDefinitionSaveRequest(
            Id: null,
            ExpectedVersionId: null,
            Name: name,
            Description: "Active human-in-loop workflow used by process executor integration tests.",
            Status: WorkflowLifecycleStatus.Active,
            Graph: CreateHumanInputGraph(),
            RuntimePolicy: InProcessRuntimePolicy()));
    }

    private static WorkflowRuntimePolicy InProcessRuntimePolicy()
    {
        return new WorkflowRuntimePolicy(
            WorkflowRuntimeBackendKind.InProcess,
            AllowInProcessPreviewRuns: true,
            RequireDurableProductionRuns: false,
            ExposeAzureFunctionsStatusEndpoint: false,
            ExposeAzureFunctionsMcpTool: false);
    }

    private static WorkflowGraph CreateLlmGraph(WorkflowComponentId componentId)
    {
        return new WorkflowGraph(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                CreateNode("logic", WorkflowNodeKind.StrictLogic, inputShape: WorkflowValueShape.Text, resultShape: WorkflowValueShape.Text),
                CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
            ],
            [
                CreateEdge("start-to-logic", "start", "logic"),
                CreateEdge("logic-to-end", "logic", "end")
            ]);
    }

    private static WorkflowGraph CreateHumanInputGraph()
    {
        return new WorkflowGraph(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                CreateNode(
                    "human-input",
                    WorkflowNodeKind.HumanInput,
                    inputShape: WorkflowValueShape.Text,
                    resultShape: WorkflowValueShape.Text,
                    externalRequestKind: WorkflowExternalRequestKind.HumanInput),
                CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
            ],
            [
                CreateEdge("start-to-human", "start", "human-input"),
                CreateEdge("human-to-end", "human-input", "end")
            ]);
    }

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null,
        WorkflowExternalRequestKind? externalRequestKind = null)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                externalRequestKind,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));
    }

    private static WorkflowEdge CreateEdge(string id, string source, string target)
    {
        return new WorkflowEdge(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);
    }
}
