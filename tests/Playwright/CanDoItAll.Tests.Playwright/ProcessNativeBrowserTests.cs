using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessNativeBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_process_launch_workflow_and_manager_chat_keep_native_persistence(bool projectScoped) {
        var marker = "PC1 actual assistant " + Guid.NewGuid().ToString("N");
        var submittedContext = string.Empty;
        var attachmentContext = string.Empty;
        var attachmentReply = marker + " attachment received";
        await using var wire = await AgentResponseFixture.StartAsync("unused");
        wire.Steps = [input => {
            submittedContext = input.GetRawText();
            return new([], marker);
        }, input => {
            attachmentContext = input.GetRawText();
            return new([], attachmentReply);
        }];
        await using var host = await ProcessNativeBrowserHost.StartAsync(wire.BaseUrl, failFirstRunRead: true);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        var route = projectScoped ? $"/projects/{host.ProjectId:D}/processes" : "/processes";
        try {
            await page.GotoAsync(host.BaseUrl + route);
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await Assertions.Expect(page.GetByTestId("processes-page-scaffold")).ToHaveAttributeAsync("data-interactive", "true");
            var definition = page.GetByTestId("processes-definition-" + ProcessNativeBrowserHost.CompleteDefinition);
            if (await definition.GetAttributeAsync("aria-selected") != "true") {
                await definition.ClickAsync();
            }
            await Assertions.Expect(page.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync(ProcessNativeBrowserHost.CompleteDefinition);
            await page.GetByTestId("processes-command-launchrun").ClickAsync();
            await page.WaitForURLAsync(new Regex("[?&]runId=", RegexOptions.IgnoreCase), new() { Timeout = 90_000 });
            var runId = new ProcessRunId(Guid.Parse(QueryHelpers.ParseQuery(new Uri(page.Url).Query)["runId"].ToString()));
            await host.ReadFailure.WaitAsync(TimeSpan.FromSeconds(60));
            Assert.Equal(1, host.ReadFailures);
            await Assertions.Expect(page.GetByTestId("live-processes-error")).ToContainTextAsync("The process workspace could not be read. Refresh to recheck its current scope and access.");
            await Assertions.Expect(page.GetByTestId("live-processes-error")).Not.ToContainTextAsync("PC1 accepted-run read unavailable");
            var preparation = await host.ReadAsync(services => services.GetRequiredService<IProcessPreparedLaunchStore>().FindByRunAsync(runId));
            Assert.NotNull(preparation);
            Assert.NotNull(preparation.AcceptedAtUtc);
            Assert.NotNull(preparation.Preparation.CallerIntentId);
            Assert.Equal(projectScoped ? host.ProjectId : (Guid?)null, preparation.Preparation.Request.ProjectId);
            var state = await ReadState();
            using (var completionDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(60))) {
                while (state.Status == ProcessRuntimeStatus.Active && !completionDeadline.IsCancellationRequested) {
                    await Task.Delay(200, completionDeadline.Token);
                    state = await ReadState();
                }
            }
            Assert.Equal(ProcessRuntimeStatus.Completed, state.Status);
            Assert.Equal("PC1 native workflow completed.", Assert.Single(state.AppliedResults).UserSafeSummary);
            var workflow = host.Workflows[ProcessNativeBrowserHost.CompleteDefinition];
            var child = Assert.Single(await host.ReadAsync(services => services.GetRequiredService<IWorkflowRuntimeManager>().ListRunsAsync(workflow.Id)));
            Assert.Equal(WorkflowRunState.Completed, child.State);
            var origin = Assert.IsType<WorkflowLaunchOrigin.ProcessDispatchAssignment>(child.Origin);
            Assert.Equal(runId.Value, origin.Dispatch.ProcessRun.Value);
            await page.ReloadAsync();
            await page.GetByTestId("live-processes-run-open-details").WaitForAsync();
            Assert.Equal(runId, (await ReadState()).RunId);
            Assert.Equal(child.RunId, Assert.Single(await host.ReadAsync(services => services.GetRequiredService<IWorkflowRuntimeManager>().ListRunsAsync(workflow.Id))).RunId);
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "native-completed-run.png") });
            var filePath = $"artifacts/process-runs/{runId.Value:D}/evidence.md";
            await WriteArtifactAsync("First authorized revision");
            await page.GetByTestId("live-processes-run-open-details").First.ClickAsync();
            await page.GetByTestId("live-processes-run-files").ClickAsync();
            var files = page.GetByTestId("process-run-files-dialog");
            await files.Locator(".ft-file-browser__item-main").Filter(new() { HasTextString = "evidence.md" }).DblClickAsync();
            await Assertions.Expect(files.GetByTestId("interaction-markdown-view")).ToContainTextAsync("First authorized revision");
            await Assertions.Expect(files.GetByTestId("interaction-mode-edit")).ToBeDisabledAsync();
            await WriteArtifactAsync("Second authorized revision");
            await files.GetByTestId("process-run-files-back").ClickAsync();
            await files.Locator(".ft-file-browser__item-main").Filter(new() { HasTextString = "evidence.md" }).PressAsync("Enter");
            await Assertions.Expect(files.GetByTestId("interaction-markdown-view")).ToContainTextAsync("Second authorized revision");
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "native-authorized-files.png") });
            await files.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await page.GetByTestId("live-processes-run-detail-dialog").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await page.GotoAsync($"{host.BaseUrl}{route}?runId={runId.Value:D}&definitionKey={ProcessNativeBrowserHost.CompleteDefinition}");
            await Assertions.Expect(page.GetByTestId("processes-page-scaffold")).ToHaveAttributeAsync("data-interactive", "true");
            await page.GetByTestId("processes-detail-tab-manager-chat").ClickAsync();
            var manager = page.GetByTestId("processes-manager-chat-agent-select");
            await Assertions.Expect(manager).ToBeEnabledAsync(new() { Timeout = 30_000 });
            if (await manager.InputValueAsync() != host.AgentId.ToString("D")) {
                await manager.SelectOptionAsync(host.AgentId.ToString("D"));
            }
            await Assertions.Expect(page.GetByTestId("processes-manager-chat-context").Locator("h3")).ToHaveTextAsync("PC1 process manager");
            await Assertions.Expect(page.GetByTestId("processes-manager-chat-reload")).ToBeEnabledAsync();
            var chat = page.GetByTestId("processes-manager-chat-tab");
            await Assertions.Expect(chat.Locator(".chat-panel-header").GetByAltText("PC1 process manager")).ToBeVisibleAsync();
            await chat.GetByTestId("chat-prompt-input").FillAsync("Explain the selected process in one sentence.");
            await chat.GetByTestId("chat-prompt-input").PressAsync("Tab");
            await Assertions.Expect(chat.GetByTestId("chat-prompt-input")).ToHaveValueAsync("Explain the selected process in one sentence.");
            await chat.GetByTestId("chat-send-button").ClickAsync();
            await wire.PrefixFlushed.Task.WaitAsync(TimeSpan.FromSeconds(60));
            try {
                await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Agent responding");
                await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "native-streaming.png") });
            } finally {
                wire.ReleaseReply.TrySetResult();
            }
            await Assertions.Expect(chat).ToContainTextAsync(marker, new() { Timeout = 60_000 });
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Completed");
            var execution = await host.ReadAsync(async services => {
                var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
                var run = Assert.Single(await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: host.AgentId)));
                return await workspace.GetExecutionRunDetailAsync(run.Id);
            });
            Assert.Equal(RunOutcome.Succeeded, execution.Run.Outcome);
            Assert.Equal(marker, Assert.Single(execution.ChatSession!.Messages, message => message.Role == ChatMessageRole.Assistant).Content);
            Assert.Contains(runId.Value.ToString("N"), submittedContext.Replace("-", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("local-fixture-credential", submittedContext, StringComparison.Ordinal);
            Assert.Equal(1, wire.Requests);
            const string attachmentContent = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";
            var attachment = await host.ReadAsync(async services => {
                var bytes = Convert.FromBase64String(attachmentContent);
                using var stream = new MemoryStream(bytes);
                return await services.GetRequiredService<IAgentChatAttachmentStagingService>()
                    .StageImageAsync("pc1-manager.png", "image/png", bytes.Length, stream);
            });
            var attachmentPath = attachment.RelativePath;
            var attached = await host.ReadAsync(async services => {
                var store = Assert.IsAssignableFrom<ISandboxWorkspaceExecutionRunMutationStore>(services.GetRequiredService<ISandboxWorkspaceStore>());
                return await store.UpdateExecutionRunDetailAsync(execution.Run.Id, detail => detail with {
                    Artifacts = [.. detail.Artifacts, new ExecutionArtifactRecord(Guid.NewGuid(), execution.Run.Id,
                        "image", "PC1 manager attachment", attachmentPath, attachment.ContentType, "owned-browser-fixture",
                        "A persisted artifact staged through the real manager control.", DateTimeOffset.UtcNow)]
                });
            });
            Assert.Contains(attached.Artifacts, artifact => artifact.RelativePath == attachmentPath);
            await chat.GetByTestId("chat-attachment-button").ClickAsync();
            await Assertions.Expect(chat).ToContainTextAsync(attachmentPath);
            await chat.GetByTestId("chat-prompt-input").FillAsync("Read the attached evidence and confirm receipt.");
            await chat.GetByTestId("chat-prompt-input").PressAsync("Tab");
            await chat.GetByTestId("chat-send-button").ClickAsync();
            await Assertions.Expect(chat).ToContainTextAsync(attachmentReply, new() { Timeout = 60_000 });
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Completed");
            using (var submittedAttachment = JsonDocument.Parse(attachmentContext)) {
                var image = Assert.Single(submittedAttachment.RootElement.GetProperty("input").EnumerateArray()
                    .Where(item => item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                    .SelectMany(item => item.GetProperty("content").EnumerateArray()),
                    part => part.TryGetProperty("type", out var type) && type.GetString() == "input_image");
                Assert.Equal("data:image/png;base64," + attachmentContent, image.GetProperty("image_url").GetString());
            }
            var attachmentRun = await host.ReadAsync(async services => {
                var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
                var run = Assert.Single(await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: host.AgentId)),
                    candidate => candidate.Id != execution.Run.Id);
                return await workspace.GetExecutionRunDetailAsync(run.Id);
            });
            Assert.Equal(execution.ChatSession.Id, attachmentRun.ChatSession!.Id);
            Assert.Equal(RunOutcome.Succeeded, attachmentRun.Run.Outcome);
            Assert.Contains(attachmentRun.ChatSession.Messages, message => message.Role == ChatMessageRole.Assistant && message.Content == attachmentReply);
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "native-chat-attachment.png") });
            await page.GetByTestId("processes-detail-tab-runs").ClickAsync();
            await page.GetByTestId("processes-detail-tab-manager-chat").ClickAsync();
            await Assertions.Expect(chat).ToContainTextAsync(marker);
            Assert.Equal(2, wire.Requests);
            await page.GotoAsync($"{host.BaseUrl}{route}?definitionKey={ProcessNativeBrowserHost.WaitingDefinition}");
            await Assertions.Expect(page.GetByTestId("processes-page-scaffold")).ToHaveAttributeAsync("data-interactive", "true");
            await page.GetByTestId("processes-command-launchrun").ClickAsync();
            await page.WaitForURLAsync(new Regex("[?&]runId=", RegexOptions.IgnoreCase), new() { Timeout = 90_000 });
            var waitingRunId = new ProcessRunId(Guid.Parse(QueryHelpers.ParseQuery(new Uri(page.Url).Query)["runId"].ToString()));
            var waitingWorkflow = host.Workflows[ProcessNativeBrowserHost.WaitingDefinition];
            using (var waitingDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(60))) {
                while (!(await host.ReadAsync(services => services.GetRequiredService<IWorkflowRuntimeManager>().ListRunsAsync(waitingWorkflow.Id)))
                    .Any(run => run.State == WorkflowRunState.WaitingForInput)) {
                    await Task.Delay(200, waitingDeadline.Token);
                }
            }
            await page.GetByTestId("live-processes-run-open-details").First.ClickAsync();
            var runDialog = page.GetByTestId("live-processes-run-detail-dialog");
            await runDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            var beforeCancellation = await host.ReadAsync(services => services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(waitingRunId));
            Assert.NotEqual(ProcessRuntimeStatus.Cancelled, beforeCancellation!.Status);
            await page.GetByTestId("live-processes-run-open-details").First.ClickAsync();
            await runDialog.GetByTestId("process-run-cancel").ClickAsync();
            await Assertions.Expect(runDialog.GetByTestId("process-run-cancellation-result")).ToContainTextAsync("cancel", new() { IgnoreCase = true, Timeout = 60_000 });
            var cancelled = await host.ReadAsync(services => services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(waitingRunId));
            Assert.Equal(ProcessRuntimeStatus.Cancelled, cancelled!.Status);
            Assert.Equal(ProcessRuntimeStatus.Completed, (await ReadState()).Status);
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "native-cancelled-run.png") });
            await File.WriteAllTextAsync(Path.Combine(host.Evidence, "native-oracle.json"), JsonSerializer.Serialize(new {
                RunId = runId.Value, preparation.Preparation.CallerIntentId, preparation.Preparation.AdmissionId,
                preparation.LinkDeliveryState, preparation.DeliveredLinkId, WorkflowRun = child.RunId,
                ChatRun = execution.Run.Id, ChatSession = execution.ChatSession.Id, AssistantContent = marker, wire.Requests,
                Attachment = attachmentPath, AttachmentRun = attachmentRun.Run.Id, AttachmentContent = attachmentContent,
                FailedAcceptedRunReads = host.ReadFailures,
                AuthorizedFile = filePath, CancelledRun = waitingRunId.Value
            }));
            Assert.Empty(errors);
            async Task<ProcessRuntimeStateSnapshot> ReadState() => (await host.ReadAsync(services => services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(runId)))!;
            Task<bool> WriteArtifactAsync(string text) => host.ReadAsync(services => {
                var saved = services.GetRequiredService<IWorkspaceFileService>().WriteTextFile(filePath, $"# Native process evidence\n\n{text}\n");
                Assert.True(saved.Succeeded, saved.Message);
                return Task.FromResult(saved.Succeeded);
            });
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "failure.png") });
            await File.WriteAllTextAsync(Path.Combine(host.Evidence, "failure.txt"), await page.Locator("body").InnerTextAsync());
            throw;
        } finally {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(host.Evidence, "trace.zip") });
        }
    }
}
