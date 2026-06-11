using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests {
    private const string Sb02TemplateName = "Business plan development";
    private const string ProjectStructureProcessDefinitionNodePrefix = "process-definition:";
    private const string ProjectStructureAssignmentAskHrTestId = "project-structure-process-assignment-ask-hr";
    private const string ProjectStructureAssignmentConfirmHrTestId = "project-structure-process-assignment-confirm-hr";
    private const string ProjectStructureAssignmentDialogTestId = "project-structure-process-assignment-dialog";
    private const string ProjectStructureAssignmentReviewStartTestId = "project-structure-process-assignment-review-start";
    private const string ProjectStructureAssignmentRoleCardsTestId = "project-structure-process-assignment-role-cards";

    [Fact]
    [Trait("Surface", "ProjectStructure")]
    public async Task Project_structure_process_template_launch_SB02_INV_001_launches_approved_template_from_structure_context_and_reads_back_run() {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "process-template-ui-live-e2e-runtime-readiness-sb02");
        ResetDirectory(artifactsDir);

        using var agentClient = CreateProjectStructureAgentApiClient(
            fixture.BaseUrl,
            "sb02-playwright-agent",
            "SB02 Playwright",
            "tests/process-template-ui-live-e2e-runtime-readiness-sb02");
        using var processClient = CreateProcessApiClient(fixture.BaseUrl);
        await using var runtimeServiceProvider = BuildSb02RuntimeServiceProvider();
        await EnsureSb02ProcessMockCatalogAsync(runtimeServiceProvider);

        var suffix = Guid.NewGuid().ToString("N");
        var project = await PostJsonAndReadApiAsync<ProjectSummary>(
            agentClient,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                $"SB02 template launch project {suffix}",
                "Browser project-structure template launch proof",
                "Verify a published project template can start from a project-structure node and open the durable run.",
                "Execution",
                ProjectStatus.Active));
        var lease = await PostJsonAndReadApiAsync<ProjectStructureLeaseSnapshot>(
            agentClient,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString("D"),
                "SB02 browser template launch proof",
                15));
        var workNode = await PostJsonAndReadApiAsync<ProjectStructureNodeSummary>(
            agentClient,
            $"/api/project-structure/projects/{project.Id:D}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.WorkItem,
                $"SB02 launch target {suffix}",
                "Process execution target",
                "Selected work item used for browser-visible template launch proof.",
                $"project:{project.Id:D}",
                360,
                260,
                null,
                null,
                "task",
                null,
                null,
                lease.LeaseToken));

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{project.Id:D}/processes");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected project process route to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
        await page.GetByTestId("processes-workspace-shell").WaitForAsync();
        await page.GetByTestId("processes-templates-button").ClickAsync();

        var templateDialog = page.GetByTestId("processes-template-library-dialog");
        await templateDialog.WaitForAsync();
        await templateDialog.GetByPlaceholder("Search templates, roles, artifacts, governance, or evidence")
            .FillAsync(Sb02TemplateName);
        await page.GetByTestId("processes-template-library-item-business-plan-development").WaitForAsync();
        await page.GetByTestId("processes-template-library-item-business-plan-development").ClickAsync();
        await templateDialog.GetByRole(AriaRole.Heading, new() {
            Name = Sb02TemplateName,
            Exact = true
        }).WaitForAsync();
        await WaitForBodyTextAsync(page, project.Name, 30_000);
        await templateDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "01-project-template-selected-large-desktop.png")
        });

        await page.GetByTestId("processes-template-library-add-button").ClickAsync();
        var definition = await WaitForProjectDefinitionAsync(
            processClient,
            project.Id,
            Sb02TemplateName,
            candidate => candidate.StepCount > 0,
            30_000);
        Assert.Equal(project.Id, definition.ProjectId);

        await templateDialog.GetByRole(AriaRole.Button, new() {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await templateDialog.WaitForAsync(new() {
            State = WaitForSelectorState.Detached
        });

        await PostApiAsync(processClient, $"/api/processes/definitions/{definition.Id:D}/publish");
        definition = await WaitForProjectDefinitionAsync(
            processClient,
            project.Id,
            Sb02TemplateName,
            candidate => candidate.Id == definition.Id && candidate.HasPublishedVersion,
            30_000);

        await PostJsonAndReadApiAsync<ProjectStructureLinkChangeResult>(
            agentClient,
            $"/api/project-structure/projects/{project.Id:D}/nodes/{workNode.Id}/process-definition",
            new ProjectStructureProcessDefinitionLinkInput(definition.Id, lease.LeaseToken));

        response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{project.Id:D}/structure");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected project structure route to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
        await page.WaitForSelectorAsync("text=Structure canvas", new PageWaitForSelectorOptions {
            Timeout = 90_000
        });
        await page.Locator(".cw-workbench-shell").WaitForAsync(new LocatorWaitForOptions {
            Timeout = 90_000
        });
        await WaitForInitializedCanvasHostAsync(page);
        await WaitForCanvasRenderIdleAsync(page);

        var definitionNodeId = BuildProjectedProcessDefinitionNodeId(definition.Id);
        await WaitForProjectedProcessDefinitionNodeAsync(page, definitionNodeId, Sb02TemplateName, 90_000);
        await page.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "02-project-template-linked-structure-large-desktop.png"),
            FullPage = false
        });

        var outlineNode = page.GetByTestId(BuildProjectStructureOutlineNodeTestId(definitionNodeId));
        await outlineNode.WaitForAsync(new LocatorWaitForOptions {
            Timeout = 30_000
        });
        await outlineNode.ScrollIntoViewIfNeededAsync();
        await outlineNode.ClickAsync(new LocatorClickOptions {
            Button = MouseButton.Right
        });
        await page.GetByTestId("project-structure-outline-context-menu").WaitForAsync(new LocatorWaitForOptions {
            Timeout = 10_000
        });
        await page.GetByTestId("project-structure-outline-context-action-start-process").ClickAsync();

        var startDialog = page.GetByTestId("project-structure-process-start-dialog");
        await startDialog.WaitForAsync();
        var startDialogText = await startDialog.InnerTextAsync();
        Assert.Contains(workNode.Title, startDialogText, StringComparison.Ordinal);
        Assert.Contains(Sb02TemplateName, startDialogText, StringComparison.Ordinal);
        await startDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "03-project-structure-start-confirm-large-desktop.png")
        });

        await page.GetByTestId("project-structure-process-start-continue").ClickAsync();
        var assignmentDialog = page.GetByTestId(ProjectStructureAssignmentDialogTestId);
        await assignmentDialog.WaitForAsync(new LocatorWaitForOptions {
            Timeout = 90_000
        });
        await page.GetByTestId(ProjectStructureAssignmentRoleCardsTestId).WaitForAsync(new LocatorWaitForOptions {
            Timeout = 90_000
        });
        await assignmentDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "04-project-structure-assignment-review-large-desktop.png")
        });

        var expectedLaunchName = $"{workNode.Title} / {Sb02TemplateName}";
        var launchPlan = await WaitForProjectLaunchPlanAsync(
            processClient,
            project.Id,
            definition.Id,
            expectedLaunchName,
            plan => plan.Status == ProcessLaunchPlanStatus.Draft,
            30_000);
        var launchPlanDetails = await ReadRequiredJsonAsync<ProcessLaunchPlanDetails>(
            processClient,
            $"/api/processes/launch-plans/{launchPlan.Id:D}");
        Assert.Equal(project.Id, launchPlanDetails.ProjectId);
        Assert.Contains("Started from project structure.", launchPlanDetails.TriggerReason, StringComparison.Ordinal);
        Assert.Contains("Project structure context JSON:", launchPlanDetails.TriggerReason, StringComparison.Ordinal);
        Assert.Contains(workNode.Id, launchPlanDetails.TriggerReason, StringComparison.Ordinal);

        await ResolveProjectStructureAssignmentsAsync(page, artifactsDir);
        launchPlanDetails = await SelectSb02ProcessMockLaunchCandidatesAsync(runtimeServiceProvider, launchPlan.Id);
        Assert.All(launchPlanDetails.Roles, role =>
            Assert.True(
                role.IsResolved && !role.RequiresProvisioning && role.SelectedCandidateId.HasValue,
                $"Expected launch role '{role.RoleKey}' to be bound to a runnable process-mock agent."));
        await assignmentDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "05-project-structure-assignment-ready-large-desktop.png")
        });

        await page.GetByTestId(ProjectStructureAssignmentReviewStartTestId).ClickAsync();
        await page.WaitForURLAsync(
            $"**/projects/{project.Id:D}/processes?processId={definition.Id:D}&runId=*",
            new PageWaitForURLOptions {
                Timeout = 90_000
            });
        var runId = ExtractRequiredQueryGuid(page.Url, "runId");
        var run = await WaitForProjectRunAsync(processClient, project.Id, definition.Id, runId, 90_000);
        Assert.Equal(project.Id, run.ProjectId);
        Assert.Equal(expectedLaunchName, run.Name);

        launchPlanDetails = await ReadRequiredJsonAsync<ProcessLaunchPlanDetails>(
            processClient,
            $"/api/processes/launch-plans/{launchPlan.Id:D}");
        Assert.Equal(ProcessLaunchPlanStatus.Executing, launchPlanDetails.Status);
        Assert.Equal(runId, launchPlanDetails.GeneratedRunId);

        var apiDetail = await ReadRequiredJsonAsync<ProcessRunDetailApiProof>(
            processClient,
            $"/api/processes/runs/{runId:D}?includeWorkBriefs=false&includeExecutionRuns=false&includeDirectMessages=false");
        Assert.NotNull(apiDetail.Run);
        Assert.Equal(runId, apiDetail.Run!.Id);
        Assert.Equal(project.Id, apiDetail.Run.ProjectId);
        Assert.True(apiDetail.StepRuns.Count > 0, "Expected the executed template run to create durable step runs.");

        await DrainSb02ProcessRunOutboxUntilSettledAsync(runtimeServiceProvider, runId, TimeSpan.FromSeconds(180));
        apiDetail = await WaitForCompletedProjectRunApiProofAsync(processClient, runId, 240_000);
        Assert.NotNull(apiDetail.Run);
        Assert.Equal(ProcessRunStatus.Completed, apiDetail.Run!.Status);
        Assert.NotEmpty(apiDetail.Artifacts);
        Assert.Contains(apiDetail.Artifacts, artifact => !string.IsNullOrWhiteSpace(artifact.ManagedStoragePath));
        Assert.NotEmpty(apiDetail.OutboxRecords);
        Assert.NotEmpty(apiDetail.ExecutionRuns);
        Assert.All(apiDetail.StepRuns, stepRun =>
            Assert.True(
                IsCompletedOrSkippedStepStatus(stepRun.Status),
                $"Expected step '{stepRun.Title}' to be completed or skipped, got {stepRun.Status}."));
        Assert.All(apiDetail.OutboxRecords, outbox =>
            Assert.True(
                outbox.Status == ProcessOutboxRecordStatus.Completed && outbox.CompletedAtUtc.HasValue,
                $"Expected outbox command '{outbox.CommandKey}' to be completed, got {outbox.Status}."));
        Assert.All(apiDetail.ExecutionRuns, executionRun =>
            Assert.True(
                executionRun.State == ExecutionState.Completed &&
                executionRun.Outcome == RunOutcome.Succeeded &&
                executionRun.CompletedAtUtc.HasValue,
                $"Expected execution run '{executionRun.Title}' to succeed, got {executionRun.State}/{executionRun.Outcome}."));

        response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{project.Id:D}/processes?processId={definition.Id:D}&runId={runId:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected completed project process route to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
        await page.GetByTestId("processes-workspace-shell").WaitForAsync();
        await page.GetByRole(AriaRole.Tab, new() {
            Name = "Runs",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-runs-tab-shell").WaitForAsync();
        await page.GetByTestId($"processes-run-history-item-{runId:D}").WaitForAsync(new LocatorWaitForOptions {
            Timeout = 30_000
        });
        await page.GetByTestId("processes-selected-run-summary").WaitForAsync(new LocatorWaitForOptions {
            Timeout = 30_000
        });
        var selectedRunSummary = await page.GetByTestId("processes-selected-run-summary").InnerTextAsync();
        Assert.Contains(expectedLaunchName, selectedRunSummary, StringComparison.Ordinal);
        Assert.Contains(ProcessRunStatus.Completed.ToString(), selectedRunSummary, StringComparison.Ordinal);
        Assert.Contains(apiDetail.Run.TotalStepCount.ToString(), selectedRunSummary, StringComparison.Ordinal);
        await page.GetByTestId("processes-selected-run-summary").ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "06-project-run-completed-summary-large-desktop.png")
        });

        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Evidence",
            Exact = true
        }).ClickAsync();
        await WaitForBodyTextAsync(page, "Artifacts, decisions, and briefs", 30_000);
        await WaitForBodyTextAsync(page, "Expectation ledger", 30_000);
        await WaitForBodyTextAsync(page, "Business strategy intake brief", 30_000);
        await WaitForBodyTextAsync(page, "Artifact record:", 30_000);
        await page.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "07-project-run-artifacts-readback-large-desktop.png"),
            FullPage = true
        });

        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Execution",
            Exact = true
        }).ClickAsync();
        var runtimeHostReadback = page.GetByTestId("processes-runtime-host-readback");
        await runtimeHostReadback.WaitForAsync(new LocatorWaitForOptions {
            Timeout = 30_000
        });
        await WaitForBodyTextAsync(page, "Operator readback", 30_000);
        var runtimeHostReadbackText = await runtimeHostReadback.InnerTextAsync();
        Assert.DoesNotContain("Runtime-host readback failed", runtimeHostReadbackText, StringComparison.Ordinal);
        Assert.Contains(runId.ToString("D"), runtimeHostReadbackText, StringComparison.Ordinal);
        Assert.Contains("process-workspace:run-detail-runtime-host-readback", runtimeHostReadbackText, StringComparison.Ordinal);
        Assert.Contains("Capability", runtimeHostReadbackText, StringComparison.Ordinal);
        Assert.Contains("Hash", runtimeHostReadbackText, StringComparison.Ordinal);
        Assert.Contains("evidence refs", runtimeHostReadbackText, StringComparison.Ordinal);
        Assert.Contains("No mutation", runtimeHostReadbackText, StringComparison.Ordinal);
        Assert.Contains("process writes: denied", runtimeHostReadbackText, StringComparison.Ordinal);
        Assert.Contains("transition writes: denied", runtimeHostReadbackText, StringComparison.Ordinal);
        Assert.Contains("finalizer writes: denied", runtimeHostReadbackText, StringComparison.Ordinal);
        await runtimeHostReadback.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "08-project-run-runtime-host-readback-large-desktop.png")
        });

        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Activity",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId($"processes-run-history-item-{runId:D}").ClickAsync();
        var runStepsDialog = page.GetByTestId("processes-run-steps-dialog");
        await runStepsDialog.GetByTestId("processes-run-steps-dialog-step-list").WaitForAsync(new LocatorWaitForOptions {
            Timeout = 30_000
        });
        var runStepsDialogText = await runStepsDialog.InnerTextAsync();
        Assert.Contains(ProcessRunStatus.Completed.ToString(), runStepsDialogText, StringComparison.Ordinal);
        await runStepsDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "09-project-run-completed-steps-large-desktop.png")
        });
        await runStepsDialog.GetByRole(AriaRole.Button, new() {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await runStepsDialog.WaitForAsync(new() {
            State = WaitForSelectorState.Detached
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Project_scoped_process_workspace_SB010_INV_001_preserves_project_and_launch_plan_context() {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "project-scoped-process-launch");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        using var apiClient = CreateProcessApiClient(fixture.BaseUrl);
        var projectName = $"SB010 Project {Guid.NewGuid():N}";
        var projectId = await CreateProjectAsync(page, projectName, "Execution");

        var response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/processes");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected project process route to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
        await page.GetByTestId("processes-workspace-shell").WaitForAsync();
        await page.GetByTestId("processes-templates-button").WaitForAsync();
        await page.GetByTestId("processes-templates-button").ClickAsync();

        var templateDialog = page.GetByTestId("processes-template-library-dialog");
        await templateDialog.WaitForAsync();
        await templateDialog.GetByPlaceholder("Search templates, roles, artifacts, governance, or evidence")
            .FillAsync("Business plan development");
        await page.GetByTestId("processes-template-library-item-business-plan-development").WaitForAsync();
        await page.GetByTestId("processes-template-library-item-business-plan-development").ClickAsync();
        await templateDialog.GetByRole(AriaRole.Heading, new() {
            Name = "Business plan development",
            Exact = true
        }).WaitForAsync();
        await WaitForBodyTextAsync(page, projectName, 30_000);
        await templateDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "01-project-template-selected-large-desktop.png")
        });

        await page.GetByTestId("processes-template-library-add-button").ClickAsync();
        var definition = await WaitForProjectDefinitionAsync(
            apiClient,
            projectId,
            "Business plan development",
            candidate => candidate.StepCount > 0,
            30_000);
        Assert.Equal(projectId, definition.ProjectId);

        await templateDialog.GetByRole(AriaRole.Button, new() {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await templateDialog.WaitForAsync(new() {
            State = WaitForSelectorState.Detached
        });

        await PostApiAsync(apiClient, $"/api/processes/definitions/{definition.Id:D}/publish");
        definition = await WaitForProjectDefinitionAsync(
            apiClient,
            projectId,
            "Business plan development",
            candidate => candidate.Id == definition.Id && candidate.HasPublishedVersion,
            30_000);

        var launchName = $"SB010 project launch {Guid.NewGuid():N}";
        response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/processes?processId={definition.Id:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected selected project process route to return 2xx, got {(int)response.Status}.");
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await WaitForBodyTextAsync(page, "Business plan development", 30_000);
        await page.GetByRole(AriaRole.Tab, new() {
            Name = "Runs",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-runs-tab-shell").WaitForAsync();
        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Launch",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-launch-name-input").FillAsync(launchName);
        await page.GetByTestId("processes-create-launch-plan-button").ClickAsync();
        await WaitForBodyTextAsync(page, "Launch plan created.", 30_000);
        await page.GetByTestId("processes-launch-plan-detail").WaitForAsync();
        await WaitForBodyTextAsync(page, launchName, 30_000);
        await page.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "02-project-launch-plan-created-large-desktop.png"),
            FullPage = false
        });

        var launchPlan = await WaitForProjectLaunchPlanAsync(
            apiClient,
            projectId,
            definition.Id,
            launchName,
            plan => plan.Status == ProcessLaunchPlanStatus.Draft,
            30_000);
        Assert.Equal(projectId, launchPlan.ProjectId);

        response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/processes?processId={definition.Id:D}&launchPlanId={launchPlan.Id:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected selected project launch plan route to return 2xx, got {(int)response.Status}.");
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await page.GetByRole(AriaRole.Tab, new() {
            Name = "Runs",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Launch",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-launch-plan-detail").WaitForAsync();
        await WaitForBodyTextAsync(page, launchName, 30_000);
        await page.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "03-project-launch-plan-query-large-desktop.png"),
            FullPage = false
        });

        Assert.Contains($"/projects/{projectId:D}/processes", page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"processId={definition.Id:D}", page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"launchPlanId={launchPlan.Id:D}", page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task<ProcessDefinitionListItem> WaitForProjectDefinitionAsync(
        HttpClient client,
        Guid projectId,
        string definitionName,
        Func<ProcessDefinitionListItem, bool> predicate,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var definitions = await ReadRequiredJsonAsync<IReadOnlyList<ProcessDefinitionListItem>>(
                client,
                $"/api/processes/definitions?projectId={projectId:D}");
            var match = definitions
                .Where(definition => definition.ProjectId == projectId)
                .Where(definition => string.Equals(definition.Name, definitionName, StringComparison.Ordinal))
                .Where(predicate)
                .OrderByDescending(definition => definition.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for project process definition '{definitionName}'.");
    }

    private static async Task<ProcessLaunchPlanListItem> WaitForProjectLaunchPlanAsync(
        HttpClient client,
        Guid projectId,
        Guid definitionId,
        string launchName,
        Func<ProcessLaunchPlanListItem, bool> predicate,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var launchPlans = await ReadRequiredJsonAsync<IReadOnlyList<ProcessLaunchPlanListItem>>(
                client,
                $"/api/processes/launch-plans?definitionId={definitionId:D}&projectId={projectId:D}&take=50");
            var match = launchPlans
                .Where(plan => plan.ProjectId == projectId)
                .Where(plan => string.Equals(plan.Name, launchName, StringComparison.Ordinal))
                .Where(predicate)
                .OrderByDescending(plan => plan.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for project launch plan '{launchName}'.");
    }

    private static async Task<ProcessRunListItem> WaitForProjectRunAsync(
        HttpClient client,
        Guid projectId,
        Guid definitionId,
        Guid runId,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var runs = await ReadRequiredJsonAsync<IReadOnlyList<ProcessRunListItem>>(
                client,
                $"/api/processes/runs?definitionId={definitionId:D}&projectId={projectId:D}&take=50");
            var match = runs
                .Where(run => run.Id == runId)
                .Where(run => run.ProjectId == projectId)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for project process run '{runId:D}'.");
    }

    private static async Task<ProcessRunDetailApiProof> WaitForCompletedProjectRunApiProofAsync(
        HttpClient client,
        Guid runId,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        var lastSnapshot = "not loaded";
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var detail = await ReadRequiredJsonAsync<ProcessRunDetailApiProof>(
                client,
                $"/api/processes/runs/{runId:D}?includeWorkBriefs=false&includeDirectMessages=false&take=200");
            lastSnapshot = BuildCompletionSnapshot(detail);
            if (IsCompletedProjectRunApiProof(detail)) {
                return detail;
            }

            await Task.Delay(1_000);
        }

        throw new TimeoutException($"Timed out waiting for completed project process run '{runId:D}'. Last snapshot: {lastSnapshot}");
    }

    private ServiceProvider BuildSb02RuntimeServiceProvider() {
        var activeProfile = CreateSb02ActiveProfile();
        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(
            activeProfile.EnvironmentRootPath,
            "CanDoItAll.Tests.Playwright.SB02ProcessRuntime");
        var configuration = TestApplicationBootstrap.BuildConfiguration(
            activeProfile,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false",
                [$"{ProcessMockAgentOptions.SectionName}:Enabled"] = "true"
            });

        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);
        return services.BuildServiceProvider(new ServiceProviderOptions {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private TestDatabaseProfile CreateSb02ActiveProfile() {
        if (string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString)) {
            throw new InvalidOperationException("Playwright fixture did not expose a database connection string.");
        }

        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot)) {
            throw new InvalidOperationException("Playwright fixture did not expose the storage workspace root.");
        }

        var workspaceRoot = fixture.StorageWorkspaceRoot;
        var profileRoot = Directory.GetParent(workspaceRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not resolve profile root from '{workspaceRoot}'.");
        var environmentRoot = Path.GetFullPath(Path.Combine(profileRoot, "..", ".."));

        return new TestDatabaseProfile(
            "playwright-sb02-runtime",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.PostgreSql,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private static async Task EnsureSb02ProcessMockCatalogAsync(ServiceProvider serviceProvider) {
        await using var scope = serviceProvider.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();
        var context = await catalogService.EnsureCatalogAsync();

        Assert.NotNull(context);
        Assert.Contains(ProcessMockAgentRoleKeys.BusinessStrategist, context!.AgentIdsByRoleKey.Keys);
        Assert.Contains(ProcessMockAgentRoleKeys.FinancialStrategist, context.AgentIdsByRoleKey.Keys);
        Assert.Contains(ProcessMockAgentRoleKeys.MarketingSpecialist, context.AgentIdsByRoleKey.Keys);
    }

    private static async Task<ProcessLaunchPlanDetails> SelectSb02ProcessMockLaunchCandidatesAsync(
        ServiceProvider serviceProvider,
        Guid launchPlanId) {
        await using var scope = serviceProvider.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();
        var catalog = await catalogService.EnsureCatalogAsync();
        Assert.NotNull(catalog);

        var launchDetails = await processesService.GetLaunchPlanAsync(launchPlanId);
        Assert.NotNull(launchDetails);

        var mockRoleByTemplateRoleKey = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["business-strategist"] = ProcessMockAgentRoleKeys.BusinessStrategist,
            ["financial-strategist"] = ProcessMockAgentRoleKeys.FinancialStrategist,
            ["marketing-specialist"] = ProcessMockAgentRoleKeys.MarketingSpecialist
        };
        var missingRequiredRoleKeys = launchDetails!.Roles
            .Where(role => role.IsRequired)
            .Where(role => !mockRoleByTemplateRoleKey.ContainsKey(role.RoleKey))
            .Select(role => role.RoleKey)
            .OrderBy(roleKey => roleKey, StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            missingRequiredRoleKeys.Length == 0,
            $"Process-mock role mapping is missing required launch roles: {string.Join(", ", missingRequiredRoleKeys)}.");

        foreach (var (templateRoleKey, processMockRoleKey) in mockRoleByTemplateRoleKey) {
            var launchRole = Assert.Single(
                launchDetails.Roles,
                role => string.Equals(role.RoleKey, templateRoleKey, StringComparison.Ordinal));
            Assert.True(
                catalog!.AgentIdsByRoleKey.TryGetValue(processMockRoleKey, out var technicalAgentId),
                $"Process-mock role '{processMockRoleKey}' was not seeded.");

            var selectResult = await processesService.SelectLaunchTechnicalAgentAsync(new ProcessLaunchTechnicalAgentSelectionRequest {
                LaunchPlanId = launchPlanId,
                LaunchPlanRoleId = launchRole.Id,
                TechnicalAgentId = technicalAgentId
            });
            Assert.True(selectResult.IsSuccess, string.Join(" | ", selectResult.Errors.Select(error => error.Message)));
        }

        launchDetails = await processesService.GetLaunchPlanAsync(launchPlanId);
        Assert.NotNull(launchDetails);
        return launchDetails!;
    }

    private static async Task DrainSb02ProcessRunOutboxUntilSettledAsync(
        ServiceProvider serviceProvider,
        Guid runId,
        TimeSpan timeout) {
        await using var scope = serviceProvider.CreateAsyncScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var timeoutAt = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < timeoutAt) {
            await outboxService.ProcessPendingAsync(
                take: 20,
                leaseDuration: TimeSpan.FromMinutes(1),
                maxParallelism: 1);

            var outboxRecords = await ListSb02RunOutboxRecordsAsync(dbContextFactory, runId);
            var deadLetteredOutbox = outboxRecords.FirstOrDefault(outbox => outbox.Status == ProcessOutboxRecordStatus.DeadLettered);
            if (deadLetteredOutbox is not null) {
                Assert.Fail(await BuildSb02RunDiagnosticsAsync(
                    dbContextFactory,
                    processesService,
                    runId,
                    $"Process outbox record {deadLetteredOutbox.Id:D} dead-lettered."));
            }

            var run = await processesService.GetRunAsync(runId);
            if (run?.Status == ProcessRunStatus.Completed &&
                outboxRecords.Count > 0 &&
                outboxRecords.All(outbox => outbox.Status == ProcessOutboxRecordStatus.Completed)) {
                return;
            }

            if (run?.Status == ProcessRunStatus.Failed) {
                Assert.Fail(await BuildSb02RunDiagnosticsAsync(
                    dbContextFactory,
                    processesService,
                    runId,
                    $"Process run {runId:D} failed before SB02 runtime completion."));
            }

            await Task.Delay(100);
        }

        Assert.Fail(await BuildSb02RunDiagnosticsAsync(
            dbContextFactory,
            processesService,
            runId,
            $"Timed out draining process outbox for SB02 run {runId:D}."));
    }

    private static async Task<IReadOnlyList<ProcessOutboxRecord>> ListSb02RunOutboxRecordsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<ProcessOutboxRecord>()
            .AsNoTracking()
            .Where(outbox => outbox.ProcessRunId == runId)
            .OrderBy(outbox => outbox.CreatedAtUtc)
            .ThenBy(outbox => outbox.Id)
            .ToListAsync();
    }

    private static async Task<string> BuildSb02RunDiagnosticsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ProcessesService processesService,
        Guid runId,
        string reason) {
        var builder = new System.Text.StringBuilder()
            .AppendLine(reason);

        var run = await processesService.GetRunAsync(runId);
        builder.Append("Run status: ")
            .AppendLine(run?.Status.ToString() ?? "missing");

        var stepRuns = await processesService.ListStepRunsAsync(runId);
        builder.AppendLine("Steps:");
        foreach (var stepRun in stepRuns.OrderBy(stepRun => stepRun.Sequence)) {
            builder.Append("  ")
                .Append(stepRun.Sequence)
                .Append(": ")
                .Append(stepRun.Title)
                .Append(" / ")
                .Append(stepRun.Status)
                .Append(" / blockCode=")
                .Append(stepRun.BlockReasonCode)
                .Append(" / nextRecovery=")
                .Append(stepRun.NextRecoveryAction)
                .Append(" / decision=")
                .AppendLine(stepRun.DecisionSummary);
            AppendSb02DiagnosticLine(builder, "blocked", stepRun.BlockedReason);
            AppendSb02DiagnosticLine(builder, "refusal", stepRun.RefusalReason);
            AppendSb02DiagnosticLine(builder, "exception", stepRun.ExceptionSummary);
            AppendSb02DiagnosticLine(builder, "health", stepRun.Health.ActionableReason);
        }

        var outboxRecords = await ListSb02RunOutboxRecordsAsync(dbContextFactory, runId);
        builder.AppendLine("Outbox:");
        foreach (var outbox in outboxRecords) {
            builder.Append("  ")
                .Append(outbox.CommandKey)
                .Append(" / ")
                .Append(outbox.Status)
                .Append(" / attempts=")
                .Append(outbox.AttemptCount)
                .Append(" / lastError=")
                .AppendLine(outbox.LastError);
        }

        return builder.ToString();
    }

    private static void AppendSb02DiagnosticLine(
        System.Text.StringBuilder builder,
        string label,
        string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return;
        }

        builder.Append("    ")
            .Append(label)
            .Append(": ")
            .AppendLine(value);
    }

    private static bool IsCompletedProjectRunApiProof(ProcessRunDetailApiProof detail) {
        return detail.Run?.Status == ProcessRunStatus.Completed &&
            detail.StepRuns.Count > 0 &&
            detail.StepRuns.All(stepRun => IsCompletedOrSkippedStepStatus(stepRun.Status)) &&
            detail.Artifacts.Count > 0 &&
            detail.Artifacts.Any(artifact => !string.IsNullOrWhiteSpace(artifact.ManagedStoragePath)) &&
            detail.OutboxRecords.Count > 0 &&
            detail.OutboxRecords.All(outbox => outbox.Status == ProcessOutboxRecordStatus.Completed && outbox.CompletedAtUtc.HasValue) &&
            detail.ExecutionRuns.Count > 0 &&
            detail.ExecutionRuns.All(executionRun =>
                executionRun.State == ExecutionState.Completed &&
                executionRun.Outcome == RunOutcome.Succeeded &&
                executionRun.CompletedAtUtc.HasValue);
    }

    private static bool IsCompletedOrSkippedStepStatus(ProcessStepRunStatus status) {
        return status is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Skipped;
    }

    private static string BuildCompletionSnapshot(ProcessRunDetailApiProof detail) {
        var runStatus = detail.Run?.Status.ToString() ?? "missing";
        var stepStatusSummary = string.Join(
            ", ",
            detail.StepRuns
                .GroupBy(stepRun => stepRun.Status)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}:{group.Count()}"));
        var outboxStatusSummary = string.Join(
            ", ",
            detail.OutboxRecords
                .GroupBy(outbox => outbox.Status)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}:{group.Count()}"));
        var executionStatusSummary = string.Join(
            ", ",
            detail.ExecutionRuns
                .GroupBy(executionRun => $"{executionRun.State}/{executionRun.Outcome}")
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}:{group.Count()}"));

        return $"run={runStatus}; steps=[{stepStatusSummary}]; artifacts={detail.Artifacts.Count}; outbox=[{outboxStatusSummary}]; executions=[{executionStatusSummary}]";
    }

    private static async Task ResolveProjectStructureAssignmentsAsync(IPage page, string artifactsDir) {
        if (!await TryWaitForEnabledTestIdAsync(page, ProjectStructureAssignmentReviewStartTestId, 2_000)) {
            await page.GetByTestId(ProjectStructureAssignmentAskHrTestId).ClickAsync();
            await page.GetByTestId(ProjectStructureAssignmentConfirmHrTestId).WaitForAsync(new LocatorWaitForOptions {
                Timeout = 30_000
            });
            await page.GetByTestId(ProjectStructureAssignmentConfirmHrTestId).ScreenshotAsync(new() {
                Path = Path.Combine(artifactsDir, "04a-project-structure-hr-confirm-large-desktop.png")
            });
            await page.GetByTestId(ProjectStructureAssignmentConfirmHrTestId).ClickAsync();
            await page.GetByTestId(ProjectStructureAssignmentRoleCardsTestId).WaitForAsync(new LocatorWaitForOptions {
                Timeout = 90_000
            });
        }

        await TryUseVisibleProjectStructureRecommendationsAsync(page, 30_000);
        await WaitForEnabledTestIdAsync(page, ProjectStructureAssignmentReviewStartTestId, 90_000);
    }

    private static async Task TryUseVisibleProjectStructureRecommendationsAsync(IPage page, int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            if (await TryWaitForEnabledTestIdAsync(page, ProjectStructureAssignmentReviewStartTestId, 250)) {
                return;
            }

            var recommendationButtons = page.Locator("[data-testid^='project-structure-process-assignment-use-recommendation-']");
            var buttonCount = await recommendationButtons.CountAsync();
            var clicked = false;
            for (var index = 0; index < buttonCount; index++) {
                var button = recommendationButtons.Nth(index);
                if (!await button.IsVisibleAsync() || !await button.IsEnabledAsync()) {
                    continue;
                }

                await button.ClickAsync();
                clicked = true;
                await page.WaitForTimeoutAsync(250);
                break;
            }

            if (!clicked) {
                return;
            }
        }
    }

    private static async Task<bool> TryWaitForEnabledTestIdAsync(IPage page, string testId, int timeoutMs) {
        var locator = page.GetByTestId(testId);
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            try {
                if (await locator.IsEnabledAsync()) {
                    return true;
                }
            }
            catch (PlaywrightException) {
            }

            await Task.Delay(250);
        }

        return false;
    }

    private static async Task WaitForProjectedProcessDefinitionNodeAsync(
        IPage page,
        string nodeId,
        string titleFragment,
        int timeoutMs) {
        await page.WaitForFunctionAsync(
            @"input => {
                const host = document.querySelector('.cw-canvas-host');
                const surfaceNodes = Array.isArray(host?.__canvasWorkbenchState?.surface?.nodes)
                    ? host.__canvasWorkbenchState.surface.nodes
                    : [];

                return surfaceNodes.some(node =>
                    node?.id === input.nodeId &&
                    (node.title || node.subtitle || node.id || '').includes(input.titleFragment));
            }",
            new {
                nodeId,
                titleFragment
            },
            new PageWaitForFunctionOptions {
                Timeout = timeoutMs
            });
    }

    private static string BuildProjectedProcessDefinitionNodeId(Guid definitionId) {
        return string.Concat(ProjectStructureProcessDefinitionNodePrefix, definitionId.ToString("D"));
    }

    private static string BuildProjectStructureOutlineNodeTestId(string nodeId) {
        var buffer = new char[nodeId.Length];
        for (var index = 0; index < nodeId.Length; index++) {
            var character = nodeId[index];
            buffer[index] = char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-';
        }

        return $"project-structure-outline-node-{new string(buffer)}";
    }

    private static Guid ExtractRequiredQueryGuid(string url, string queryName) {
        var uri = new Uri(url);
        var query = uri.Query.TrimStart('?');
        var segments = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments) {
            var pair = segment.Split('=', 2);
            if (pair.Length != 2 || !string.Equals(pair[0], queryName, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            return Guid.Parse(Uri.UnescapeDataString(pair[1]));
        }

        throw new InvalidOperationException($"The URL '{url}' does not include required query parameter '{queryName}'.");
    }
}
