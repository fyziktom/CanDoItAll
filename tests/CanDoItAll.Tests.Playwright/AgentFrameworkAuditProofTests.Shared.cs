using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AgentFrameworkAuditProofTests
{
    private const string BundleReviewsRoot = @"C:\repositories\CanDoItAll\agentframework-full-integration\reviews";
    private const string BundleArtifactsRoot = @"C:\repositories\CanDoItAll\agentframework-full-integration\reviews\artifacts";

    private async Task<IBrowserContext> CreateContextAsync(int width, int height)
    {
        return await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = width,
                Height = height
            }
        });
    }

    private static async Task OpenRunsTabAsync(IPage page)
    {
        var runsTab = page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions
        {
            Name = "Runs",
            Exact = true
        });

        TimeoutException? lastException = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await runsTab.ClickAsync();
            try
            {
                await WaitForTestIdWithRefreshAsync(page, "processes-launch-name-input", refreshTestId: null, attempts: 1, timeoutMsPerAttempt: 10_000);
                return;
            }
            catch (TimeoutException exception) when (attempt < 3)
            {
                lastException = exception;
                await Task.Delay(500);
            }
        }

        if (lastException is not null)
        {
            var bodyText = await page.Locator("body").InnerTextAsync();
            throw new TimeoutException($"{lastException.Message}{Environment.NewLine}{BuildBodySnapshot(bodyText)}", lastException);
        }
    }

    private static async Task OpenRunStepsDialogAsync(IPage page, Guid runId)
    {
        await page.GetByTestId("processes-runs-tabs").GetByRole(AriaRole.Tab, new LocatorGetByRoleOptions
        {
            Name = "Activity",
            Exact = true
        }).ClickAsync();

        var runHistoryItem = page.GetByTestId($"processes-run-history-item-{runId:D}");
        await runHistoryItem.WaitForAsync();
        await runHistoryItem.ClickAsync();
        await page.GetByTestId("processes-run-steps-dialog-step-list").WaitForAsync();
    }

    private static async Task CloseRunStepsDialogAsync(IPage page)
    {
        var dialog = page.GetByTestId("processes-run-steps-dialog");
        if (await dialog.CountAsync() == 0)
        {
            return;
        }

        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = "Close",
            Exact = true
        }).ClickAsync();
        await dialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });
    }

    private static async Task ConfigureDirectMessageComposerAsync(
        IPage page,
        Guid sourceRoleRequirementId,
        Guid targetRoleRequirementId,
        string messageBody)
    {
        var sourceSelect = page.GetByTestId("processes-direct-message-source-select");

        await sourceSelect.SelectOptionAsync(sourceRoleRequirementId.ToString("D"));
        var targetSelect = page.GetByTestId("processes-direct-message-target-select");
        await targetSelect.SelectOptionAsync(targetRoleRequirementId.ToString("D"));
        var bodyInput = page.GetByTestId("processes-direct-message-body-input");
        await bodyInput.FillAsync(messageBody);
        await bodyInput.EvaluateAsync("element => element.dispatchEvent(new Event('change', { bubbles: true }))");
        await bodyInput.BlurAsync();

        try
        {
            await page.WaitForFunctionAsync(
                @"() => {
                    const button = document.querySelector('[data-testid=""processes-direct-message-send-button""]');
                    return button instanceof HTMLButtonElement && button.disabled === false;
                }");
        }
        catch (TimeoutException exception)
        {
            var snapshot = await page.EvaluateAsync<string>(
                @"() => {
                    const source = document.querySelector('[data-testid=""processes-direct-message-source-select""]');
                    const target = document.querySelector('[data-testid=""processes-direct-message-target-select""]');
                    const body = document.querySelector('[data-testid=""processes-direct-message-body-input""]');
                    const button = document.querySelector('[data-testid=""processes-direct-message-send-button""]');
                    const sourceValue = source instanceof HTMLSelectElement ? source.value : '<missing>';
                    const targetValue = target instanceof HTMLSelectElement ? target.value : '<missing>';
                    const bodyValue = body instanceof HTMLTextAreaElement ? body.value : '<missing>';
                    const disabled = button instanceof HTMLButtonElement ? button.disabled.toString() : '<missing>';
                    return `source=${sourceValue}; target=${targetValue}; bodyLength=${bodyValue.length}; disabled=${disabled}`;
                }");
            var bodyText = await page.Locator("body").InnerTextAsync();
            throw new TimeoutException($"{exception.Message}{Environment.NewLine}{snapshot}{Environment.NewLine}{BuildBodySnapshot(bodyText)}", exception);
        }
    }

    private static async Task SelectLaunchCandidateAsync(
        IPage page,
        string roleDisplayName,
        string candidateDisplayName)
    {
        var roleCard = page.GetByTestId("processes-launch-role-card").Filter(new LocatorFilterOptions
        {
            HasText = roleDisplayName
        });
        await roleCard.WaitForAsync();
        await roleCard.GetByRole(AriaRole.Button).Filter(new LocatorFilterOptions
        {
            HasText = candidateDisplayName
        }).First.ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-message-card"), "Launch candidate selected.");
        await roleCard.WaitForAsync();
    }

    private static async Task WaitForWorkspaceMessageAsync(
        IPage page,
        string expectedValue,
        int timeoutMs = 20_000)
    {
        try
        {
            await ExpectTextContainsAsync(page.GetByTestId("processes-message-card"), expectedValue, timeoutMs);
        }
        catch (TimeoutException exception)
        {
            var bodyText = await page.Locator("body").InnerTextAsync();
            var messageCard = page.GetByTestId("processes-message-card");
            var messageCardText = await messageCard.CountAsync() > 0
                ? await messageCard.InnerTextAsync()
                : "<missing>";
            var submitDisabled = await page.GetByTestId("processes-launch-submit-approval-button").IsDisabledAsync();
            var approveDisabled = await page.GetByTestId("processes-launch-approve-button").IsDisabledAsync();
            var blazorErrorVisible = await page.Locator("#blazor-error-ui").IsVisibleAsync();
            var submitHitTarget = await page.EvaluateAsync<string>(
                @"() => {
                    const button = document.querySelector('[data-testid=""processes-launch-submit-approval-button""]');
                    if (!(button instanceof HTMLElement)) {
                        return '<missing>';
                    }

                    const rect = button.getBoundingClientRect();
                    const hit = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
                    if (!(hit instanceof HTMLElement)) {
                        return '<none>';
                    }

                    const testId = hit.getAttribute('data-testid') ?? '<no-testid>';
                    const text = (hit.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 120);
                    return `${hit.tagName.toLowerCase()} data-testid=${testId} text=${text}`;
                }");
            throw new TimeoutException(
                $"Timed out waiting for workspace message '{expectedValue}'.{Environment.NewLine}Message card: {messageCardText}{Environment.NewLine}Submit disabled: {submitDisabled}; approve disabled: {approveDisabled}; blazor error visible: {blazorErrorVisible}{Environment.NewLine}Submit hit target: {submitHitTarget}{Environment.NewLine}{BuildBodySnapshot(bodyText)}",
                exception);
        }
    }

    private static async Task RegisterDomClickProbeAsync(IPage page, string testId, string probeName)
    {
        await page.EvaluateAsync(
            @"([testId, probeName]) => {
                window.__codexButtonClickProbes ??= {};
                window.__codexButtonClickProbes[probeName] = 0;
                const button = document.querySelector(`[data-testid=""${testId}""]`);
                if (button instanceof HTMLElement) {
                    button.addEventListener('click', () => {
                        window.__codexButtonClickProbes[probeName] += 1;
                    });
                }
            }",
            new object[] { testId, probeName });
    }

    private static async Task<int> ReadDomClickProbeAsync(IPage page, string probeName)
    {
        return await page.EvaluateAsync<int>(
            @"probeName => {
                const probes = window.__codexButtonClickProbes ?? {};
                return Number.isFinite(probes[probeName]) ? probes[probeName] : 0;
            }",
            probeName);
    }

    private static async Task<string> ReadReconnectStateAsync(IPage page)
    {
        return await page.EvaluateAsync<string>(
            @"() => {
                const reconnect = document.getElementById('components-reconnect-modal');
                if (!(reconnect instanceof HTMLElement)) {
                    return '<missing>';
                }

                const classes = reconnect.className || '<none>';
                const ariaHidden = reconnect.getAttribute('aria-hidden') ?? '<missing>';
                const text = (reconnect.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 160);
                return `classes=${classes}; aria-hidden=${ariaHidden}; text=${text}`;
            }");
    }

    private static string ReadLaunchPlanDatabaseSnapshot(string? connectionString, string launchName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "Database snapshot: <no connection string>";
        }

        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    plan.Name,
                    plan.Status,
                    plan.SubmittedAtUtc,
                    plan.ApprovedAtUtc,
                    plan.ExecutedAtUtc,
                    (
                        SELECT COUNT(*)
                        FROM ProcessLaunchApprovalRecords approval
                        WHERE approval.LaunchPlanId = plan.Id
                    ) AS ApprovalCount,
                    (
                        SELECT COUNT(*)
                        FROM CollaborationThreadRecords thread
                        WHERE thread.ContextId = plan.Id
                    ) AS ThreadCount
                FROM ProcessLaunchPlans plan
                WHERE plan.Name = $name
                ORDER BY plan.UpdatedAtUtc DESC
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$name", launchName);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return $"Database snapshot: no launch plan found for '{launchName}'. Connection={connectionString}";
            }

            var submittedAt = reader.IsDBNull(2) ? "<null>" : reader.GetString(2);
            var approvedAt = reader.IsDBNull(3) ? "<null>" : reader.GetString(3);
            var executedAt = reader.IsDBNull(4) ? "<null>" : reader.GetString(4);
            return
                $"Database snapshot: status={reader.GetString(1)}; submittedAt={submittedAt}; approvedAt={approvedAt}; executedAt={executedAt}; approvals={reader.GetInt32(5)}; threads={reader.GetInt32(6)}; connection={connectionString}";
        }
        catch (Exception exception)
        {
            return $"Database snapshot failed: {exception.GetType().Name}: {exception.Message}. Connection={connectionString}";
        }
    }

    private static async Task SetStepRunStatusAsync(
        IPage page,
        string stepTitle,
        string buttonName)
    {
        var stepCard = page.GetByTestId("processes-step-run-card").Filter(new LocatorFilterOptions
        {
            HasText = stepTitle
        });
        await stepCard.WaitForAsync();
        await stepCard.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
        {
            Name = buttonName,
            Exact = true
        }).ClickAsync();
    }

    private static async Task ExpectStepCardContainsAsync(
        IPage page,
        string stepTitle,
        string expectedValue,
        int timeoutMs = 30_000)
    {
        var stepCard = page.GetByTestId("processes-step-run-card").Filter(new LocatorFilterOptions
        {
            HasText = stepTitle
        });
        await stepCard.WaitForAsync();
        await ExpectTextContainsAsync(stepCard, expectedValue, timeoutMs);
    }

    private static async Task WaitForButtonEnabledAsync(
        IPage page,
        string testId,
        int timeoutMs = 20_000)
    {
        var button = page.GetByTestId(testId);
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (!await button.IsDisabledAsync())
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for button '{testId}' to become enabled.");
    }

    private static async Task WaitForTestIdWithRefreshAsync(
        IPage page,
        string testId,
        string? refreshTestId,
        int attempts = 3,
        int timeoutMsPerAttempt = 10_000)
    {
        var locator = page.GetByTestId(testId);
        TimeoutException? lastException = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await locator.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = timeoutMsPerAttempt
                });
                return;
            }
            catch (TimeoutException exception)
            {
                lastException = exception;
                if (attempt >= attempts - 1)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(refreshTestId))
                {
                    await page.GetByTestId(refreshTestId).ClickAsync();
                }
                else
                {
                    await Task.Delay(500);
                }
            }
        }

        var bodyText = await page.Locator("body").InnerTextAsync();
        throw new TimeoutException(
            $"Timed out waiting for test id '{testId}' after refresh attempts.{Environment.NewLine}{BuildBodySnapshot(bodyText)}",
            lastException);
    }

    private static async Task ExpectTextContainsAsync(ILocator locator, string expectedValue, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if ((await locator.InnerTextAsync()).Contains(expectedValue, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for text '{expectedValue}'.");
    }

    private static async Task ExpectPageTextContainsAsync(
        IPage page,
        string expectedValue,
        string? refreshTestId = null,
        int attempts = 3,
        int timeoutMsPerAttempt = 10_000)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await ExpectTextContainsAsync(page.Locator("body"), expectedValue, timeoutMsPerAttempt);
                return;
            }
            catch (TimeoutException) when (attempt < attempts - 1)
            {
                if (!string.IsNullOrWhiteSpace(refreshTestId))
                {
                    await page.GetByTestId(refreshTestId).ClickAsync();
                }
                else
                {
                    await Task.Delay(500);
                }
            }
        }

        var bodyText = await page.Locator("body").InnerTextAsync();
        throw new TimeoutException($"Timed out waiting for text '{expectedValue}' after refresh attempts.{Environment.NewLine}{BuildBodySnapshot(bodyText)}");
    }

    private static async Task ExpectTextContainsWithRefreshAsync(
        IPage page,
        ILocator locator,
        string expectedValue,
        string? refreshTestId = null,
        int attempts = 3,
        int timeoutMsPerAttempt = 10_000)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                await ExpectTextContainsAsync(locator, expectedValue, timeoutMsPerAttempt);
                return;
            }
            catch (TimeoutException) when (attempt < attempts - 1)
            {
                if (!string.IsNullOrWhiteSpace(refreshTestId))
                {
                    await page.GetByTestId(refreshTestId).ClickAsync();
                }
                else
                {
                    await Task.Delay(500);
                }
            }
        }

        var bodyText = await page.Locator("body").InnerTextAsync();
        throw new TimeoutException($"Timed out waiting for text '{expectedValue}' after refresh attempts.{Environment.NewLine}{BuildBodySnapshot(bodyText)}");
    }

    private static string BuildBodySnapshot(string bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return "Body snapshot: <empty>";
        }

        var normalized = bodyText.ReplaceLineEndings(" ").Trim();
        if (normalized.Length > 1_600)
        {
            normalized = normalized[..1_600] + "...";
        }

        return $"Body snapshot: {normalized}";
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 1_500)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        try
        {
            await startupDialog.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });
    }

    private static async Task SaveFullPageScreenshotAsync(IPage page, string fileName)
    {
        Directory.CreateDirectory(BundleArtifactsRoot);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(BundleArtifactsRoot, fileName),
            FullPage = true
        });
    }

    private static async Task WriteProofMetadataAsync(string fileName, string content)
    {
        Directory.CreateDirectory(BundleArtifactsRoot);
        await File.WriteAllTextAsync(Path.Combine(BundleArtifactsRoot, fileName), content, Encoding.UTF8);
    }
}
