using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_AC1_floating_handles_detach_follow_next_turn_and_close_preserve_pending_approval() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var profile = await ImportedResponsesAsync(fixture);
        var marker = "AC1_CONTEXT_" + Guid.NewGuid().ToString("N");
        var projectName = "AC1 context " + marker;
        var projectId = await fixture.PostAsync<Guid>("api/projects", new ProjectEditorModel { Name = projectName });
        var node = await fixture.PostAsync<ProjectStructureNodeSummary>($"api/project-structure/projects/{projectId:D}/nodes",
            new ProjectStructureNodeCreateInput(ProjectObjectType.ProjectBlock, "Original selected node", "", "", $"project:{projectId:D}", X: 450, Y: 220));
        var model = profile.ModelCatalog.First(item => item.Id != profile.DefaultModel && profile.SuggestedModels.Contains(item.Id));
        var firstAgent = await CreateFileAgentAsync(fixture, profile, model.Id, marker + "_A", projectId, projectName);
        var secondAgent = await CreateFileAgentAsync(fixture, profile, model.Id, marker + "_B", projectId, projectName);
        var firstConfiguration = await fixture.GetAsync($"api/agents/{firstAgent:D}");
        var secondConfiguration = await fixture.GetAsync($"api/agents/{secondAgent:D}");
        await fixture.EvidenceAsync("ac1-context-original-configurations", new { projectId, firstAgent, secondAgent, firstConfiguration, secondConfiguration });
        var page = fixture.Page;
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(page);
        await SelectFileNodeAsync(page, node.Id, node.Title);
        var chat = await OpenFloatingChatFromCatalogAsync(page, "project-structure-agents-toggle", firstAgent,
            Path.Combine(fixture.Settings.Evidence, "ac1-context-catalog-failure.png"));
        var firstHandle = await HandleAsync();
        var first = await TurnAsync(firstAgent, marker + "_A", "Original context");
        AssertContext(first);
        await chat.GetByTestId("floating-agent-chat-affinity-toggle").ClickAsync();
        await Assertions.Expect(chat.GetByTestId("floating-agent-chat-affinity-status")).ToHaveTextAsync("Detached from application context");
        var detached = await TurnAsync(firstAgent, marker + "_A", "Detached context");
        Assert.NotEqual(AgentChatTrustedSourceKinds.ProjectStructure, detached.GetProperty("sourceKind").GetString());
        Assert.Empty(detached.GetProperty("snapshots").EnumerateArray());
        var detachedDetail = await fixture.GetAsync($"api/agents/{firstAgent:D}/execution-runs/{detached.GetProperty("runId").GetGuid():D}");
        var firstSession = detachedDetail.GetProperty("run").GetProperty("chatSessionId").GetGuid();

        var catalog = page.GetByTestId("floating-agent-catalog-window");
        await catalog.GetByRole(AriaRole.Tab, new() { Name = "Available", Exact = true }).ClickAsync();
        await catalog.GetByTestId($"floating-agent-chat-agent-list-new-chat-{secondAgent:N}").ClickAsync();
        await Assertions.Expect(page.GetByTestId("floating-agent-chat-window")).ToHaveAttributeAsync("aria-label", "Chat with PP2C file agent " + marker + "_B");
        var secondHandle = await HandleAsync();
        Assert.NotEqual(firstHandle, secondHandle);
        await chat.GetByTestId("chat-prompt-input").WaitForAsync();
        await fixture.ScriptAsync(model.DisplayName, marker + "_B",
            ToolsStep(Call(ToolContractCatalog.WorkspaceWriteFile, new { path = "ac1/" + marker + "/denied.txt", content = "Uncommitted", overwrite = false })));
        await SendAsync(chat, "Propose a new non-overwriting file and await my decision.");
        await Assertions.Expect(chat.Locator("[data-testid^='chat-approval-approve-']")).ToHaveCountAsync(1, new() { Timeout = 60_000 });
        var pending = await fixture.ReadAgentAsync(secondAgent);
        AssertContext(pending);
        var pendingRunId = pending.GetProperty("runId").GetGuid();
        var proposal = Assert.Single(pending.GetProperty("proposals").EnumerateArray());
        Assert.Equal((int)ExecutionApprovalStatus.Pending, proposal.GetProperty("approvalStatus").GetInt32());
        await fixture.AssertScriptCompleteAsync(1);
        await page.GetByTestId("floating-agent-chat-window").GetByRole(AriaRole.Button, new() { Name = "Hide window", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("floating-agent-chat-close-confirmation")).ToContainTextAsync("does not reject the durable pending approval");
        await page.GetByTestId("floating-agent-chat-stop").ClickAsync();
        await Assertions.Expect(page.GetByTestId("floating-agent-chat-window")).ToHaveCountAsync(0);
        var afterClose = await fixture.ReadAgentAsync(secondAgent);
        Assert.Equal(pending.GetRawText(), afterClose.GetRawText());
        await fixture.EvidenceAsync("ac1-context-after-stop", new { firstAgent, secondAgent, firstHandle, secondHandle, Pending = pending, AfterClose = afterClose });
        await catalog.GetByRole(AriaRole.Tab, new() { Name = "Active", Exact = true }).ClickAsync();
        var firstItem = catalog.GetByTestId("floating-agent-chat-active-list-agent-chat:" + firstHandle);
        await Assertions.Expect(firstItem).ToBeVisibleAsync();
        await Assertions.Expect(catalog.GetByTestId("floating-agent-chat-active-list-agent-chat:" + secondHandle)).ToHaveCountAsync(0);
        await firstItem.GetByRole(AriaRole.Button, new() { Name = "Open", Exact = true }).ClickAsync();
        Assert.Equal(firstHandle, await HandleAsync());
        await Assertions.Expect(chat).ToContainTextAsync("Detached context " + marker + "_A");
        await Assertions.Expect(chat.GetByTestId("floating-agent-chat-affinity-status")).ToHaveTextAsync("Detached from application context");
        await chat.GetByTestId("floating-agent-chat-affinity-toggle").ClickAsync();
        await TurnAsync(firstAgent, marker + "_A", "Follow restored");
        var drag = await page.GetByTestId("floating-agent-chat-window").Locator("[data-cda-overlay-drag='true']").BoundingBoxAsync();
        Assert.NotNull(drag);
        await page.Mouse.MoveAsync(drag.X + 60, drag.Y + 12);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(drag.X + 60, drag.Y + 252, new() { Steps = 12 });
        await page.Mouse.UpAsync();
        await Assertions.Expect(page.GetByTestId("floating-agent-chat-window")).ToHaveCSSAsync("top", new System.Text.RegularExpressions.Regex("^2[0-9]{2}px$"));
        await page.GetByRole(AriaRole.Tab, new() { Name = "Gantt", Exact = true }).ClickAsync();
        await Assertions.Expect(chat.GetByTestId("floating-agent-chat-pending-context")).ToContainTextAsync("gantt", new() { IgnoreCase = true });
        var followed = await TurnAsync(firstAgent, marker + "_A", "New view accepted");
        Assert.Equal(projectId.ToString("D"), followed.GetProperty("sourceId").GetString());
        var followedDetail = await fixture.GetAsync($"api/agents/{firstAgent:D}/execution-runs/{followed.GetProperty("runId").GetGuid():D}");
        Assert.Equal(firstSession, followedDetail.GetProperty("run").GetProperty("chatSessionId").GetGuid());
        await Assertions.Expect(chat.GetByTestId("floating-agent-chat-affinity-status")).ToContainTextAsync("gantt", new() { IgnoreCase = true });
        await Assertions.Expect(chat.GetByTestId("floating-agent-chat-pending-context")).ToHaveCountAsync(0);
        await fixture.ScreenshotAsync("ac1-context-followed-1920");

        await catalog.GetByRole(AriaRole.Tab, new() { Name = "Available", Exact = true }).ClickAsync();
        await catalog.GetByTestId($"floating-agent-chat-agent-list-history-{secondAgent:N}").ClickAsync();
        await Assertions.Expect(page.GetByTestId("agent-thread-history-row")).ToHaveCountAsync(1);
        await page.GetByTestId("agent-thread-history-row").ClickAsync();
        await Assertions.Expect(chat.GetByTestId("chat-approval-reject-" + proposal.GetProperty("approvalId").GetString())).ToBeVisibleAsync();
        await fixture.ScriptAsync(model.DisplayName, marker + "_B", TextStep("Explicit rejection preserved " + marker));
        await chat.GetByTestId("chat-approval-reject-" + proposal.GetProperty("approvalId").GetString()).ClickAsync();
        await Assertions.Expect(chat).ToContainTextAsync("Explicit rejection preserved " + marker, new() { Timeout = 60_000 });
        await fixture.AssertScriptCompleteAsync(1);
        var rejected = await fixture.ReadAgentAsync(secondAgent);
        Assert.Equal(pendingRunId, rejected.GetProperty("runId").GetGuid());
        Assert.All(rejected.GetProperty("proposals").EnumerateArray(), item => Assert.Equal((int)ExecutionApprovalStatus.Rejected, item.GetProperty("approvalStatus").GetInt32()));
        Assert.DoesNotContain(rejected.GetProperty("receipts").EnumerateArray(), item => item.GetProperty("invocationOutcome").GetInt32() == (int)AgentToolInvocationOutcome.Succeeded);
        Assert.Equal(firstConfiguration.GetRawText(), (await fixture.GetAsync($"api/agents/{firstAgent:D}")).GetRawText());
        Assert.Equal(secondConfiguration.GetRawText(), (await fixture.GetAsync($"api/agents/{secondAgent:D}")).GetRawText());
        await fixture.EvidenceAsync("ac1-floating-context-close", new { projectId, node.Id, firstAgent, secondAgent, firstHandle, secondHandle,
            First = first, Detached = detached, Followed = followed, Pending = pending, AfterClose = afterClose, Rejected = rejected });

        async Task<string> HandleAsync() {
            var id = await page.GetByTestId("floating-agent-chat-window").GetAttributeAsync("data-cda-overlay-window");
            Assert.StartsWith("floating-agent-chat-", id);
            return id!["floating-agent-chat-".Length..];
        }

        async Task<JsonElement> TurnAsync(Guid agent, string scriptMarker, string answer) {
            await fixture.ScriptAsync(model.DisplayName, scriptMarker, TextStep(answer + " " + scriptMarker));
            await SendAsync(chat, answer + " request");
            await Assertions.Expect(chat).ToContainTextAsync(answer + " " + scriptMarker, new() { Timeout = 60_000 });
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Completed");
            await fixture.AssertScriptCompleteAsync(1);
            var observation = await fixture.ReadAgentAsync(agent);
            await fixture.EvidenceAsync("ac1-context-turn-" + observation.GetProperty("runId").GetGuid().ToString("N"), observation);
            return observation;
        }

        void AssertContext(JsonElement result) {
            Assert.Equal(AgentChatTrustedSourceKinds.ProjectStructure, result.GetProperty("sourceKind").GetString());
            Assert.Equal(projectId.ToString("D"), result.GetProperty("sourceId").GetString());
            var snapshot = Assert.Single(result.GetProperty("snapshots").EnumerateArray());
            Assert.Equal(projectId, snapshot.GetProperty("projectId").GetGuid());
            Assert.Equal([node.Id], snapshot.GetProperty("selectedNodeIds").EnumerateArray().Select(item => item.GetString()));
        }
    }
}
