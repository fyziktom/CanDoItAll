using System.IO;
using System.Net.Http.Json;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    private const string AgentFrameworkIntegrationProjectId = "eaee1691-f5cf-49b1-a43d-1c8cd07d50f0";

    [Fact]
    [Trait("Surface", "ProjectStructure")]
    public async Task Project_structure_process_run_output_SB012_INV_002_opens_project_processes_from_output_folder_node()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "project-structure-run-output-sb012");
        ResetDirectory(artifactsDir);

        using var agentClient = CreateProjectStructureAgentApiClient(fixture.BaseUrl);
        using var processClient = CreateProcessApiClient(fixture.BaseUrl);
        var suffix = Guid.NewGuid().ToString("N");
        var project = await PostJsonAndReadSb012ApiAsync<ProjectSummary>(
            agentClient,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                $"SB012 browser project {suffix}",
                "Browser project-structure run output proof",
                "Verify projected process run output folders can open the process workspace.",
                "Execution",
                ProjectStatus.Active));
        var lease = await PostJsonAndReadSb012ApiAsync<ProjectStructureLeaseSnapshot>(
            agentClient,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString("D"),
                "SB012 browser process run output proof",
                15));
        var workNode = await PostJsonAndReadSb012ApiAsync<ProjectStructureNodeSummary>(
            agentClient,
            $"/api/project-structure/projects/{project.Id:D}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.WorkItem,
                $"SB012 browser run output {suffix}",
                "Process execution target",
                "Selected work item used for browser-visible run-output proof.",
                $"project:{project.Id:D}",
                360,
                260,
                null,
                null,
                "task",
                null,
                null,
                lease.LeaseToken));
        var definitionId = await PostJsonAndReadSb012ApiAsync<Guid>(
            processClient,
            "/api/processes/definitions",
            BuildSb012BrowserProcessDefinition(project.Id, suffix));
        await PostApiAsync(processClient, $"/api/processes/definitions/{definitionId:D}/publish");
        await PostJsonAndReadSb012ApiAsync<ProjectStructureLinkChangeResult>(
            agentClient,
            $"/api/project-structure/projects/{project.Id:D}/nodes/{workNode.Id}/process-definition",
            new ProjectStructureProcessDefinitionLinkInput(definitionId, lease.LeaseToken));
        var startResult = await PostJsonAndReadSb012ApiAsync<ProjectStructureProcessNodeStartResult>(
            agentClient,
            $"/api/project-structure/projects/{project.Id:D}/nodes/{workNode.Id}/process/start",
            new ProjectStructureProcessNodeStartInput(
                definitionId,
                RunHrMatch: false,
                Execute: true,
                IncludeLaunchPlan: true,
                RequestedBy: "sb012-playwright",
                LeaseToken: lease.LeaseToken));

        Assert.Equal("run-started", startResult.Stage);
        Assert.NotNull(startResult.RunId);
        var runId = startResult.RunId!.Value;
        var runs = await ReadRequiredJsonAsync<IReadOnlyList<ProcessRunListItem>>(
            processClient,
            $"/api/processes/runs?definitionId={definitionId:D}&projectId={project.Id:D}&take=50");
        var run = Assert.Single(runs, candidate => candidate.Id == runId);
        Assert.Equal(project.Id, run.ProjectId);
        var outputPath = $"output/scopes/project/{project.Id:D}/process-runs/{runId:D}/SB012BrowserOutput/index.html";
        await PostJsonAndReadSb012ApiAsync<Guid>(
            processClient,
            "/api/processes/artifacts",
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = runId,
                ArtifactKind = ProcessArtifactKind.Deliverable,
                Title = "SB012 browser output",
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ProvenanceSummary = "Recorded by SB012 browser output proof.",
                AllowedFutureUsageSummary = "Validate projected process run output folder navigation.",
                ReviewSummary = "Output folder should open the project process workspace.",
                ManagedStoragePath = outputPath
            });

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{project.Id:D}/structure");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected project structure route to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page);
        await page.WaitForSelectorAsync("text=Structure canvas", new PageWaitForSelectorOptions
        {
            Timeout = 90_000
        });
        await page.Locator(".cw-workbench-shell").WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = 90_000
        });
        await WaitForInitializedCanvasHostAsync(page);
        await WaitForCanvasRenderIdleAsync(page);

        var outputNode = await WaitForSb012OutputProjectionAsync(page, runId, "SB012BrowserOutput", 90_000);
        Assert.Equal($"process-run:{runId:D}", outputNode.ParentId);
        Assert.StartsWith("process-run-output:", outputNode.Id, StringComparison.Ordinal);
        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "01-structure-run-output-node-large-desktop.png"),
            FullPage = false
        });

        await OpenProjectedNodeQuickActionsByIdAsync(page, outputNode.Id);
        var quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        var primaryQuickAction = page.GetByTestId("project-structure-quick-action-primary");
        await primaryQuickAction.WaitForAsync();
        Assert.Contains("Open", await primaryQuickAction.TextContentAsync(), StringComparison.OrdinalIgnoreCase);
        await CaptureLocatorAsync(quickActionDialog, Path.Combine(artifactsDir, "02-run-output-quick-actions-large-desktop.png"));

        var runPopupTask = context.WaitForPageAsync();
        await primaryQuickAction.ClickAsync();
        var runPopup = await runPopupTask;
        await runPopup.WaitForURLAsync($"**/projects/{project.Id:D}/processes?processId={definitionId:D}&runId={runId:D}");
        await DismissStartupModalIfPresentAsync(runPopup, timeoutMs: 15_000);
        await runPopup.GetByTestId("processes-workspace-shell").WaitForAsync();
        await runPopup.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "03-run-output-process-workspace-before-history-wait-large-desktop.png"),
            FullPage = false
        });
        try
        {
            await runPopup.GetByTestId($"processes-run-history-item-{runId:D}").WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = 30_000
            });
        }
        catch (TimeoutException exception)
        {
            var bodyText = await runPopup.Locator("body").InnerTextAsync();
            throw new TimeoutException(
                $"{exception.Message}{Environment.NewLine}Body snapshot:{Environment.NewLine}{bodyText[..Math.Min(bodyText.Length, 4_000)]}{Environment.NewLine}App log snapshot:{Environment.NewLine}{fixture.GetLogSnapshot()}",
                exception);
        }
        await runPopup.GetByTestId("processes-selected-run-summary").WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = 30_000
        });
        var selectedRunSummary = await runPopup.GetByTestId("processes-selected-run-summary").InnerTextAsync();
        Assert.Contains(workNode.Title, selectedRunSummary, StringComparison.Ordinal);
        await runPopup.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "03-run-output-process-workspace-large-desktop.png"),
            FullPage = false
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.False(await runPopup.Locator("#blazor-error-ui").IsVisibleAsync());
        await runPopup.CloseAsync();
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    [Trait("Surface", "ProjectStructure")]
    public async Task Seeded_project_structure_projects_process_nodes_and_opens_process_workspace()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "project-structure-process-projection");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{fixture.BaseUrl}/projects/{AgentFrameworkIntegrationProjectId}/structure");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected seeded structure route to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page);
        await page.WaitForSelectorAsync("text=Structure canvas", new PageWaitForSelectorOptions
        {
            Timeout = 90_000
        });
        await page.Locator(".cw-workbench-shell").WaitForAsync(new LocatorWaitForOptions
        {
            Timeout = 90_000
        });
        await WaitForInitializedCanvasHostAsync(page);
        await WaitForCanvasRenderIdleAsync(page);

        var projection = await page.EvaluateAsync<ProjectStructureProcessProjection>(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const surfaceNodes = Array.isArray(host?.__canvasWorkbenchState?.surface?.nodes)
                    ? host.__canvasWorkbenchState.surface.nodes
                    : [];

                const definitions = surfaceNodes
                    .filter(node => typeof node?.id === 'string' && node.id.startsWith('process-definition:'))
                    .map(node => ({
                        id: node.id,
                        title: node.title || node.subtitle || node.id
                    }));

                const runs = surfaceNodes
                    .filter(node => typeof node?.id === 'string' && node.id.startsWith('process-run:'))
                    .map(node => ({
                        id: node.id,
                        title: node.title || node.subtitle || node.id
                    }));

                return {
                    definitions,
                    runs
                };
            }");

        Assert.NotNull(projection);
        Assert.NotNull(projection!.Definitions);
        Assert.NotNull(projection.Runs);
        Assert.True(projection.Definitions.Length >= 5, $"Expected projected process definitions in the structure graph, got {projection.Definitions.Length}.");
        Assert.True(projection.Runs.Length >= 5, $"Expected projected process runs in the structure graph, got {projection.Runs.Length}.");
        Assert.Contains(
            projection.Definitions,
            node => node.Title.Contains("role-first operating model baseline", StringComparison.OrdinalIgnoreCase));

        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "01-structure-process-projection.png"),
            FullPage = false
        });

        var definitionNode = projection.Definitions[0];
        var runNode = projection.Runs[0];

        await OpenProjectedNodeQuickActionsByIdAsync(page, definitionNode.Id);
        var quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        var primaryQuickAction = page.GetByTestId("project-structure-quick-action-primary");
        await primaryQuickAction.WaitForAsync();
        Assert.Contains("Open Processes", await primaryQuickAction.TextContentAsync(), StringComparison.Ordinal);
        await CaptureLocatorAsync(quickActionDialog, Path.Combine(artifactsDir, "02-process-definition-quick-actions.png"));

        var definitionPopupTask = context.WaitForPageAsync();
        await primaryQuickAction.ClickAsync();
        var definitionPopup = await definitionPopupTask;
        await definitionPopup.WaitForURLAsync($"**/projects/{AgentFrameworkIntegrationProjectId}/processes?processId=*");
        Assert.Contains($"processId={ExtractEntityId(definitionNode.Id)}", definitionPopup.Url, StringComparison.OrdinalIgnoreCase);
        await definitionPopup.CloseAsync();

        await quickActionDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        await OpenProjectedNodeQuickActionsByIdAsync(page, runNode.Id);
        quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        primaryQuickAction = page.GetByTestId("project-structure-quick-action-primary");
        await primaryQuickAction.WaitForAsync();
        Assert.Contains("Open Processes", await primaryQuickAction.TextContentAsync(), StringComparison.Ordinal);
        await CaptureLocatorAsync(quickActionDialog, Path.Combine(artifactsDir, "03-process-run-quick-actions.png"));

        var runPopupTask = context.WaitForPageAsync();
        await primaryQuickAction.ClickAsync();
        var runPopup = await runPopupTask;
        await runPopup.WaitForURLAsync($"**/projects/{AgentFrameworkIntegrationProjectId}/processes?processId=*&runId=*");
        Assert.Contains($"runId={ExtractEntityId(runNode.Id)}", runPopup.Url, StringComparison.OrdinalIgnoreCase);
        await runPopup.CloseAsync();

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task OpenProjectedNodeQuickActionsByIdAsync(IPage page, string nodeId)
    {
        var opened = await page.EvaluateAsync<bool>(
            @"targetId => {
                const host = document.querySelector('.cw-canvas-host');
                const runtime = window.CanDoItAll?.canvasWorkbench;
                if (!host || !runtime?.openNode || !targetId) {
                    return false;
                }

                return runtime.openNode(host, targetId);
            }",
            nodeId);

        Assert.True(opened, $"Expected to open quick actions for projected canvas node '{nodeId}'.");
    }

    private static string ExtractEntityId(string nodeId)
    {
        var separatorIndex = nodeId.IndexOf(':', StringComparison.Ordinal);
        return separatorIndex >= 0 && separatorIndex < nodeId.Length - 1
            ? nodeId[(separatorIndex + 1)..]
            : nodeId;
    }

    private static HttpClient CreateProjectStructureAgentApiClient(
        string baseUrl,
        string agentId = "sb012-playwright-agent",
        string agentName = "SB012 Playwright",
        string branchName = "tests/project-structure-sb012")
    {
        var client = CreateProcessApiClient(baseUrl);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentId, agentId);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentName, agentName);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.MachineName, Environment.MachineName);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.RepositoryRoot, GetRepoRoot());
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.BranchName, branchName);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.SessionId, Guid.NewGuid().ToString("N"));
        return client;
    }

    private static async Task<T> PostJsonAndReadSb012ApiAsync<T>(HttpClient client, string requestUri, object payload)
    {
        using var response = await client.PostAsJsonAsync(requestUri, payload);
        await AssertApiSuccessAsync(response);
        var value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value;
    }

    private static ProcessDefinitionEditorModel BuildSb012BrowserProcessDefinition(Guid projectId, string suffix)
    {
        var observerRoleId = Guid.NewGuid();
        var runStepId = Guid.NewGuid();
        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = $"SB012 browser output process {suffix}",
            Summary = "Browser-visible project-structure run output proof process.",
            ValueStatement = "Preserve project run context and expose output-folder navigation.",
            CustomerName = "Project structure browser validation",
            OwnerName = "Process runtime validation",
            InterfaceContractSummary = "Start from a selected project node and expose the run output folder.",
            GovernanceNotes = "Browser proof must open the process workspace from the projected output folder.",
            ChangeSummary = "Initial SB012 browser proof definition.",
            GovernancePolicySummary = "Executed runs keep project/node context and projected outputs.",
            ConstitutionRuleSummary = "Output-folder projection remains linked to the current run.",
            OperatingModeSummary = "Assisted execution from project-structure API.",
            SimulationReadinessSummary = "Safe deterministic Playwright proof.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = observerRoleId,
                    Key = "browser-output-observer",
                    DisplayName = "Browser output observer",
                    Purpose = "Observe browser-visible run output navigation.",
                    StaffingIntent = "Optional proof role with no required staffing gap.",
                    PreferredExecutorKind = "person",
                    IsRequired = false,
                    AllowsFallback = false,
                    SnapshotSummary = "Optional observer role for SB012 browser proof."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = runStepId,
                    Key = "open-projected-output",
                    Title = "Open projected process output",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Selected project-structure work item.",
                    OutputContractSummary = "Run output folder is projected under the process run node.",
                    EvidenceContractSummary = "Browser opens the project process workspace from the output folder quick action.",
                    DecisionRightsSummary = "Optional observer validates the output route.",
                    ExceptionPolicySummary = "Block if the projected output folder cannot open the run workspace.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            Id = Guid.NewGuid(),
                            RoleRequirementId = observerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Reviewer,
                            IsRequired = false,
                            RebindPolicySummary = "Do not block execution on this optional proof role."
                        }
                    ]
                }
            ]
        };
    }

    private static async Task<ProjectStructureProcessNodeProof> WaitForSb012OutputProjectionAsync(
        IPage page,
        Guid runId,
        string titleFragment,
        int timeoutMs)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var outputNode = await page.EvaluateAsync<ProjectStructureProcessNodeProof?>(
                @"input => {
                    const host = document.querySelector('.cw-canvas-host');
                    const surfaceNodes = Array.isArray(host?.__canvasWorkbenchState?.surface?.nodes)
                        ? host.__canvasWorkbenchState.surface.nodes
                        : [];
                    const expectedParentId = `process-run:${input.runId}`;
                    const match = surfaceNodes
                        .filter(node => typeof node?.id === 'string' && node.id.startsWith('process-run-output:'))
                        .map(node => ({
                            id: node.id,
                            parentId: node.parentId || '',
                            title: node.title || node.subtitle || node.id,
                            artifactKind: node.artifactKind || '',
                            route: node.route || ''
                        }))
                        .find(node =>
                            node.parentId === expectedParentId &&
                            node.title.includes(input.titleFragment));
                    return match || null;
                }",
                new
                {
                    runId = runId.ToString("D"),
                    titleFragment
                });
            if (outputNode is not null)
            {
                return outputNode;
            }

            await Task.Delay(250);
            await WaitForCanvasRenderIdleAsync(page);
        }

        throw new TimeoutException($"Timed out waiting for projected process output node containing '{titleFragment}'.");
    }

    private sealed class ProjectStructureProcessProjection
    {
        public ProjectStructureProcessNodeProof[] Definitions { get; set; } = [];

        public ProjectStructureProcessNodeProof[] Runs { get; set; } = [];
    }

    private sealed class ProjectStructureProcessNodeProof
    {
        public string Id { get; set; } = string.Empty;

        public string ParentId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string ArtifactKind { get; set; } = string.Empty;

        public string Route { get; set; } = string.Empty;
    }
}
