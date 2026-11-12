using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;
using static CanDoItAll.Tests.Playwright.Smoke.ScriptedAgentUiFixture;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed class AgentRuntimeUiTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "Playwright")]
    [Trait("Category", "HostPlatform")]
    public async Task Actual_MAF_chat_approval_activity_stream_reopen_and_cancel_replace_only_external_model(bool approve) {
        var evidence = new UiEvidence(approve ? "ui-deterministic-agent-approved" : "ui-deterministic-agent-cancelled") {
            Execution = "deterministic-external-model"
        };
        await using var wire = await AgentResponseFixture.StartAsync(ProjectStructureToolPolicy.ProjectStructureAssetCreate);
        await using var host = await LiveUiHost.StartAsync();
        try {
            var fixture = await CreateFileJourneyAsync(host);
            host.Watch(fixture.Agent.Id);
            var marker = "agent-stream-" + Guid.NewGuid().ToString("D");
            var title = "Deterministic actual asset " + marker;
            wire.Reply = marker;
            wire.Arguments = JsonSerializer.Serialize(new { projectId = fixture.ProjectId, request = new {
                objectType = "File", title, parentNodeKey = fixture.ParentId,
                media = new { fileName = "actual.txt", contentType = "text/plain", base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(marker)) }
            } });
            await ConfigureScriptedAgentAsync(host, fixture.Agent.Id, wire.BaseUrl);
            var page = await host.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);
            await GrantFileJourneyAsync(host, oracle, page, fixture);
            var chat = await OpenFileChatAsync(host, oracle, page, fixture);
            await SendAsync(chat, "Create the requested synthetic file once and wait for my approval.");
            var approval = chat.Locator("[data-testid^='chat-approval-approve-']");
            await Assertions.Expect(approval).ToHaveCountAsync(1, new() { Timeout = 60_000 });
            var pending = await host.SeedAsync(services => ReadLatestRunAsync(services, fixture.Agent.Id));
            var proposal = Assert.Single(Proposals(pending));
            Assert.Equal(ProjectStructureToolPolicy.ProjectStructureAssetCreate, proposal.Payload.ToolName);
            Assert.Equal(ExecutionApprovalStatus.Pending, proposal.ApprovalStatus);
            Assert.Equal(AgentChatTrustedSourceKinds.ProjectStructure, pending.Run.SourceKind);
            Assert.True(await chat.Locator(".chat-panel-header__title-row").EvaluateAsync<bool>("element => element.getBoundingClientRect().width >= 120"));
            Assert.False(await chat.Locator(".chat-panel-header").EvaluateAsync<bool>("element => element.scrollWidth > element.clientWidth"));
            Assert.DoesNotContain((await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>()
                .GetStructureAsync(fixture.ProjectId, new(IncludeAssets: true)))).Nodes, item => item.Title == title);
            await page.ScreenshotAsync(new() { Path = host.Artifact(approve ? "agent-scripted-approval.png" : "agent-scripted-before-cancel.png") });
            if (approve) {
                await approval.ClickAsync();
                await wire.PrefixFlushed.Task.WaitAsync(TimeSpan.FromSeconds(60));
                try {
                    await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Agent responding");
                    await page.ScreenshotAsync(new() { Path = host.Artifact("agent-scripted-live-prefix.png") });
                } finally {
                    wire.ReleaseReply.TrySetResult();
                }
            } else {
                await chat.GetByTestId("agents-chat-cancel-pending-run").ClickAsync();
            }
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase"))
                .ToHaveTextAsync(approve ? "Completed" : "Cancelled", new() { Timeout = 60_000 });
            var result = await host.SeedAsync(services => ReadLatestRunAsync(services, fixture.Agent.Id));
            evidence.RecordRun(approve ? "approved" : "cancelled", result);
            Assert.Equal(pending.Run.Id, result.Run.Id);
            Assert.Equal(approve ? RunOutcome.Succeeded : RunOutcome.Cancelled, result.Run.Outcome);
            var tree = await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>()
                .GetStructureAsync(fixture.ProjectId, new(IncludeAssets: true)));
            if (approve) {
                await Assertions.Expect(chat).ToContainTextAsync(marker);
                AssertSuccessfulTool(result, ProjectStructureToolPolicy.ProjectStructureAssetCreate);
                var file = Assert.Single(tree.Nodes, item => item.Title == title);
                Assert.Equal(fixture.ParentId, file.ParentId);
                var content = await ReadFileContentAsync(host, fixture.ProjectId, file.Id);
                Assert.Equal(marker, content);
                evidence.Observations["file"] = new { file.Id, file.ParentId, file.ArtifactId,
                    sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))) };
            } else {
                Assert.DoesNotContain(tree.Nodes, item => item.Title == title);
                Assert.DoesNotContain(Proposals(result), item => item.EffectState == AgentToolEffectState.Committed);
            }
            Assert.Equal(approve ? 2 : 1, wire.Requests);
            await oracle.NavigateAsync($"{host.BaseUrl}/agents?tab=chat&agentId={fixture.Agent.Id:D}");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
            await page.GetByRole(AriaRole.Button, new() { Name = "Open thread " + result.ChatSession!.Title, Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByText("Loading agent workspace", new() { Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("agent-thread-selected-agent")).ToContainTextAsync(fixture.Agent.Name);
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Open thread " + result.ChatSession.Title, Exact = true }))
                .ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(page.GetByTestId("chat-execution-summary").Or(page.GetByTestId("chat-execution-history")).First).ToBeVisibleAsync();
            var reopened = await host.SeedAsync(services => ReadLatestRunAsync(services, fixture.Agent.Id));
            Assert.Equal(result.Run.Id, reopened.Run.Id);
            Assert.Equal(approve ? 2 : 1, wire.Requests);
            await Assertions.Expect(page.GetByText("Loading agent workspace", new() { Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("agent-thread-selected-agent")).ToContainTextAsync(fixture.Agent.Name);
            await page.ScreenshotAsync(new() { Path = host.Artifact(approve ? "agent-scripted-reopened.png" : "agent-scripted-cancelled.png") });
            evidence.Observations["externalBoundary"] = new { requests = wire.Requests, externalCalls = 0, uiReopened = true, approve };
            await oracle.AssertCleanAsync();
            evidence.Passed = true;
        } finally {
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }
    }

}
