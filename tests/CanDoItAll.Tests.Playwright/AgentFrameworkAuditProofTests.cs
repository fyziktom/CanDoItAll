using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed partial class AgentFrameworkAuditProofTests
{
    private readonly PlaywrightAppFixture fixture;

    public AgentFrameworkAuditProofTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Agents_shell_route_renders_integrated_tabs_and_executes_sc04_through_the_scenario_harness()
    {
        await EnsureScenarioHarnessCatalogAsync();
        await using var context = await CreateContextAsync(1600, 900);
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/agents");

        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /agents to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("agents-shell-tabs").WaitForAsync();
        await ExpectTextContainsAsync(page.Locator("body"), "Integrated technical agent runtime");
        await page.GetByTestId("agents-shell-open-crmhr").WaitForAsync();
        await page.GetByTestId("agents-shell-open-processes").WaitForAsync();
        await page.GetByTestId("agents-shell-open-scenarios").WaitForAsync();
        await SaveFullPageScreenshotAsync(page, "sb10-agents-shell-desktop.png");

        await page.GetByTestId("agents-shell-open-scenarios").ClickAsync();
        await page.GetByTestId("scenario-card-sc04").WaitForAsync();
        await page.GetByTestId("scenario-card-sc04").ClickAsync();
        await page.GetByTestId("run-scenario-sc04").ClickAsync();
        await ExpectPageTextContainsAsync(page, "Waiting on approval", "scenario-refresh-button", attempts: 3, timeoutMsPerAttempt: 10_000);
        await WaitForTestIdWithRefreshAsync(page, "approve-scenario-sc04", "scenario-refresh-button", attempts: 3, timeoutMsPerAttempt: 10_000);
        await page.GetByTestId("approve-scenario-sc04").ClickAsync();
        await WaitForScenarioCompletionAsync("SC04", "approval-report.md");
        await page.GetByTestId("scenario-refresh-button").ClickAsync();
        await ExpectPageTextContainsAsync(page, "SC04 resumed after approval.", "scenario-refresh-button", attempts: 4, timeoutMsPerAttempt: 10_000);
        await page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions
        {
            Name = "Evidence",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("scenario-artifact-list").WaitForAsync();
        await ExpectTextContainsWithRefreshAsync(page, page.GetByTestId("scenario-artifact-list"), "approval-report.md", "scenario-refresh-button", attempts: 4, timeoutMsPerAttempt: 10_000);
        await SaveFullPageScreenshotAsync(page, "sb11-scenarios-sc04.png");

        await page.GetByTestId("agents-shell-tabs").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = "Providers",
            Exact = false
        }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions
        {
            Exact = true,
            Name = "Provider profiles"
        }).WaitForAsync();
        await ExpectTextContainsAsync(page.Locator("body"), "Scenario Harness Provider");
        await SaveFullPageScreenshotAsync(page, "sb04-provider-bridge.png");

        await page.GetByTestId("agents-shell-tabs").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = "Agents",
            Exact = false
        }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions
        {
            Exact = true,
            Name = "Technical agents"
        }).WaitForAsync();
        await ExpectTextContainsAsync(page.Locator("body"), "Scenario Harness Operator");

        await page.GetByTestId("agents-shell-tabs").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = "Governance",
            Exact = false
        }).ClickAsync();
        await WaitForTestIdWithRefreshAsync(page, "agents-governance-panel", refreshTestId: null, attempts: 2, timeoutMsPerAttempt: 15_000);
        await ExpectTextContainsAsync(page.Locator("body"), "approval-report.md");
        await SaveFullPageScreenshotAsync(page, "sb05-agent-catalog-governance.png");

        await page.GetByTestId("agents-shell-tabs").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = "Overview",
            Exact = false
        }).ClickAsync();
        await page.SetViewportSizeAsync(1100, 900);
        await page.GetByTestId("agents-shell-tabs").WaitForAsync();
        await SaveFullPageScreenshotAsync(page, "sb10-agents-shell-narrow.png");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Collaboration_seeded_thread_surfaces_inbox_detail_mark_read_and_mobile_layout()
    {
        var seed = await SeedCollaborationThreadAsync();
        await using var context = await CreateContextAsync(1600, 900);
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/collaboration?threadId={seed.ThreadId:D}");

        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /collaboration to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page);
        await page.WaitForSelectorAsync("text=Inbox, threads, and escalation workspace");
        await page.GetByTestId("collaboration-thread-title").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("collaboration-thread-title"), seed.Subject);
        await ExpectTextContainsAsync(page.Locator("[data-testid='collaboration-thread-message-item']").First, seed.MessageBody);
        await ExpectTextContainsAsync(page.Locator("body"), "1 unread");

        await page.GetByTestId("collaboration-mark-read").ClickAsync();
        await page.GetByTestId("collaboration-status-card").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("collaboration-status-card"), "Selected thread marked as read.");

        await page.SetViewportSizeAsync(390, 844);
        await page.GetByTestId("collaboration-thread-title").WaitForAsync();
        await ExpectTextContainsAsync(page.Locator("body"), seed.ContextLabel);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Processes_seeded_direct_message_flow_surfaces_transcript_and_denied_policy_evidence()
    {
        var seed = await SeedDirectMessagingRunAsync();
        await using var context = await CreateContextAsync(1600, 900);
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={seed.DefinitionId:D}&runId={seed.RunId:D}");

        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /processes to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page);
        await OpenRunsTabAsync(page);
        await page.GetByTestId("processes-direct-message-source-select").WaitForAsync();
        await ConfigureDirectMessageComposerAsync(
            page,
            seed.SourceRoleRequirementId,
            seed.TargetRoleRequirementId,
            "Allowed Playwright proof message from delivery lead to review lead.");
        await page.GetByTestId("processes-direct-message-send-button").ClickAsync();

        await page.GetByTestId("processes-direct-message-thread-card").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-direct-message-thread-card"), "Delivery lead");
        await ExpectTextContainsAsync(page.GetByTestId("processes-direct-message-thread-card"), "Allowed Playwright proof message from delivery lead to review lead.");
        var transcriptEntryCount = await page.GetByTestId("processes-direct-message-entry").CountAsync();

        await UpdateDirectMessagingPermissionAsync(seed.RunId, seed.TargetRoleRequirementId, "Review lead", allowsDirectMessaging: false);

        await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={seed.DefinitionId:D}&runId={seed.RunId:D}");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await OpenRunsTabAsync(page);
        await page.GetByTestId("processes-direct-message-source-select").WaitForAsync();
        await ExpectPageTextContainsAsync(page, "Review lead (messaging off)", refreshTestId: null, attempts: 3, timeoutMsPerAttempt: 5_000);
        await ConfigureDirectMessageComposerAsync(
            page,
            seed.SourceRoleRequirementId,
            seed.TargetRoleRequirementId,
            "Denied Playwright proof message after permission revocation.");
        await page.GetByTestId("processes-direct-message-send-button").ClickAsync();
        await WaitForDirectMessageDenialAsync(seed.RunId, "cannot receive direct messages");

        await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={seed.DefinitionId:D}&runId={seed.RunId:D}");
        await DismissStartupModalIfPresentAsync(page);
        await OpenRunsTabAsync(page);
        await ExpectPageTextContainsAsync(page, "DirectMessagingPolicy", refreshTestId: null, attempts: 3, timeoutMsPerAttempt: 5_000);
        Assert.Equal(transcriptEntryCount, await page.GetByTestId("processes-direct-message-entry").CountAsync());
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Processes_agent_recovery_run_surfaces_missing_artifact_deadletter_and_manual_rerun()
    {
        var seed = await SeedAgentRecoveryRunAsync();
        await using var context = await CreateContextAsync(1600, 900);
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={seed.DefinitionId:D}&runId={seed.RunId:D}");

        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /processes to return 2xx, got {(int)response.Status}.");

        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await page.GetByTestId("processes-detail-tabs").GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions
        {
            Name = "Runs",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-runs-tab-shell").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-selected-run-summary"), "One or more automation dispatch records are dead-lettered.");
        await ExpectTextContainsAsync(page.GetByTestId("processes-selected-run-summary"), "1 missing artifacts");
        await ExpectTextContainsAsync(page.GetByTestId("processes-selected-run-summary"), "1 dead-lettered");

        await OpenRunStepsDialogAsync(page, seed.RunId);
        var stepCard = page.GetByTestId("processes-step-run-card").Filter(new LocatorFilterOptions
        {
            HasText = seed.StepTitle
        });
        await ExpectTextContainsAsync(stepCard, "Blocked");
        await ExpectTextContainsAsync(stepCard, "1 dead-lettered");
        await ExpectTextContainsAsync(stepCard, "Automation dispatch is dead-lettered for this step.");
        await stepCard.GetByTestId("processes-rerun-agent-step-button").WaitForAsync();
        await CloseRunStepsDialogAsync(page);

        var runsTabs = page.GetByTestId("processes-runs-tabs");
        await runsTabs.GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions
        {
            Name = "Evidence",
            Exact = true
        }).ClickAsync();
        var artifactLedger = page.GetByTestId("processes-artifact-obligation-ledger");
        await ExpectTextContainsAsync(artifactLedger, seed.ArtifactTitle);
        await ExpectTextContainsAsync(artifactLedger, "Missing");
        await SaveFullPageScreenshotAsync(page, "sb12-agent-recovery-artifact-ledger.png");

        await runsTabs.GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions
        {
            Name = "Execution",
            Exact = true
        }).ClickAsync();
        var outboxLedger = page.GetByTestId("processes-automation-outbox-ledger");
        await ExpectTextContainsAsync(outboxLedger, "dispatch-run-automation");
        await ExpectTextContainsAsync(outboxLedger, "DeadLettered");
        await ExpectTextContainsAsync(outboxLedger, "Provider execution failed after retry exhaustion.");

        await runsTabs.GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions
        {
            Name = "Activity",
            Exact = true
        }).ClickAsync();
        await OpenRunStepsDialogAsync(page, seed.RunId);
        await stepCard.GetByTestId("processes-rerun-agent-step-button").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-message-card"), "Agent step rerun requested with a recovery directive.", 20_000);
        await ExpectStepCardContainsAsync(page, seed.StepTitle, "InProgress");
        await CloseRunStepsDialogAsync(page);

        await runsTabs.GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions
        {
            Name = "Execution",
            Exact = true
        }).ClickAsync();
        outboxLedger = page.GetByTestId("processes-automation-outbox-ledger");
        await ExpectTextContainsAsync(outboxLedger, "AGENT-STEP-RERUN");
        await SaveFullPageScreenshotAsync(page, "sb12-agent-recovery-rerun-outbox.png");
        await WriteProofMetadataAsync(
            "sb12-agent-recovery-metadata.md",
            $"# Agent Recovery Browser Proof{Environment.NewLine}{Environment.NewLine}- Project id: `{seed.ProjectId:D}`{Environment.NewLine}- Definition id: `{seed.DefinitionId:D}`{Environment.NewLine}- Run id: `{seed.RunId:D}`{Environment.NewLine}- Step: `{seed.StepTitle}`{Environment.NewLine}- Required artifact: `{seed.ArtifactTitle}`");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Processes_workflow_delivery_flow_runs_launch_approval_messaging_and_completion_end_to_end()
    {
        var seed = await SeedWorkflowDeliveryScenarioAsync();
        var launchName = $"SC11 Workflow launch {DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

        await using var context = await CreateContextAsync(1600, 900);
        var page = await context.NewPageAsync();
        var browserDiagnostics = new List<string>();
        page.Console += (_, message) =>
        {
            browserDiagnostics.Add($"console:{message.Type}:{message.Text}");
        };
        page.PageError += (_, errorText) =>
        {
            browserDiagnostics.Add($"pageerror:{errorText}");
        };
        page.Crash += (_, _) =>
        {
            browserDiagnostics.Add("pagecrash");
        };

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/agents?partyId={seed.BuilderPartyId:D}");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-agent-summary-provider").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-agent-summary-provider"), "Scenario Harness Provider");
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-agent-summary-owner"), seed.ManagerName);
        await SaveFullPageScreenshotAsync(page, "sb06-crmhr-agent-binding.png");

        await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={seed.DefinitionId:D}");
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions
        {
            Name = "SC11 workflow delivery process",
            Exact = true
        }).WaitForAsync();
        await ExpectPageTextContainsAsync(page, "SC11 workflow delivery process", refreshTestId: null, attempts: 4, timeoutMsPerAttempt: 10_000);
        await OpenRunsTabAsync(page);
        await page.GetByTestId("processes-launch-name-input").FillAsync(launchName);
        await page.GetByTestId("processes-create-launch-plan-button").ClickAsync();
        await WaitForTestIdWithRefreshAsync(page, "processes-launch-plan-detail", refreshTestId: null, attempts: 2, timeoutMsPerAttempt: 15_000);
        await SelectLaunchCandidateAsync(page, seed.BuilderRoleName, seed.BuilderAgentName);
        await SelectLaunchCandidateAsync(page, seed.ReviewerRoleName, seed.ReviewerAgentName);
        await SaveFullPageScreenshotAsync(page, "sb07-process-launch-planning.png");

        await RegisterDomClickProbeAsync(page, "processes-launch-submit-approval-button", "launch-submit");
        await page.GetByTestId("processes-launch-submit-approval-button").ClickAsync();
        try
        {
            await WaitForWorkspaceMessageAsync(page, "Launch plan submitted for approval.");
        }
        catch (TimeoutException exception)
        {
            var domClickCount = await ReadDomClickProbeAsync(page, "launch-submit");
            var reconnectState = await ReadReconnectStateAsync(page);
            var databaseState = ReadLaunchPlanDatabaseState(fixture.DatabaseConnectionString, launchName);
            var browserDiagnosticsSnapshot = browserDiagnostics.Count == 0
                ? "<none>"
                : string.Join(Environment.NewLine, browserDiagnostics);
            throw new TimeoutException(
                $"{exception.Message}{Environment.NewLine}DOM click probe: {domClickCount}{Environment.NewLine}Reconnect state: {reconnectState}{Environment.NewLine}{databaseState}{Environment.NewLine}Browser diagnostics:{Environment.NewLine}{browserDiagnosticsSnapshot}{Environment.NewLine}App log snapshot:{Environment.NewLine}{fixture.GetLogSnapshot()}",
                exception);
        }
        await WaitForTestIdWithRefreshAsync(page, "processes-launch-approval-record", refreshTestId: null, attempts: 3, timeoutMsPerAttempt: 15_000);
        await ExpectTextContainsAsync(page.GetByTestId("processes-launch-approval-record"), seed.ManagerName);
        await page.GetByTestId("processes-launch-approval-record")
            .GetByRole(AriaRole.Link, new LocatorGetByRoleOptions
            {
                Name = "Open collaboration thread",
                Exact = true
            })
            .ClickAsync();
        await page.GetByTestId("collaboration-thread-title").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("collaboration-thread-title"), launchName);
        await SaveFullPageScreenshotAsync(page, "sb08-launch-approval-thread.png");

        await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={seed.DefinitionId:D}");
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await OpenRunsTabAsync(page);
        await page.GetByTestId("processes-launch-decision-summary-input").FillAsync("Manager approval confirms the staffed workflow workflow.");
        await page.GetByTestId("processes-launch-decision-summary-input").BlurAsync();
        await page.GetByTestId("processes-launch-approve-button").ClickAsync();
        await WaitForButtonEnabledAsync(page, "processes-launch-execute-button");
        await page.GetByTestId("processes-launch-execute-button").ClickAsync();

        var reviewGateEvidence = await WaitForWorkflowReviewGateAsync(seed, launchName);
        await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={seed.DefinitionId:D}&runId={reviewGateEvidence.RunId:D}");
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await OpenRunsTabAsync(page);
        await OpenRunStepsDialogAsync(page, reviewGateEvidence.RunId);
        await ExpectStepCardContainsAsync(page, seed.GenerationStepTitle, "Completed");
        await ExpectStepCardContainsAsync(page, seed.HandoffStepTitle, "Ready");
        await CloseRunStepsDialogAsync(page);
        Assert.DoesNotContain("Unknown role", await page.Locator("body").InnerTextAsync(), StringComparison.Ordinal);

        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions
        {
            Name = "Coordination",
            Exact = true
        }).ClickAsync();
        await page.GetByTestId("processes-direct-message-source-select").WaitForAsync();
        await ConfigureDirectMessageComposerAsync(
            page,
            seed.BuilderRoleRequirementId,
            seed.ReviewerRoleRequirementId,
            "Workflow delivery is ready for reviewer validation after the generated build passed.");
        await page.GetByTestId("processes-direct-message-send-button").ClickAsync();
        await page.GetByTestId("processes-direct-message-thread-card").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-direct-message-thread-card"), seed.BuilderRoleName);
        await ExpectTextContainsAsync(page.GetByTestId("processes-direct-message-thread-card"), "Workflow delivery is ready for reviewer validation after the generated build passed.");
        await SaveFullPageScreenshotAsync(page, "sb11-workflow-direct-message.png");

        await OpenRunStepsDialogAsync(page, reviewGateEvidence.RunId);
        await SetStepRunStatusAsync(page, seed.HandoffStepTitle, "Start");
        await ExpectStepCardContainsAsync(page, seed.HandoffStepTitle, "InProgress");
        await SetStepRunStatusAsync(page, seed.HandoffStepTitle, "Complete");
        await CloseRunStepsDialogAsync(page);

        var finalEvidence = await WaitForWorkflowRunCompletionAsync(seed, launchName);
        await WriteProofMetadataAsync("sb11-workflow-run-metadata.md", BuildWorkflowEvidenceMarkdown(seed, launchName, finalEvidence));

        await page.GotoAsync($"{fixture.BaseUrl}/processes?processId={seed.DefinitionId:D}&runId={finalEvidence.RunId:D}");
        await page.GetByTestId("processes-detail-tabs").WaitForAsync();
        await OpenRunsTabAsync(page);
        await OpenRunStepsDialogAsync(page, finalEvidence.RunId);
        await ExpectStepCardContainsAsync(page, seed.ReviewStepTitle, "Completed");
        await CloseRunStepsDialogAsync(page);
        await ExpectTextContainsAsync(page.Locator("body"), "generation-report.md");
        await ExpectTextContainsAsync(page.Locator("body"), "review-report.md");
        await SaveFullPageScreenshotAsync(page, "sb09-execution-observability.png");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }
}
