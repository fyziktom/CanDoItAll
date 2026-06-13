using System.Net.Http.Json;
using CanDoItAll.Modules.Processes;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests {
    [Fact]
    public async Task Process_run_detail_recovery_large_screen_displays_blocked_recovery_and_artifact_readback() {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "process-run-detail-recovery-sb030");
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
        var definitionId = await PostJsonAndReadApiAsync<Guid>(
            apiClient,
            "/api/processes/definitions",
            BuildSb030RunDetailDefinition());
        await PostApiAsync(apiClient, $"/api/processes/definitions/{definitionId:D}/publish");

        var runName = $"SB030 blocked run detail {Guid.NewGuid():N}";
        var runId = await PostJsonAndReadApiAsync<Guid>(
            apiClient,
            "/api/processes/runs/start",
            new ProcessRunStartRequest {
                ProcessDefinitionId = definitionId,
                RunName = runName,
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "SB030 Playwright run-detail recovery proof."
            });
        var stepRun = Assert.Single(await ReadRequiredJsonAsync<IReadOnlyList<ProcessStepRunViewModel>>(
            apiClient,
            $"/api/processes/runs/{runId:D}/steps"));
        var artifactExpectation = Assert.Single(stepRun.ArtifactExpectations);

        await PostJsonApiAsync(
            apiClient,
            $"/api/processes/runs/{runId:D}/steps/{stepRun.Id:D}/transition",
            new {
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = "Required runtime evidence artifact was not produced for the selected run.",
                BlockCause = ProcessStepBlockCause.OwnOutput,
                DecidedBy = "sb030-playwright",
                SuppressAutomationDispatch = true
            });
        var artifactId = await PostJsonAndReadApiAsync<Guid>(
            apiClient,
            $"/api/processes/runs/{runId:D}/steps/{stepRun.Id:D}/artifacts",
            new {
                ArtifactExpectationId = artifactExpectation.ArtifactExpectationId,
                ArtifactKind = ProcessArtifactKind.Evidence,
                Title = "SB030 blocked recovery evidence",
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ProvenanceSummary = "Created by SB030 Playwright run-detail recovery proof.",
                AllowedFutureUsageSummary = "Regression validation for run detail recovery UI.",
                ReviewSummary = "Artifact readback should remain visible in the run evidence ledger.",
                ManagedStoragePath = $"artifacts/playwright/sb030/{runId:D}/blocked-recovery-evidence.md",
                ExternalReferenceKey = $"sb030-run-detail-recovery:{runId:D}"
            });

        var apiDetail = await ReadRequiredJsonAsync<ProcessRunDetailApiProof>(
            apiClient,
            $"/api/processes/runs/{runId:D}?includeWorkBriefs=false&includeExecutionRuns=false&includeDirectMessages=false");
        Assert.NotNull(apiDetail.Run);
        Assert.NotNull(apiDetail.Health);
        Assert.Equal(ProcessRunStatus.Blocked, apiDetail.Run!.Status);
        Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, apiDetail.Health!.RecommendedAction);
        var apiStep = Assert.Single(apiDetail.StepRuns);
        Assert.Equal(ProcessStepRunStatus.Blocked, apiStep.Status);
        Assert.Equal(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, apiStep.BlockReasonCode);
        Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, apiStep.NextRecoveryAction);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, apiStep.RecoveryOptions);
        Assert.Contains(apiDetail.Artifacts, artifact => artifact.Id == artifactId);

        var response = await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={definitionId:D}&runId={runId:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /processes run detail route to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
        await page.GetByTestId("processes-workspace-shell").WaitForAsync();
        await page.GetByRole(AriaRole.Tab, new() {
            Name = "Runs",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-runs-tab-shell").WaitForAsync();
        await page.GetByTestId("processes-selected-run-summary").WaitForAsync();
        await WaitForBodyTextAsync(page, runName, 30_000);
        var selectedRunSummary = await page.GetByTestId("processes-selected-run-summary").InnerTextAsync();
        Assert.Contains(runName, selectedRunSummary, StringComparison.Ordinal);
        Assert.Contains("Blocked", selectedRunSummary, StringComparison.Ordinal);
        Assert.Contains("recommended: Recover artifacts only", selectedRunSummary, StringComparison.Ordinal);
        await page.GetByTestId("processes-selected-run-summary").ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "01-selected-run-summary-large-desktop.png")
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
            Path = Path.Combine(artifactsDir, "02-runtime-host-readback-large-desktop.png")
        });

        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Activity",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId($"processes-run-history-item-{runId:D}").ClickAsync();
        var runStepsDialog = page.GetByTestId("processes-run-steps-dialog");
        await runStepsDialog.GetByTestId("processes-run-steps-dialog-step-list").WaitForAsync();
        var recoveryDiagnostics = runStepsDialog.GetByTestId("processes-step-recovery-diagnostics");
        await recoveryDiagnostics.WaitForAsync();
        Assert.Equal(
            ProcessStepBlockReasonCode.ArtifactContractUnsatisfied.ToString(),
            await recoveryDiagnostics.GetAttributeAsync("data-block-reason-code"));
        var recoveryOptions = await recoveryDiagnostics.GetAttributeAsync("data-recovery-options") ?? string.Empty;
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly.ToString(), recoveryOptions, StringComparison.Ordinal);
        var recoveryText = await recoveryDiagnostics.InnerTextAsync();
        Assert.Contains("recommended: Recover artifacts only", recoveryText, StringComparison.Ordinal);
        Assert.Contains("Recover artifacts only", recoveryText, StringComparison.Ordinal);
        await runStepsDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "03-step-recovery-diagnostics-large-desktop.png")
        });

        await runStepsDialog.GetByRole(AriaRole.Button, new() {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await runStepsDialog.WaitForAsync(new() {
            State = WaitForSelectorState.Detached
        });
        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Evidence",
            Exact = true
        }).ClickAsync();
        var artifactLedger = page.GetByTestId("processes-artifact-obligation-ledger");
        await artifactLedger.WaitForAsync();
        var artifactLedgerText = await artifactLedger.InnerTextAsync();
        Assert.Contains("SB030 blocked recovery evidence", artifactLedgerText, StringComparison.Ordinal);
        Assert.Contains("Satisfied", artifactLedgerText, StringComparison.Ordinal);
        Assert.Contains($"Artifact record: {artifactId:D}", artifactLedgerText, StringComparison.Ordinal);
        await artifactLedger.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "04-artifact-ledger-large-desktop.png")
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Process_start_large_screen_imports_template_and_executes_ready_launch_from_ui() {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "process-start-smoke");
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
        var response = await page.GotoAsync($"{fixture.BaseUrl}/processes");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /processes to return 2xx, got {(int)response.Status}.");

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
        await templateDialog.ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "01-template-selected-large-desktop.png")
        });

        await page.GetByTestId("processes-template-library-add-button").ClickAsync();
        var definition = await WaitForDefinitionAsync(
            apiClient,
            "Business plan development",
            candidate => candidate.StepCount > 0,
            30_000);
        await templateDialog.GetByRole(AriaRole.Button, new() {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await templateDialog.WaitForAsync(new() {
            State = WaitForSelectorState.Detached
        });

        await PostApiAsync(apiClient, $"/api/processes/definitions/{definition.Id:D}/publish");
        definition = await WaitForDefinitionAsync(
            apiClient,
            "Business plan development",
            candidate => candidate.Id == definition.Id && candidate.HasPublishedVersion,
            30_000);

        var launchName = $"SB015 process start smoke {Guid.NewGuid():N}";
        response = await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={definition.Id:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected selected process route to return 2xx, got {(int)response.Status}.");
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
        await page.GetByTestId("processes-runs-tab-shell").ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "02-runs-tab-before-launch-large-desktop.png")
        });
        await page.GetByTestId("processes-launch-name-input").FillAsync(launchName);
        await page.GetByTestId("processes-create-launch-plan-button").ClickAsync();
        await WaitForBodyTextAsync(page, "Launch plan created.", 30_000);
        await page.GetByTestId("processes-launch-plan-detail").WaitForAsync();
        await page.GetByTestId("processes-launch-plan-detail").ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "02-launch-plan-created-large-desktop.png")
        });

        var launchPlan = await WaitForLaunchPlanAsync(
            apiClient,
            definition.Id,
            launchName,
            plan => plan.Status == ProcessLaunchPlanStatus.Draft,
            30_000);
        await PostApiAsync(apiClient, $"/api/processes/launch-plans/{launchPlan.Id:D}/hr-match?requestedBy=sb015-playwright");
        await PostApiAsync(apiClient, $"/api/processes/launch-plans/{launchPlan.Id:D}/submit-approval?requestedBy=sb015-playwright");
        await PostJsonApiAsync(
            apiClient,
            "/api/processes/launch-plans/approval-decisions",
            new ProcessLaunchApprovalDecisionRequest {
                LaunchPlanId = launchPlan.Id,
                Status = ProcessLaunchApprovalStatus.Approved,
                ResolutionSummary = "SB015 large-screen process-start smoke approved the UI-created launch plan.",
                DecidedBy = "sb015-playwright"
            });
        await PostApiAsync(apiClient, $"/api/processes/launch-plans/{launchPlan.Id:D}/provision?requestedBy=sb015-playwright");
        launchPlan = await WaitForLaunchPlanAsync(
            apiClient,
            definition.Id,
            launchName,
            plan => plan.Id == launchPlan.Id && plan.Status == ProcessLaunchPlanStatus.Ready,
            30_000);

        response = await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={definition.Id:D}&launchPlanId={launchPlan.Id:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected ready launch route to return 2xx, got {(int)response.Status}.");
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
        await WaitForEnabledTestIdAsync(page, "processes-launch-execute-button", 30_000);
        await page.GetByTestId("processes-launch-execute-button").ClickAsync();
        await WaitForBodyTextAsync(page, "Launch plan executed into a process run.", 30_000);

        var run = await WaitForRunAsync(
            apiClient,
            definition.Id,
            launchName,
            30_000);
        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new() {
            Name = "Activity",
            Exact = true
        }).ClickAsync();
        var runHistoryItem = page.GetByTestId($"processes-run-history-item-{run.Id:D}");
        await runHistoryItem.WaitForAsync();
        await runHistoryItem.ClickAsync();
        var runStepsDialog = page.GetByTestId("processes-run-steps-dialog");
        await runStepsDialog.GetByTestId("processes-run-steps-dialog-step-list").WaitForAsync();
        await runStepsDialog.GetByRole(AriaRole.Button, new() {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await runStepsDialog.WaitForAsync(new() {
            State = WaitForSelectorState.Detached
        });
        await page.GetByTestId("processes-selected-run-summary").WaitForAsync();
        var selectedRunSummary = await page.GetByTestId("processes-selected-run-summary").InnerTextAsync();
        Assert.Contains(launchName, selectedRunSummary, StringComparison.Ordinal);
        Assert.Contains(run.TotalStepCount.ToString(), selectedRunSummary, StringComparison.Ordinal);
        await page.GetByTestId("processes-selected-run-summary").ScreenshotAsync(new() {
            Path = Path.Combine(artifactsDir, "03-run-selected-large-desktop.png")
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static HttpClient CreateProcessApiClient(string baseUrl) {
        return new HttpClient {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(45)
        };
    }

    private static async Task<ProcessDefinitionListItem> WaitForDefinitionAsync(
        HttpClient client,
        string definitionName,
        Func<ProcessDefinitionListItem, bool> predicate,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var definitions = await ReadRequiredJsonAsync<IReadOnlyList<ProcessDefinitionListItem>>(
                client,
                "/api/processes/definitions");
            var match = definitions
                .Where(definition => string.Equals(definition.Name, definitionName, StringComparison.Ordinal))
                .Where(predicate)
                .OrderByDescending(definition => definition.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for process definition '{definitionName}'.");
    }

    private static async Task<ProcessLaunchPlanListItem> WaitForLaunchPlanAsync(
        HttpClient client,
        Guid definitionId,
        string launchName,
        Func<ProcessLaunchPlanListItem, bool> predicate,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var plans = await ReadRequiredJsonAsync<IReadOnlyList<ProcessLaunchPlanListItem>>(
                client,
                $"/api/processes/launch-plans?definitionId={definitionId:D}&take=50");
            var match = plans
                .Where(plan => string.Equals(plan.Name, launchName, StringComparison.Ordinal))
                .Where(predicate)
                .OrderByDescending(plan => plan.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for launch plan '{launchName}'.");
    }

    private static async Task<ProcessRunListItem> WaitForRunAsync(
        HttpClient client,
        Guid definitionId,
        string runName,
        int timeoutMs) {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            var runs = await ReadRequiredJsonAsync<IReadOnlyList<ProcessRunListItem>>(
                client,
                $"/api/processes/runs?definitionId={definitionId:D}&take=50");
            var match = runs
                .Where(run => string.Equals(run.Name, runName, StringComparison.Ordinal))
                .OrderByDescending(run => run.UpdatedAtUtc)
                .FirstOrDefault();
            if (match is not null) {
                return match;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for process run '{runName}'.");
    }

    private static async Task WaitForEnabledTestIdAsync(IPage page, string testId, int timeoutMs) {
        var locator = page.GetByTestId(testId);
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt) {
            if (await locator.IsEnabledAsync()) {
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Timed out waiting for '{testId}' to become enabled.");
    }

    private static async Task<T> ReadRequiredJsonAsync<T>(HttpClient client, string requestUri) {
        using var response = await client.GetAsync(requestUri);
        await AssertApiSuccessAsync(response);
        var value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value;
    }

    private static async Task PostApiAsync(HttpClient client, string requestUri) {
        using var response = await client.PostAsync(requestUri, content: null);
        await AssertApiSuccessAsync(response);
    }

    private static async Task PostJsonApiAsync<T>(HttpClient client, string requestUri, T payload) {
        using var response = await client.PostAsJsonAsync(requestUri, payload);
        await AssertApiSuccessAsync(response);
    }

    private static async Task<T> PostJsonAndReadApiAsync<T>(HttpClient client, string requestUri, object payload) {
        using var response = await client.PostAsJsonAsync(requestUri, payload);
        await AssertApiSuccessAsync(response);
        var value = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(value);
        return value;
    }

    private static async Task AssertApiSuccessAsync(HttpResponseMessage response) {
        if (response.IsSuccessStatusCode) {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {body}");
    }

    private static ProcessDefinitionEditorModel BuildSb030RunDetailDefinition() {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        return new ProcessDefinitionEditorModel {
            Name = "SB030 run detail recovery proof",
            Summary = "Browser-visible run detail proof for blocked recovery and artifacts.",
            ValueStatement = "Expose durable blocked-step recovery and artifact evidence in the process run UI.",
            CustomerName = "Process runtime validation",
            OwnerName = "Playwright",
            InterfaceContractSummary = "The run detail route must display typed step status, recovery options, and artifact ledger readback.",
            GovernanceNotes = "Created only by local Playwright validation.",
            ChangeSummary = "Initial SB030 proof definition.",
            GovernancePolicySummary = "Use public process API routes and the large-desktop browser surface.",
            ConstitutionRuleSummary = "Do not replace runtime state with report-only proof.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe deterministic browser proof.",
            Roles =
            [
                new ProcessRoleEditorModel {
                    Id = roleId,
                    Key = "runtime-reviewer",
                    DisplayName = "Runtime reviewer",
                    Purpose = "Review blocked recovery evidence.",
                    StaffingIntent = "Deterministic local role for browser proof.",
                    PreferredExecutorKind = "person",
                    IsRequired = false,
                    AllowsFallback = false,
                    SnapshotSummary = "SB030 runtime reviewer."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel {
                    Id = stepId,
                    Key = "capture-recovery-evidence",
                    Title = "Capture blocked recovery evidence",
                    StepKind = ProcessStepKind.Work,
                    AllowedOperations =
                    [
                        ProcessStepOperation.ReadProcessContext,
                        ProcessStepOperation.WriteManagedProcessArtifacts,
                        ProcessStepOperation.CaptureRuntimeProof,
                        ProcessStepOperation.RecoverArtifactsOnly
                    ],
                    OperationTargetScope = ProcessStepTargetScope.ManagedProcessArtifactsOnly,
                    InputContractSummary = "The selected process run is loaded from durable storage.",
                    OutputContractSummary = "Blocked recovery evidence remains attached to the selected step run.",
                    EvidenceContractSummary = "The UI must render blocked state, recovery options, and artifact ledger readback.",
                    DecisionRightsSummary = "The local Playwright test controls the transition and artifact record.",
                    ExceptionPolicySummary = "Block if runtime evidence is absent.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Reviewer,
                            IsRequired = false,
                            RebindPolicySummary = "Keep local browser proof deterministic."
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel {
                            Id = Guid.NewGuid(),
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "SB030 blocked recovery evidence",
                            IsRequired = true,
                            TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired,
                            SensitivityLevel = ProcessSensitivityLevel.Internal,
                            AllowedFutureUsageSummary = "Regression validation for run detail recovery UI.",
                            ValidationRequirementSummary = "Must be visible in the run evidence ledger with a durable artifact record id."
                        }
                    ]
                }
            ]
        };
    }

    private sealed class ProcessRunDetailApiProof {
        public ProcessRunListItem? Run { get; set; }

        public List<ProcessStepRunViewModel> StepRuns { get; set; } = [];

        public List<ProcessArtifactViewModel> Artifacts { get; set; } = [];

        public List<ProcessOutboxRecordViewModel> OutboxRecords { get; set; } = [];

        public List<ProcessExecutionRunViewModel> ExecutionRuns { get; set; } = [];

        public ProcessRunHealthSummaryViewModel? Health { get; set; }
    }
}
