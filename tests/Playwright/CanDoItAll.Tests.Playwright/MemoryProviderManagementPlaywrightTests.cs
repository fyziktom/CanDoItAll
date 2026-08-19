using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Flows;

[Collection(PlaywrightCollection.Name)]
public sealed class MemoryProviderManagementPlaywrightTests
{
    [Fact]
    public async Task MemoryProviderManagement_RendersZeroAndDemoProviderStates()
    {
        await using var host = await MemoryProviderManagementPlaywrightHost.CreateAsync();
        await using var browser = await host.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 1000
            }
        });

        var screenshotRoot = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "codex",
            "bundles",
            "candoitall-memory-provider-extraction-bundle",
            "proof",
            "regression",
            "screenshots");
        Directory.CreateDirectory(screenshotRoot);

        var page = await context.NewPageAsync();
        try
        {
            var response = await page.GotoAsync($"{host.BaseUrl}/memory");
            Assert.True(response?.Ok, $"Expected /memory to load. Logs:{Environment.NewLine}{host.GetLogSnapshot()}");

            await WaitForVisibleWithDialogDismissalAsync(page, "memory-ui-zero-provider");
            await ExpectTextAsync(page, "No memory providers are configured");
            await Assertions.Expect(page.GetByText("Cognitive Memory", new PageGetByTextOptions { Exact = false })).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-zero-provider-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-query", "memory-ui-query");
            await ExpectTextAsync(page, "No provider is selected. Provider-backed actions are disabled.");
            await Assertions.Expect(page.GetByTestId("memory-ui-query-submit")).ToBeDisabledAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-query-no-provider-desktop.png"),
                FullPage = true
            });

            await page.SetViewportSizeAsync(390, 900);
            await SelectTabAsync(page, "memory-ui-tab-providers", "memory-ui-zero-provider");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-zero-provider-mobile.png"),
                FullPage = true
            });

            await page.SetViewportSizeAsync(1440, 1000);
            await page.GetByTestId("memory-ui-add-demo-providers").ClickAsync();
            await WaitForVisibleWithDialogDismissalAsync(page, "memory-ui-provider-list");
            await ExpectTextAsync(page, "Business demo memory");
            await ExpectTextAsync(page, "Programming demo memory");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-provider-list-desktop.png"),
                FullPage = true
            });

            await page.GetByTestId("memory-provider-provider-programming-demo").ClickAsync();
            await page.GetByTestId("memory-ui-provider-detail").WaitForAsync();
            await ExpectTextAsync(page, "Programming demo memory");
            await ExpectTextAsync(page, "Degraded");
            await ExpectTextAsync(page, "context.query.sync");
            await Assertions.Expect(page.GetByText("context.query.async", new PageGetByTextOptions { Exact = true })).Not.ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("operations.status", new PageGetByTextOptions { Exact = true })).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-provider-detail-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-query", "memory-ui-query");
            await ExpectTextAsync(page, "Selected provider health is not healthy.");
            await Assertions.Expect(page.GetByTestId("memory-ui-query-submit")).ToBeDisabledAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-provider-error-state-desktop.png"),
                FullPage = true
            });
        }
        catch
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-failure-state.png"),
                FullPage = true
            });
            throw;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task MemoryProviderOperations_RunsQueryAndBlocksUnsupportedMutations()
    {
        await using var host = await MemoryProviderManagementPlaywrightHost.CreateAsync();
        await using var browser = await host.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 1000
            }
        });

        var screenshotRoot = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "codex",
            "bundles",
            "candoitall-memory-provider-extraction-bundle",
            "proof",
            "regression",
            "screenshots");
        Directory.CreateDirectory(screenshotRoot);

        var page = await context.NewPageAsync();
        try
        {
            var response = await page.GotoAsync($"{host.BaseUrl}/memory");
            Assert.True(response?.Ok, $"Expected /memory to load. Logs:{Environment.NewLine}{host.GetLogSnapshot()}");

            await WaitForVisibleWithDialogDismissalAsync(page, "memory-ui-zero-provider");
            await page.GetByTestId("memory-ui-editor-instance-id").FillAsync("provider.regression-browser");
            await page.GetByTestId("memory-ui-editor-display-name").FillAsync("regression browser memory");
            await page.GetByTestId("memory-ui-editor-health").SelectOptionAsync("Healthy");
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-immediate-feedback")).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-snapshot-ingestion")).ToBeDisabledAsync();
            await page.GetByTestId("memory-ui-save-provider").ClickAsync();

            await WaitForVisibleWithDialogDismissalAsync(page, "memory-ui-provider-list");
            await ExpectTextAsync(page, "regression browser memory");

            await SelectTabAsync(page, "memory-ui-tab-query", "memory-ui-query");
            await page.GetByTestId("memory-ui-query-text").FillAsync("contract source references");
            await page.GetByTestId("memory-ui-query-submit").ClickAsync();
            await ExpectTextAsync(page, "Mock memory context for contract source references");
            await ExpectTextAsync(page, "Deterministic mock memory");
            await ExpectTextAsync(page, "Project 1");
            await Assertions.Expect(page.GetByTestId("memory-ui-feedback-submit")).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-query-context-pack-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-ingestion", "memory-ui-ingestion");
            await ExpectTextAsync(page, "Ingestion unavailable");
            await Assertions.Expect(page.GetByTestId("memory-ui-ingestion-submit")).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-unsupported-mutations-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-operations", "memory-ui-operations");
            await ExpectTextAsync(page, "ContextQuery");
            await ExpectTextAsync(page, "Completed");
            await ExpectTextAsync(page, "context.query.sync");
            await Assertions.Expect(page.GetByTestId("memory-ui-cancel-operation")).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-operations-ledger-desktop.png"),
                FullPage = true
            });

            await page.SetViewportSizeAsync(390, 900);
            await SelectTabAsync(page, "memory-ui-tab-query", "memory-ui-query");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-query-context-pack-mobile.png"),
                FullPage = true
            });
        }
        catch
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-operations-failure-state.png"),
                FullPage = true
            });
            throw;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task MemoryProviderSurfaces_RendersRclIframeAndRejectsUnsafeUrl()
    {
        await using var host = await MemoryProviderManagementPlaywrightHost.CreateAsync();
        await using var browser = await host.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 1000
            }
        });

        var screenshotRoot = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "codex",
            "bundles",
            "candoitall-memory-provider-extraction-bundle",
            "proof",
            "regression",
            "screenshots");
        Directory.CreateDirectory(screenshotRoot);

        var page = await context.NewPageAsync();
        try
        {
            var response = await page.GotoAsync($"{host.BaseUrl}/memory");
            Assert.True(response?.Ok, $"Expected /memory to load. Logs:{Environment.NewLine}{host.GetLogSnapshot()}");

            await WaitForVisibleWithDialogDismissalAsync(page, "memory-ui-zero-provider");
            await page.GetByTestId("memory-ui-editor-instance-id").FillAsync("provider.regression-browser");
            await page.GetByTestId("memory-ui-editor-display-name").FillAsync("regression browser memory");
            await page.GetByTestId("memory-ui-editor-health").SelectOptionAsync("Healthy");
            await page.GetByTestId("memory-ui-editor-rcl").SetCheckedAsync(true);
            await page.GetByTestId("memory-ui-editor-iframe").SetCheckedAsync(true);
            await page.GetByTestId("memory-ui-editor-provider-ui-url").FillAsync("https://memory.example.test/console");
            await page.GetByTestId("memory-ui-save-provider").ClickAsync();

            await WaitForVisibleWithDialogDismissalAsync(page, "memory-ui-provider-list");
            await ExpectTextAsync(page, "regression browser memory");

            await SelectTabAsync(page, "memory-ui-tab-provider-ui", "memory-ui-provider-ui");
            await ExpectTextAsync(page, "Provider panel");
            await ExpectTextAsync(page, "Mock provider panel");
            await ExpectTextAsync(page, "memory.mock.panel");
            await ExpectTextAsync(page, "Provider console");
            await Assertions.Expect(page.GetByTestId("memory-ui-provider-iframe"))
                .ToHaveAttributeAsync("src", "https://memory.example.test/console");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-provider-rcl-iframe-desktop.png"),
                FullPage = true
            });

            await page.SetViewportSizeAsync(390, 900);
            await SelectTabAsync(page, "memory-ui-tab-provider-ui", "memory-ui-provider-ui");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-provider-ui-mobile.png"),
                FullPage = true
            });

            await page.SetViewportSizeAsync(1440, 1000);
            await SelectTabAsync(page, "memory-ui-tab-providers", "memory-ui-provider-detail");
            await page.GetByTestId("memory-ui-editor-provider-ui-url").FillAsync("javascript:alert(1)");
            await page.GetByTestId("memory-ui-save-provider").ClickAsync();
            await ExpectTextAsync(page, "Provider UI URL must use HTTPS or loopback HTTP.");
            await Assertions.Expect(page.GetByText("javascript:alert", new PageGetByTextOptions { Exact = false })).Not.ToBeVisibleAsync();
            await SelectTabAsync(page, "memory-ui-tab-provider-ui", "memory-ui-provider-ui");
            await ExpectTextAsync(page, "Mock provider panel");
            await Assertions.Expect(page.GetByTestId("memory-ui-provider-iframe"))
                .ToHaveAttributeAsync("src", "https://memory.example.test/console");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-provider-ui-fallback-desktop.png"),
                FullPage = true
            });
        }
        catch
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-provider-ui-failure-state.png"),
                FullPage = true
            });
            throw;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    [Fact]
    public async Task MemoryUiCheckpoint_CoversGenericUiAndProviderSurfaceFlows()
    {
        await using var host = await MemoryProviderManagementPlaywrightHost.CreateAsync();
        await using var browser = await host.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 1000
            }
        });

        var screenshotRoot = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "codex",
            "bundles",
            "candoitall-memory-provider-extraction-bundle",
            "proof",
            "regression",
            "screenshots");
        Directory.CreateDirectory(screenshotRoot);

        var page = await context.NewPageAsync();
        try
        {
            var response = await page.GotoAsync($"{host.BaseUrl}/memory");
            Assert.True(response?.Ok, $"Expected /memory to load. Logs:{Environment.NewLine}{host.GetLogSnapshot()}");

            await WaitForVisibleWithDialogDismissalAsync(page, "memory-ui-zero-provider");
            await ExpectTextAsync(page, "No memory providers are configured");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-zero-provider-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-query", "memory-ui-query");
            await ExpectTextAsync(page, "No provider is selected. Provider-backed actions are disabled.");
            await Assertions.Expect(page.GetByTestId("memory-ui-query-submit")).ToBeDisabledAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-zero-provider-query-disabled-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-providers", "memory-ui-zero-provider");
            await page.GetByTestId("memory-ui-editor-instance-id").FillAsync("provider.regression-browser");
            await page.GetByTestId("memory-ui-editor-display-name").FillAsync("regression checkpoint memory");
            await page.GetByTestId("memory-ui-editor-health").SelectOptionAsync("Healthy");
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-async-query")).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-immediate-feedback")).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-snapshot-ingestion")).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-operation-status")).ToBeDisabledAsync();
            await page.GetByTestId("memory-ui-editor-rcl").SetCheckedAsync(true);
            await page.GetByTestId("memory-ui-editor-iframe").SetCheckedAsync(true);
            await page.GetByTestId("memory-ui-editor-provider-ui-url").FillAsync("https://memory.example.test/console");
            await page.GetByTestId("memory-ui-save-provider").ClickAsync();

            await WaitForVisibleWithDialogDismissalAsync(page, "memory-ui-provider-list");
            await ExpectTextAsync(page, "regression checkpoint memory");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-provider-list-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-query", "memory-ui-query");
            await page.GetByTestId("memory-ui-query-text").FillAsync("checkpoint source references");
            await page.GetByTestId("memory-ui-query-submit").ClickAsync();
            await ExpectTextAsync(page, "Mock memory context for checkpoint source references");
            await ExpectTextAsync(page, "Deterministic mock memory");
            await Assertions.Expect(page.GetByTestId("memory-ui-feedback-submit")).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-query-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-ingestion", "memory-ui-ingestion");
            await ExpectTextAsync(page, "Ingestion unavailable");
            await Assertions.Expect(page.GetByTestId("memory-ui-ingestion-submit")).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-mutations-disabled-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-operations", "memory-ui-operations");
            await ExpectTextAsync(page, "ContextQuery");
            await ExpectTextAsync(page, "context.query.sync");
            await Assertions.Expect(page.GetByTestId("memory-ui-cancel-operation")).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-operations-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-provider-ui", "memory-ui-provider-ui");
            await ExpectTextAsync(page, "Provider panel");
            await ExpectTextAsync(page, "Mock provider panel");
            await Assertions.Expect(page.GetByTestId("memory-ui-provider-iframe"))
                .ToHaveAttributeAsync("src", "https://memory.example.test/console");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-provider-ui-desktop.png"),
                FullPage = true
            });

            await page.SetViewportSizeAsync(390, 900);
            await SelectTabAsync(page, "memory-ui-tab-provider-ui", "memory-ui-provider-ui");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-provider-ui-mobile.png"),
                FullPage = true
            });

            await page.SetViewportSizeAsync(1440, 1000);
            await SelectTabAsync(page, "memory-ui-tab-providers", "memory-ui-provider-detail");
            await page.GetByTestId("memory-ui-editor-instance-id").FillAsync("provider.regression-fallback");
            await page.GetByTestId("memory-ui-editor-display-name").FillAsync("regression fallback provider");
            await page.GetByTestId("memory-ui-save-provider").ClickAsync();
            await WaitForVisibleWithDialogDismissalAsync(page, "memory-ui-provider-list");
            await ExpectTextAsync(page, "regression fallback provider");

            await page.GetByTestId("memory-ui-editor-provider-ui-url").FillAsync("javascript:alert(1)");
            await page.GetByTestId("memory-ui-save-provider").ClickAsync();
            await ExpectTextAsync(page, "Provider UI URL must use HTTPS or loopback HTTP.");
            await SelectTabAsync(page, "memory-ui-tab-provider-ui", "memory-ui-provider-ui");
            await ExpectTextAsync(page, "Mock provider panel");
            await Assertions.Expect(page.GetByTestId("memory-ui-provider-iframe"))
                .ToHaveAttributeAsync("src", "https://memory.example.test/console");
            await Assertions.Expect(page.GetByText("javascript:alert", new PageGetByTextOptions { Exact = false })).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-provider-ui-fallback-desktop.png"),
                FullPage = true
            });

            await SelectTabAsync(page, "memory-ui-tab-providers", "memory-ui-provider-detail");
            await page.GetByTestId("memory-provider-provider-regression-browser").ClickAsync();
            await ExpectTextAsync(page, "regression checkpoint memory");
            await SelectTabAsync(page, "memory-ui-tab-provider-ui", "memory-ui-provider-ui");
            await ExpectTextAsync(page, "Mock provider panel");
        }
        catch
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(screenshotRoot, "memory-ui-checkpoint-failure-state.png"),
                FullPage = true
            });
            throw;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task ExpectTextAsync(IPage page, string text)
        => await Assertions.Expect(page.GetByText(text, new PageGetByTextOptions { Exact = false }).First).ToBeVisibleAsync();

    private static async Task DismissDatabaseProfileDialogAsync(IPage page)
    {
        var heading = page.GetByText("Database profiles", new PageGetByTextOptions { Exact = true });
        if (!await IsVisibleAsync(heading, 500))
        {
            return;
        }

        var continueButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
        {
            Name = "Continue",
            Exact = true
        });
        if (await IsVisibleAsync(continueButton, 500))
        {
            await continueButton.ClickAsync();
        }
        else
        {
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
            {
                Name = "Close",
                Exact = false
            }).First.ClickAsync();
        }

        await heading.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = 5_000
        });
    }

    private static async Task WaitForVisibleWithDialogDismissalAsync(IPage page, string testId)
    {
        var locator = page.GetByTestId(testId);
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await DismissDatabaseProfileDialogAsync(page);
            if (await IsVisibleAsync(locator, 500))
            {
                return;
            }

            await page.WaitForTimeoutAsync(250);
        }

        await locator.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 1_000
        });
    }

    private static async Task SelectTabAsync(IPage page, string tabTestId, string panelTestId)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                var tab = page.GetByTestId(tabTestId);
                await tab.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 5_000
                });
                await tab.ClickAsync(new LocatorClickOptions
                {
                    Force = true,
                    Timeout = 5_000
                });

                if (await IsVisibleAsync(page.GetByTestId(panelTestId), 1_000))
                {
                    return;
                }
            }
            catch (Exception exception) when (attempt < 7 && exception is PlaywrightException or TimeoutException)
            {
            }

            await page.WaitForTimeoutAsync(250);
        }

        await page.GetByTestId(panelTestId).WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5_000
        });
    }

    private static async Task<bool> IsVisibleAsync(ILocator locator, float timeout)
    {
        try
        {
            await locator.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeout
            });
            return true;
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            return false;
        }
    }
}

internal sealed class MemoryProviderManagementPlaywrightHost : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> logs;
    private readonly Process process;
    private readonly Task stdoutPump;
    private readonly Task stderrPump;
    private readonly string runtimeRoot;

    private MemoryProviderManagementPlaywrightHost(
        string baseUrl,
        IPlaywright playwright,
        Process process,
        Task stdoutPump,
        Task stderrPump,
        ConcurrentQueue<string> logs,
        string runtimeRoot)
    {
        BaseUrl = baseUrl;
        Playwright = playwright;
        this.process = process;
        this.stdoutPump = stdoutPump;
        this.stderrPump = stderrPump;
        this.logs = logs;
        this.runtimeRoot = runtimeRoot;
    }

    public string BaseUrl { get; }

    public IPlaywright Playwright { get; }

    public static async Task<MemoryProviderManagementPlaywrightHost> CreateAsync()
    {
        var baseUrl = ResolveBaseUrl();
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "candoitall-memory-ui-playwright", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtimeRoot);

        var processStartInfo = new ProcessStartInfo(
            "dotnet",
            PlaywrightTestHostPaths.BuildDotnetRunArguments("src/App/CanDoItAll.Web", baseUrl))
        {
            WorkingDirectory = PlaywrightTestHostPaths.RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ConfigureEnvironment(processStartInfo, runtimeRoot);

        var logs = new ConcurrentQueue<string>();
        var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start CanDoItAll.Web for memory provider Playwright tests.");
        var stdoutPump = PumpAsync(process.StandardOutput, logs);
        var stderrPump = PumpAsync(process.StandardError, logs);

        await WaitForRuntimeReadyAsync(baseUrl, process, logs);

        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        return new MemoryProviderManagementPlaywrightHost(
            baseUrl,
            playwright,
            process,
            stdoutPump,
            stderrPump,
            logs,
            runtimeRoot);
    }

    public string GetLogSnapshot(int maxLines = 200)
    {
        return string.Join(
            Environment.NewLine,
            logs.Reverse().Take(maxLines).Reverse());
    }

    public async ValueTask DisposeAsync()
    {
        Playwright.Dispose();

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        await stdoutPump;
        await stderrPump;

        try
        {
            Directory.Delete(runtimeRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void ConfigureEnvironment(ProcessStartInfo processStartInfo, string runtimeRoot)
    {
        processStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        processStartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        processStartInfo.Environment["Database__Provider"] = "InMemory";
        processStartInfo.Environment["Database__ConnectionString"] = $"memory-ui-regression-{Guid.NewGuid():N}";
        processStartInfo.Environment["Storage__WorkspaceRoot"] = Path.Combine(runtimeRoot, "workspace");
        processStartInfo.Environment["ControlPlane__RootPath"] = Path.Combine(runtimeRoot, "control-plane");
        processStartInfo.Environment["DevelopmentManager__TuningModeEnabled"] = "false";
        processStartInfo.Environment["Rag__Qdrant__Enabled"] = "false";
        processStartInfo.Environment["Memory__Providers__DeterministicMock__Enabled"] = "true";
        processStartInfo.Environment["Workflows__ExampleSeed__Enabled"] = "false";
        processStartInfo.Environment["Workflows__ExampleSeed__SeedSampleWorkspaceFiles"] = "false";
        processStartInfo.Environment["Processes__Runtime__RequirePostgreSqlForAgentAutomation"] = "false";
        processStartInfo.Environment[LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey.Replace(":", "__", StringComparison.Ordinal)] =
            LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind;
    }

    private static async Task PumpAsync(StreamReader reader, ConcurrentQueue<string> logs)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            logs.Enqueue(line);
        }
    }

    private static async Task WaitForRuntimeReadyAsync(
        string baseUrl,
        Process process,
        ConcurrentQueue<string> logs)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(2);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The web app exited before becoming ready.{Environment.NewLine}{BuildLogSnapshot(logs)}");
            }

            if (await IsRuntimeReadyAsync(baseUrl, TimeSpan.FromSeconds(2)))
            {
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"Timed out waiting for runtime readiness.{Environment.NewLine}{BuildLogSnapshot(logs)}");
    }

    private static async Task<bool> IsRuntimeReadyAsync(string baseUrl, TimeSpan timeout)
    {
        using var handler = new HttpClientHandler();
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
            string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler)
        {
            Timeout = timeout
        };

        try
        {
            var payload = await client.GetStringAsync($"{baseUrl}/_dev/runtime");
            return payload.Contains("\"isReady\":true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildLogSnapshot(ConcurrentQueue<string> logs)
    {
        return string.Join(
            Environment.NewLine,
            logs.Reverse().Take(200).Reverse());
    }

    private static string ResolveBaseUrl()
    {
        var configuredBaseUrl = Environment.GetEnvironmentVariable("CANDOITALL_MEMORY_UI_PLAYWRIGHT_BASEURL");
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.TrimEnd('/');
        }

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return $"http://127.0.0.1:{port}";
    }
}
