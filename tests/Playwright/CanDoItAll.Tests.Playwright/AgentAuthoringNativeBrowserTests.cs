using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.McpTestHost;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;
using static CanDoItAll.Tests.Playwright.Smoke.ScriptedAgentUiFixture;
using CanDoItAll.Tests.Playwright.Smoke;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentAuthoringNativeBrowserTests {
    [Fact]
    public async Task Ui_definitions_real_setup_assignment_and_MCP_runtime_preserve_explicit_approval() {
        Assert.Equal(ToolInvocationClassification.LocalMcp, AgentToolPolicyCatalog.BuiltIn.Classify(AgentAuthoringSetupFixture.ToolName));
        await using var setup = await AgentAuthoringSetupFixture.StartAsync();
        await using var wire = await AgentResponseFixture.StartAsync(AgentAuthoringSetupFixture.ToolName);
        await using var host = await LiveUiHost.StartAsync();
        var marker = "CA1-" + Guid.NewGuid().ToString("N");
        var modelInputs = new System.Collections.Concurrent.ConcurrentQueue<JsonElement>();
        wire.HoldReply = false;
        wire.Steps = [
            input => Track(input, new([new(AgentAuthoringSetupFixture.ToolName, JsonSerializer.Serialize(new { value = marker + "-allowed" }))])),
            input => Track(input, new([], "Allowed fixture completed.")),
            input => Track(input, new([new(AgentAuthoringSetupFixture.ToolName, JsonSerializer.Serialize(new { value = marker + "-denied" }))])),
            input => Track(input, new([], "Operator denied the fixture call."))
        ];
        var agentId = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().SaveAgentAsync(new() {
            Name = "CA1 authoring agent " + marker, RoleTitle = "Owned capability proof", Status = AgentLifecycleStatus.Active,
            Instructions = "Use only the selected owned MCP fixture and wait for explicit approval.", SelectedCapabilityIds = []
        }));
        var unrelated = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().SaveAgentAsync(new() {
            Name = "CA1 unrelated agent " + marker, RoleTitle = "No capability assignment", SelectedCapabilityIds = []
        }));
        await ConfigureScriptedAgentAsync(host, agentId, wire.BaseUrl);
        var page = await host.NewPageAsync();
        try {
            await page.GotoAsync($"{host.BaseUrl}/agents?tab=capabilities&agentId={agentId:D}");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            var inline = await CreateAsync("skill", "inline", async () => {
                await page.GetByTestId("agents-capability-setup-skill-mode").SelectOptionAsync("Inline");
                await page.GetByTestId("agents-capability-setup-inline-instructions").FillAsync("Owned inline instructions Ω");
                await page.GetByTestId("agents-capability-setup-inline-resources").FillAsync("""[{"name":"notes.txt","content":"resource Ω","description":"owned"}]""");
            });
            using (var json = JsonDocument.Parse(inline.ConfigurationJson)) {
                Assert.Equal("resource Ω", json.RootElement.GetProperty("inlineSkill").GetProperty("resources")[0].GetProperty("content").GetString());
            }
            var card = CapabilityCard(inline.Name);
            await card.GetByRole(AriaRole.Button, new() { Name = "Details", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Tab, new() { Name = "Configuration", Exact = true }).ClickAsync();
            await page.GetByTestId("agents-capability-details-inline-instructions").FillAsync("Edited instructions Ω");
            await page.GetByTestId("agents-capability-details-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("capability-authoring-form")).ToHaveCountAsync(0);
            var edited = await ReadAsync(inline.Id);
            Assert.Contains("Edited instructions", edited.ConfigurationJson, StringComparison.Ordinal);
            Assert.Contains("notes.txt", edited.ConfigurationJson, StringComparison.Ordinal);
            await CreateAsync("skill", "upload", async () => {
                await page.GetByTestId("agents-capability-setup-skill-mode").SelectOptionAsync("Upload");
                await page.GetByTestId("agents-capability-setup-skill-file").SetInputFilesAsync(new FilePayload {
                    Name = "SKILL.md", MimeType = "text/markdown", Buffer = Encoding.UTF8.GetBytes("\uFEFF---\nname: ca1-upload\ndescription: Owned upload\n---\n# Upload Ω\nDo not execute scripts.")
                });
                await Assertions.Expect(page.GetByTestId("agents-capability-setup-inline-instructions")).ToHaveValueAsync(new System.Text.RegularExpressions.Regex("Upload Ω"));
            });
            var http = await CreateAsync("tool", "http", async () => {
                await page.GetByTestId("agents-capability-setup-tool-kind").SelectOptionAsync("externalHttp");
                await page.GetByTestId("agents-capability-setup-tool-http-endpoint").FillAsync(setup.BaseUrl + "/tool");
                await page.GetByTestId("agents-capability-setup-tool-http-output").FillAsync("ok");
                await page.GetByTestId("agents-capability-setup-tool-test-input").FillAsync("{invalid");
                await page.GetByTestId("agents-capability-setup-test").ClickAsync();
                await Assertions.Expect(page.GetByText("Setup failed", new() { Exact = true })).ToBeVisibleAsync();
                await Assertions.Expect(page.GetByTestId("agents-capability-setup-setup-diagnostics")).ToBeVisibleAsync();
                Assert.Equal(0, setup.HttpCalls);
                await page.GetByTestId("agents-capability-setup-tool-test-input").FillAsync("{}");
                await SetupAsync();
                Assert.Equal(1, setup.HttpCalls);
            });
            Assert.Equal(1, setup.HttpCalls);
            var helperRoot = Path.Combine(host.OwnedWorkspaceRoot, "ca1-owned-setup");
            Directory.CreateDirectory(helperRoot);
            var helperSource = Path.GetDirectoryName(typeof(McpTestHostMarker).Assembly.Location)!;
            foreach (var extension in new[] { ".dll", ".deps.json", ".runtimeconfig.json" }) {
                File.Copy(Path.Combine(helperSource, "CanDoItAll.McpTestHost" + extension), Path.Combine(helperRoot, "CanDoItAll.McpTestHost" + extension));
            }
            var helper = Path.Combine(helperRoot, "CanDoItAll.McpTestHost.dll");
            var processMarker = Path.Combine(helperRoot, "process.pid");
            await CreateAsync("tool", "process", async () => {
                await page.GetByTestId("agents-capability-setup-tool-process-command").FillAsync("dotnet");
                await page.GetByTestId("agents-capability-setup-tool-process-working-directory").FillAsync(helperRoot);
                await page.GetByTestId("agents-capability-setup-tool-process-arguments").FillAsync(ArgumentLines(helper, "--external-json", "--pid-file", processMarker, "", " spaced "));
                await page.GetByTestId("agents-capability-setup-tool-process-allowed-executables").FillAsync("dotnet");
                await page.GetByTestId("agents-capability-setup-tool-process-output").FillAsync("ok");
                await SetupAsync();
                Assert.True(File.Exists(processMarker));
            });
            var processWritten = File.GetLastWriteTimeUtc(processMarker);
            var stdioMarker = Path.Combine(helperRoot, "mcp.pid");
            await CreateAsync("mcp", "stdio", async () => {
                await page.GetByTestId("agents-capability-setup-mcp-server").FillAsync("ca1-stdio");
                await page.GetByTestId("agents-capability-setup-mcp-command").FillAsync("dotnet");
                await page.GetByTestId("agents-capability-setup-mcp-arguments").FillAsync(ArgumentLines(helper, "--content-length", "--pid-file", stdioMarker));
                await page.GetByTestId("agents-capability-setup-mcp-working-directory").FillAsync(helperRoot);
                await page.GetByTestId("agents-capability-setup-mcp-roots").FillAsync(helperRoot);
                await page.GetByTestId("agents-capability-setup-mcp-tools").FillAsync("echo");
                await SetupAsync();
                Assert.True(File.Exists(stdioMarker));
            });
            var remote = await CreateAsync("mcp", "remote", async () => {
                await page.GetByTestId("agents-capability-setup-mcp-server").FillAsync("ca1-remote");
                await page.GetByTestId("agents-capability-setup-mcp-tools").FillAsync(AgentAuthoringSetupFixture.ToolName);
                await page.GetByTestId("agents-capability-setup-mcp-approval").SelectOptionAsync("AlwaysRequire");
                await page.GetByTestId("agents-capability-setup-mcp-transport").SelectOptionAsync("logical");
                await page.GetByTestId("agents-capability-setup-test").ClickAsync();
                await Assertions.Expect(page.GetByText("Setup failed", new() { Exact = true })).ToBeVisibleAsync();
                Assert.Equal(0, setup.McpStarts);
                await page.GetByTestId("agents-capability-setup-mcp-transport").SelectOptionAsync("http");
                await page.GetByTestId("agents-capability-setup-mcp-endpoint").FillAsync(setup.BaseUrl + "/mcp");
                await SetupAsync();
                Assert.Equal(1, setup.McpStarts);
                Assert.Equal(1, setup.McpLists);
            });
            Assert.Equal(1, setup.McpStarts);
            Assert.Equal(1, setup.HttpCalls);
            Assert.Empty(setup.Invocations);
            Assert.Equal(processWritten, File.GetLastWriteTimeUtc(processMarker));
            foreach (var pidFile in new[] { processMarker, stdioMarker }) {
                var id = int.Parse(await File.ReadAllTextAsync(pidFile), System.Globalization.CultureInfo.InvariantCulture);
                Assert.Throws<ArgumentException>(() => Process.GetProcessById(id));
            }
            await CapabilityCard(remote.Name).GetByRole(AriaRole.Button, new() { Name = "Assign", Exact = true }).ClickAsync();
            await Assertions.Expect(CapabilityCard(remote.Name).GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true })).ToBeVisibleAsync();
            var assigned = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentEditorAsync(agentId));
            Assert.Contains(remote.Id, assigned.SelectedCapabilityIds);
            Assert.DoesNotContain(remote.Id, (await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentEditorAsync(unrelated))).SelectedCapabilityIds);
            await page.GotoAsync($"{host.BaseUrl}/agents?tab=chat&agentId={agentId:D}");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
            await page.GetByRole(AriaRole.Button, new() { Name = "New thread", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByText("This thread is ready for the first prompt", new() { Exact = true })).ToBeVisibleAsync();
            await page.GetByTestId("chat-prompt-input").WaitForAsync();
            foreach (var approve in new[] { true, false }) {
                await page.GetByTestId("chat-prompt-input").FillAsync(approve ? "Call the owned fixture with the allowed marker." : "Propose the owned fixture with the denied marker.");
                await page.GetByTestId("chat-prompt-input").PressAsync("Tab");
                await Assertions.Expect(page.GetByTestId("chat-send-button")).ToBeEnabledAsync();
                await page.GetByTestId("chat-send-button").ClickAsync();
                var approvals = page.Locator("[data-testid^='chat-approval-approve-']");
                await Assertions.Expect(approvals).ToHaveCountAsync(1, new() { Timeout = 60_000 });
                var pending = await host.SeedAsync(services => ReadLatestRunAsync(services, agentId));
                var proposal = Assert.Single(Proposals(pending));
                Assert.Equal(AgentAuthoringSetupFixture.ToolName, proposal.Payload.ToolName);
                Assert.Contains(marker + (approve ? "-allowed" : "-denied"), JsonSerializer.Serialize(proposal.Payload), StringComparison.Ordinal);
                Assert.Equal(approve ? 0 : 1, setup.Invocations.Count);
                await page.GetByTestId($"chat-approval-{(approve ? "approve" : "reject")}-{proposal.ApprovalId}").ClickAsync();
                await Assertions.Expect(page.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Completed", new() { Timeout = 60_000 });
                var terminal = await host.SeedAsync(services => ReadLatestRunAsync(services, agentId));
                Assert.Equal(pending.Run.Id, terminal.Run.Id);
                if (approve) {
                    var completed = Assert.Single(Proposals(terminal));
                    Assert.Equal(AgentToolProposalState.Completed, completed.State);
                    Assert.Equal(ExecutionApprovalStatus.Approved, completed.ApprovalStatus);
                    Assert.NotNull(completed.Result);
                    await File.WriteAllTextAsync(host.Artifact("ca1-native-mcp-result-private.json"), completed.Result.PayloadJson);
                    Assert.Equal(AgentToolProtocolEnvelope.ComputeDigest(completed.Result.PayloadJson), completed.Result.Digest);
                    Assert.Equal(marker + "-allowed", Assert.Single(setup.Invocations).GetProperty("value").GetString());
                    await File.WriteAllTextAsync(host.Artifact("ca1-native-mcp-journal.json"), JsonSerializer.Serialize(new {
                        Run = terminal.Run.Id, completed.State, completed.ApprovalStatus, completed.Payload.ToolName,
                        ResultFormat = completed.Result.Format, ResultDigest = completed.Result.Digest, completed.EffectState
                    }));
                } else {
                    Assert.Contains(Proposals(terminal), item => item.ApprovalStatus == ExecutionApprovalStatus.Rejected);
                }
                Assert.Single(setup.Invocations);
            }
            Assert.Equal(marker + "-allowed", setup.Invocations.Single().GetProperty("value").GetString());
            Assert.Equal(4, wire.Requests);
            await page.ScreenshotAsync(new() { Path = host.Artifact("ca1-native-approved-denied.png") });
            await File.WriteAllTextAsync(host.Artifact("ca1-native-authoring.json"), JsonSerializer.Serialize(new {
                agentId, unrelated, Inline = inline.Id, Http = http.Id, Mcp = remote.Id, setup.HttpCalls,
                setup.McpStarts, setup.McpLists, McpInvocations = setup.Invocations.Count, ModelRequests = wire.Requests,
                Viewport = "1920x1080@1", ProcessMarkers = new[] { Path.GetFileName(processMarker), Path.GetFileName(stdioMarker) }
            }));
        } catch (Exception exception) {
            await File.WriteAllTextAsync(host.Artifact("ca1-native-model-input-private.json"), JsonSerializer.Serialize(modelInputs));
            await File.WriteAllTextAsync(host.Artifact("ca1-native-effect-counts.json"), JsonSerializer.Serialize(new {
                setup.HttpCalls, setup.McpStarts, setup.McpLists, Calls = setup.Invocations.Count, wire.Requests
            }));
            await File.WriteAllTextAsync(host.Artifact("ca1-native-failure.txt"), exception.ToString());
            await File.WriteAllTextAsync(host.Artifact("ca1-native-page.txt"), await page.Locator("body").InnerTextAsync());
            await host.CaptureHostLogAsync();
            try {
                await page.ScreenshotAsync(new() { Path = host.Artifact("ca1-native-failure.png"), Timeout = 5_000 });
            } catch (Exception screenshotException) {
                await File.WriteAllTextAsync(host.Artifact("ca1-native-screenshot-failure.txt"), screenshotException.ToString());
            }
            throw;
        }

        ILocator CapabilityCard(string name) => page.GetByTestId("agents-capability-card").Filter(new() { HasTextString = name });
        AgentResponseFixture.ScriptedTurn Track(JsonElement input, AgentResponseFixture.ScriptedTurn turn) {
            modelInputs.Enqueue(input.Clone());
            return turn;
        }
        Task<CapabilityEditorModel> ReadAsync(Guid id) => host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetCapabilityEditorAsync(id));
        string ArgumentLines(params string[] arguments) => string.Join("\n", arguments.Select(value => JsonSerializer.Serialize(value)));
        async Task SetupAsync() {
            await page.GetByTestId("agents-capability-setup-test").ClickAsync();
            await Assertions.Expect(page.GetByText("Setup passed", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        async Task<CapabilityCatalogItem> CreateAsync(string kind, string suffix, Func<Task> configure) {
            var name = marker + " " + suffix;
            await page.GetByTestId("agents-capability-new-" + kind).ClickAsync();
            await page.GetByTestId("agents-capability-setup-name").FillAsync(name);
            await page.GetByTestId("agents-capability-setup-next").ClickAsync();
            await configure();
            await page.GetByTestId("agents-capability-setup-next").ClickAsync();
            await page.GetByTestId("agents-capability-setup-create").ClickAsync();
            await Assertions.Expect(page.GetByTestId("capability-authoring-form")).ToHaveCountAsync(0);
            return await host.SeedAsync(async services => Assert.Single(await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListCapabilitiesAsync(), item => item.Name == name));
        }
    }
}
