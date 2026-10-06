using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;
using Xunit.Sdk;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class AppSmokeTests {
    [Fact]
    [Trait("Category", "LiveProcess")]
    [Trait("Category", "HostPlatform")]
    public async Task WB5_native_runtime_preview_close_reopen_and_exact_stop_preserve_an_independent_process() {
        if (!OperatingSystem.IsWindows()) {
            throw SkipException.ForSkip("This explicit Windows native runtime journey requires its owned Windows host lane.");
        }
        Assert.NotNull(fixture.StorageWorkspaceRoot);
        Assert.NotNull(fixture.DatabaseConnectionString);
        var output = Environment.GetEnvironmentVariable("CANDOITALL_PLAYWRIGHT_EVIDENCE_ROOT")
            ?? Path.Combine(GetRepoRoot(), "output", "playwright", "wb5-runtime");
        Directory.CreateDirectory(output);
        var root = Path.Combine(fixture.StorageWorkspaceRoot!, "wb5-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        var url = $"http://127.0.0.1:{port}/";
        var marker = "WB5 owned runtime " + Guid.NewGuid().ToString("N");
        var receiptPath = Path.Combine(root, "processes.json");
        var projectPath = Path.Combine(root, "OwnedRuntime.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "Program.cs"), $$"""
            using System.Diagnostics;
            using System.Text.Json;
            if (args.Contains("--owned-child")) {
                await Task.Delay(TimeSpan.FromMinutes(5));
                return;
            }
            var childStart = new ProcessStartInfo(Environment.ProcessPath!) {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            childStart.ArgumentList.Add("--owned-child");
            using var child = Process.Start(childStart)!;
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.MapGet("/", () => {{JsonSerializer.Serialize(marker)}});
            app.Lifetime.ApplicationStarted.Register(() => File.WriteAllText({{JsonSerializer.Serialize(receiptPath)}},
                JsonSerializer.Serialize(new { ParentId = Environment.ProcessId, ChildId = child.Id })));
            app.Run({{JsonSerializer.Serialize(url)}});
            """);
        var canaryStart = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-NoProfile", "-Command", "Start-Sleep -Seconds 300" }) {
            canaryStart.ArgumentList.Add(argument);
        }
        using var canary = Process.Start(canaryStart) ?? throw new InvalidOperationException("Owned independent canary did not start.");
        await using var context = await fixture.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var page = await context.NewPageAsync();
        var failures = new List<string>();
        page.PageError += (_, error) => failures.Add(error);
        try {
            var projectResponse = await context.APIRequest.PostAsync($"{fixture.BaseUrl}/_dev/projects?name=WB5%20owned%20runtime&phase=Execution");
            Assert.True(projectResponse.Ok);
            var projectId = JsonNode.Parse(await projectResponse.TextAsync())!["projectId"]!.GetValue<Guid>();
            var scriptId = await CreateRuntimeNodeAsync(context, projectId, ProjectObjectType.Script, "powershell",
                "WB5 explicit script", "One launch", "Reviewed fixture writes one bounded output line.", new() {
                    Script = new() { ScriptKind = ProjectScriptKind.PowerShell, Command = "Write-Output 'WB5 approved native script'", WorkingDirectory = root }
                });
            var nodeId = await CreateRuntimeNodeAsync(context, projectId, ProjectObjectType.Environment, "dotnet-runtime",
                "WB5 owned server", "Native lifetime", "Reviewed fixture serves static bytes and owns one sleeping descendant.", new() {
                    Environment = new() { EnvironmentKind = ProjectEnvironmentKind.DotNetRuntime, ProjectPath = projectPath,
                        WorkingDirectory = root, LocalhostUrl = url }
                });
            await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/structure");
            await ProjectFilesUiJourney.ReadyFileCanvasAsync(page);
            await OpenNodeQuickActionsAsync(page, SelectorForNodeId(scriptId));
            await page.GetByTestId("project-structure-quick-action-runtime-open").ClickAsync();
            await page.GetByTestId("project-structure-runtime-launch-approval-cancel").ClickAsync();
            await Assertions.Expect(page.GetByTestId("project-structure-runtime-launch-approval-dialog")).Not.ToBeVisibleAsync();
            await OpenNodeQuickActionsAsync(page, SelectorForNodeId(scriptId));
            await page.GetByTestId("project-structure-quick-action-runtime-open").ClickAsync();
            await Assertions.Expect(page.GetByTestId("project-structure-runtime-launch-approval-dialog")).ToContainTextAsync("WB5 approved native script");
            await page.GetByTestId("project-structure-runtime-launch-approval-confirm").ClickAsync();
            await Assertions.Expect(page.GetByTestId("project-structure-runtime-launch-approval-dialog")).Not.ToBeVisibleAsync();
            await OpenNodeQuickActionsAsync(page, SelectorForNodeId(nodeId));
            await page.GetByTestId("project-structure-quick-action-runtime-open").ClickAsync();
            var preview = page.GetByTestId("project-structure-web-preview-dialog");
            await preview.WaitForAsync(new() { Timeout = 120_000 });
            await Assertions.Expect(page.FrameLocator("[data-testid='project-structure-web-preview-frame'] iframe").Locator("body"))
                .ToContainTextAsync(marker);
            var label = await preview.GetByTestId("operator-preview-process").InnerTextAsync();
            var acceptedPid = int.Parse(Regex.Match(label, @"Owned process (\d+)", RegexOptions.CultureInvariant).Groups[1].Value);
            using var accepted = Process.GetProcessById(acceptedPid);
            var childReceipt = JsonNode.Parse(await File.ReadAllTextAsync(receiptPath))!;
            using var parent = Process.GetProcessById(childReceipt["ParentId"]!.GetValue<int>());
            using var child = Process.GetProcessById(childReceipt["ChildId"]!.GetValue<int>());
            var identities = new[] { accepted, parent, child, canary }.Select(process => new { process.Id, StartedAtUtc = process.StartTime.ToUniversalTime() }).ToArray();
            await page.ScreenshotAsync(new() { Path = Path.Combine(output, "wb5-native-runtime-serving.png") });
            await preview.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            Assert.False(accepted.HasExited);
            Assert.False(parent.HasExited);
            Assert.False(child.HasExited);
            using var client = new HttpClient();
            Assert.Equal(marker, await client.GetStringAsync(url));
            await page.ReloadAsync();
            await ProjectFilesUiJourney.ReadyFileCanvasAsync(page);
            await DoubleClickCanvasNodeAsync(page, SelectorForNodeId(nodeId));
            await preview.WaitForAsync();
            Assert.Equal(label, await preview.GetByTestId("operator-preview-process").InnerTextAsync());
            await preview.GetByTestId("project-structure-web-preview-stop").ClickAsync();
            await Assertions.Expect(preview).Not.ToBeVisibleAsync(new() { Timeout = 30_000 });
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await Task.WhenAll(accepted.WaitForExitAsync(deadline.Token), parent.WaitForExitAsync(deadline.Token), child.WaitForExitAsync(deadline.Token));
            Assert.False(canary.HasExited);
            Assert.Empty(failures);
            await File.WriteAllTextAsync(Path.Combine(output, "wb5-native-runtime.json"), JsonSerializer.Serialize(new {
                projectId, scriptId, nodeId, url, marker, identities, Label = label, ClosedWithoutStop = true,
                SameIdentityAfterPageReload = true, DescendantsExited = true, CanaryUntouched = true,
                Viewport = new { Width = 1920, Height = 1080, Dpr = 1 },
                Qualification = "Browser observes native PID/start identity; full process-boundary identity comparison is independently covered at the original registry. No terminal or elevation was invoked."
            }, new JsonSerializerOptions { WriteIndented = true }));
        } finally {
            await File.WriteAllTextAsync(Path.Combine(output, "wb5-native-runtime-host.log"), fixture.GetLogSnapshot(600));
            if (!canary.HasExited) {
                canary.Kill(entireProcessTree: true);
                await canary.WaitForExitAsync();
            }
        }
    }
}
