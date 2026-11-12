using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;
using static CanDoItAll.Tests.Playwright.Smoke.ScriptedAgentUiFixture;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed class FileJourneyHarnessBrowserTests {
    public enum FileHarnessAttack { SiblingProject, AnotherPath, Overwrite, SecondAttachment, ExternalSource }

    [Fact]
    [Trait("Category", "Playwright")]
    [Trait("Category", "HostPlatform")]
    public async Task File_harness_timeout_retains_original_write_and_pending_evidence_until_readers_finish() {
        var evidence = new UiEvidence("file-harness-retained-evidence") { Execution = "deterministic-external-model" };
        await using var wire = await AgentResponseFixture.StartAsync(ToolContractCatalog.WorkspaceWriteFile);
        var host = await LiveUiHost.StartAsync();
        using var stopTurn = new CancellationTokenSource();
        var queued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReader = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ExecutionRunDetail>? turn = null;
        try {
            var fixture = await CreateFileJourneyAsync(host);
            await ConfigureScriptedAgentAsync(host, fixture.Agent.Id, wire.BaseUrl);
            host.Watch(fixture.Agent.Id);
            var expected = new FileApprovalIntent(fixture.ProjectId, fixture.ParentId, "Uncommitted attachment",
                "Retained committed write", "retained.txt", "text/plain", $"proof/{Guid.NewGuid():N}/retained.txt");
            wire.HoldReply = false;
            wire.Steps = [
                _ => new([Call(ToolContractCatalog.WorkspaceWriteFile, new { path = expected.WorkspacePath, content = expected.Content, overwrite = false })]),
                _ => new([Call(ProjectStructureToolPolicy.ProjectStructureAssetCreate, new { projectId = expected.ProjectId, request = new {
                    objectType = "File", title = expected.Title, parentNodeKey = expected.ParentId, sourceWorkspacePath = expected.WorkspacePath,
                    sourceFileName = expected.FileName, sourceContentType = expected.ContentType } })])
            ];
            wire.BeforeResponse = async ordinal => {
                if (ordinal == 2) {
                    queued.TrySetResult();
                    await releaseProvider.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
            };
            var page = await host.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);
            await GrantFileJourneyAsync(host, oracle, page, fixture);
            var chat = await OpenFileChatAsync(host, oracle, page, fixture);
            turn = FileTurnAsync(host, page, chat, fixture.Agent.Id, "Write the fixture file and prepare its attachment.", expected,
                cancellationToken: stopTurn.Token);
            await queued.Task.WaitAsync(TimeSpan.FromSeconds(90));
            var before = await host.SeedAsync(services => ReadLatestRunAsync(services, fixture.Agent.Id));
            var originalWrite = Assert.Single(Proposals(before), proposal => proposal.Payload.ToolName == ToolContractCatalog.WorkspaceWriteFile);
            Assert.Equal(AgentToolEffectState.Committed, originalWrite.EffectState);
            var heldRead = host.SeedAsync(async services => {
                readerEntered.TrySetResult();
                await releaseReader.Task;
                return await services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetExecutionRunDetailAsync(before.Run.Id);
            });
            await readerEntered.Task;
            await stopTurn.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await turn);
            await Task.WhenAll(host.StopAsync(LiveUiStopReason.Timeout), host.StopAsync(LiveUiStopReason.ExplicitCancellation));
            releaseReader.TrySetResult();
            var after = await heldRead.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(before.Run.Id, after.Run.Id);
            var retainedWrite = Assert.Single(Proposals(after), proposal => proposal.Payload.ToolName == ToolContractCatalog.WorkspaceWriteFile);
            Assert.Equal(originalWrite.IntentId, retainedWrite.IntentId);
            Assert.Equal(AgentToolEffectState.Committed, retainedWrite.EffectState);
            Assert.Equal(before.ToolReceipts.Select(receipt => receipt.Id), after.ToolReceipts.Select(receipt => receipt.Id));
            var retainedContent = await host.SeedAsync(services => Task.FromResult(
                services.GetRequiredService<IWorkspaceFileService>().ReadTextFile(expected.WorkspacePath!)));
            Assert.True(retainedContent.Succeeded);
            Assert.Equal(expected.Content, retainedContent.Content);
            Assert.DoesNotContain(Proposals(after), proposal => proposal.Payload.ToolName == ProjectStructureToolPolicy.ProjectStructureAssetCreate);
            Assert.Equal(LiveUiStopReason.Timeout, host.StopReason);
            Assert.False(host.SpendBoundExceeded);
            releaseProvider.TrySetResult();
            Assert.Equal(fixture.SeedContent, await ReadFileContentAsync(host, fixture.ProjectId, fixture.SeedAssetId));
            Assert.Equal(fixture.SiblingContent, await ReadFileContentAsync(host, fixture.SiblingId, fixture.SiblingAssetId));
            evidence.RecordRun("original-write-after-app-stop", after);
            evidence.Observations["retirement"] = new { before.Run.Id, wire.Requests, originalDatabaseRetained = true,
                readerSettledBeforeCleanup = true, queuedAttachmentNotApproved = true };
            evidence.Passed = true;
        } finally {
            releaseReader.TrySetResult();
            releaseProvider.TrySetResult();
            await stopTurn.CancelAsync();
            if (turn is not null && !turn.IsCompleted) {
                _ = await Record.ExceptionAsync(async () => await turn);
            }
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
            await Task.WhenAll(host.DisposeAsync().AsTask(), host.DisposeAsync().AsTask());
        }
    }

    [Fact]
    [Trait("Category", "Playwright")]
    [Trait("Category", "HostPlatform")]
    public async Task File_harness_rehearses_read_write_attach_two_readbacks_and_preview_with_delayed_approval() {
        var evidence = new UiEvidence("file-harness-complete") { Execution = "deterministic-external-model" };
        await using var wire = await AgentResponseFixture.StartAsync(ToolContractCatalog.WorkspaceWriteFile);
        await using var host = await LiveUiHost.StartAsync();
        var delayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try {
            var page = await host.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);
            Guid projectId = await ProjectsPortfolioUiJourney.CreateAndEditAsync(host, page, oracle);
            var fixture = await CreateFileJourneyAsync(host, projectId);
            var provider = await ProviderProfilesUiJourney.CreateAsync(host, oracle, page, wire.BaseUrl);
            evidence.Observations["provider-created-through-ui"] = provider;
            host.Watch(fixture.Agent.Id);
            var expected = new FileApprovalIntent(fixture.ProjectId, fixture.ParentId, "Exact fixture file",
                "# Complete fixture\n" + Guid.NewGuid().ToString("D"), "roundtrip.md", "text/markdown", $"proof/{Guid.NewGuid():N}/roundtrip.md");
            wire.HoldReply = false;
            wire.BeforeResponse = async ordinal => {
                if (ordinal == 3) {
                    delayed.TrySetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
            };
            wire.Steps = [
                _ => new([Call(ProjectStructureToolPolicy.ProjectStructureAssetGet, new { projectId = fixture.ProjectId, nodeId = fixture.SeedAssetId }),
                    Call(ProjectStructureToolPolicy.ProjectStructureAssetContentGet, new { projectId = fixture.ProjectId, nodeId = fixture.SeedAssetId })]),
                input => new([], DecodeToolContent(input)),
                _ => new([Call(ToolContractCatalog.WorkspaceWriteFile, new { path = expected.WorkspacePath, content = expected.Content, overwrite = false })]),
                _ => new([Call(ProjectStructureToolPolicy.ProjectStructureAssetCreate, new { projectId = expected.ProjectId, request = new {
                    objectType = "File", title = expected.Title, parentNodeKey = expected.ParentId, sourceWorkspacePath = expected.WorkspacePath,
                    sourceFileName = expected.FileName, sourceContentType = expected.ContentType } })]),
                input => {
                    var id = FindToolString(input, "id", expected.Title);
                    return new([Call(ProjectStructureToolPolicy.ProjectStructureAssetGet, new { projectId = expected.ProjectId, nodeId = id }),
                        Call(ProjectStructureToolPolicy.ProjectStructureAssetContentGet, new { projectId = expected.ProjectId, nodeId = id })]);
                },
                input => {
                    Assert.Equal(expected.Content, DecodeToolContent(input));
                    return new([], "The exact file and attachment were read back.");
                }
            ];
            await GrantFileJourneyAsync(host, oracle, page, fixture);
            var core = await AgentEditorCoreUiJourney.EditAsync(host, oracle, page, fixture.Agent.Id, provider.Id);
            Assert.Equal(0, wire.Requests);
            wire.Steps = wire.Steps.Select(step => new Func<JsonElement, AgentResponseFixture.ScriptedTurn>(input => {
                AgentEditorCoreUiJourney.AssertRequest(input, core);
                return step(input);
            })).ToArray();
            evidence.Observations["saved-core-runtime"] = core;
            var chat = await OpenFileChatAsync(host, oracle, page, fixture);
            var read = await FileTurnAsync(host, page, chat, fixture.Agent.Id, "Read the existing selected asset and report its actual content.");
            Assert.Contains(fixture.SeedNonce, read.Run.ResultSummary, StringComparison.Ordinal);
            evidence.RecordRun("hidden-nonce-read", read);
            var writing = FileTurnAsync(host, page, chat, fixture.Agent.Id, "Write and attach the exact synthetic fixture file, then read it back.", expected);
            await delayed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(0, await chat.Locator("[data-testid^='chat-approval-approve-']").CountAsync());
            release.TrySetResult();
            var completed = await writing;
            evidence.RecordRun("write-attach-readbacks", completed);
            foreach (var tool in new[] { ToolContractCatalog.WorkspaceWriteFile, ProjectStructureToolPolicy.ProjectStructureAssetCreate,
                ProjectStructureToolPolicy.ProjectStructureAssetGet, ProjectStructureToolPolicy.ProjectStructureAssetContentGet }) {
                AssertSuccessfulTool(completed, tool);
            }
            var tree = await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>()
                .GetStructureAsync(fixture.ProjectId, new(IncludeAssets: true)));
            var file = Assert.Single(tree.Nodes, node => node.Title == expected.Title);
            Assert.Equal(expected.ParentId, file.ParentId);
            Assert.Equal(expected.Content, await ReadFileContentAsync(host, fixture.ProjectId, file.Id));
            Assert.Equal(fixture.SiblingContent, await ReadFileContentAsync(host, fixture.SiblingId, fixture.SiblingAssetId));
            await oracle.NavigateAsync($"{host.BaseUrl}/projects/{fixture.ProjectId:D}/structure");
            await ReadyFileCanvasAsync(page);
            await SelectFileNodeAsync(page, file.Id, file.Title);
            await page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = "Expand preview", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Dialog, new() { Name = "roundtrip.md file interaction", Exact = true })).ToContainTextAsync(expected.Content[2..]);
            await page.ScreenshotAsync(new() { Path = host.Artifact("exact-file-preview.png") });
            await ProjectsFilesBrowserProof.ReopenProducedAssetAsync(host, page, oracle, fixture.ProjectId,
                file.MediaRelativePath!, expected.Content, "agent-produced-file");
            Assert.Equal(6, wire.Requests);
            evidence.Observations["effects"] = new { file.Id, file.ParentId, writes = 1, attachments = 1, wire.Requests,
                sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expected.Content))) };
            wire.Steps = [.. wire.Steps, input => {
                Assert.Equal(provider.AlternateModel, input.GetProperty("model").GetString());
                return new([], "PP1_CHAT_OK");
            }];
            await SharedProviderMetadataUiChecks.ExerciseSimpleChatAsync(page, host.BaseUrl, provider.Name,
                provider.Model, [provider.Model, provider.AlternateModel], provider.AlternateModel, host.ArtifactDirectory,
                "pp1-consumer", "PP1_CHAT_OK", importedProvider: false, navigate: oracle.NavigateAsync);
            var chatReceipt = await host.SeedAsync(async services => {
                var definitions = await services.GetRequiredService<ILlmChatDefinitionApplicationService>().ListAsync(new(searchText: "UI shared catalog pp1-consumer"));
                Assert.True(definitions.IsSuccess);
                var definition = Assert.Single(definitions.Value!);
                Assert.Equal(provider.Id, definition.Revision.ProviderProfileId);
                Assert.Equal(provider.AlternateModel, definition.Revision.Model);
                var conversations = services.GetRequiredService<ILlmChatConversationApplicationService>();
                var listed = await conversations.ListAsync(new(definitionId: definition.Definition.Id));
                Assert.True(listed.IsSuccess);
                var detail = await conversations.GetAsync(Assert.Single(listed.Value!).Conversation.Id, new LlmChatTranscriptQuery());
                Assert.True(detail.IsSuccess);
                Assert.Equal(provider.Id, detail.Value!.ProviderModel.ProviderId);
                var assistant = Assert.Single(detail.Value.TranscriptMessages, entry => entry.Role == LlmMessageRole.Assistant);
                Assert.Equal("PP1_CHAT_OK", assistant.Text);
                Assert.Equal(provider.AlternateModel, assistant.Model);
                var operation = await services.GetRequiredService<ILlmChatOperationApplicationService>().GetAsync(new LlmChatOperationId(assistant.TurnId));
                Assert.True(operation.IsSuccess);
                Assert.Equal(LlmChatOperationStatus.Succeeded, operation.Value!.Operation.Status);
                Assert.Equal(assistant.EntryId, operation.Value.AssistantMessage!.EntryId);
                return new { definition.Definition.Id, ConversationId = detail.Value.Conversation.Id,
                    OperationId = assistant.TurnId, ProviderId = provider.Id, assistant.Model,
                    sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(assistant.Text))) };
            });
            evidence.Observations["simple-chat-native-receipt"] = chatReceipt;
            Assert.Equal(7, wire.Requests);
            await AgentEditorCoreUiJourney.DeleteCompletedFixtureAsync(host, oracle, page, fixture.Agent.Id);
            Assert.Equal(expected.Content, await ReadFileContentAsync(host, fixture.ProjectId, file.Id));
            Assert.Equal(fixture.SiblingContent, await ReadFileContentAsync(host, fixture.SiblingId, fixture.SiblingAssetId));
            await oracle.AssertCleanAsync();
            evidence.Passed = true;
        } finally {
            release.TrySetResult();
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }
    }

    [Theory]
    [InlineData(FileHarnessAttack.SiblingProject)]
    [InlineData(FileHarnessAttack.AnotherPath)]
    [InlineData(FileHarnessAttack.Overwrite)]
    [InlineData(FileHarnessAttack.SecondAttachment)]
    [InlineData(FileHarnessAttack.ExternalSource)]
    [Trait("Category", "Playwright")]
    [Trait("Category", "HostPlatform")]
    public async Task File_harness_rejects_changed_proposals_through_actual_UI_without_widening_grants(FileHarnessAttack attack) {
        var evidence = new UiEvidence($"file-harness-refusal-{attack}") { Execution = "deterministic-external-model" };
        await using var wire = await AgentResponseFixture.StartAsync(ProjectStructureToolPolicy.ProjectStructureAssetCreate);
        await using var host = await LiveUiHost.StartAsync();
        try {
            var page = await host.NewPageAsync();
            await page.SetViewportSizeAsync(1920, 1080);
            var oracle = CrmHrBrowserOracle.Attach(page);
            Guid? projectId = attack == FileHarnessAttack.SiblingProject
                ? await ProjectsPortfolioUiJourney.CreateAndEditAsync(host, page, oracle) : null;
            var fixture = await CreateFileJourneyAsync(host, projectId);
            await ConfigureScriptedAgentAsync(host, fixture.Agent.Id, wire.BaseUrl);
            host.Watch(fixture.Agent.Id);
            var writeAttack = attack is FileHarnessAttack.AnotherPath or FileHarnessAttack.Overwrite;
            var expected = new FileApprovalIntent(fixture.ProjectId, fixture.ParentId, "Only permitted fixture asset", "safe",
                "fixture.txt", "text/plain", writeAttack ? $"proof/{Guid.NewGuid():N}/fixture.txt" : null);
            AgentResponseFixture.ScriptedCall Asset(bool changed) => Call(ProjectStructureToolPolicy.ProjectStructureAssetCreate, new {
                projectId = attack == FileHarnessAttack.SiblingProject ? fixture.SiblingId : fixture.ProjectId,
                request = new {
                    objectType = "File", title = expected.Title + (changed ? " changed" : ""), parentNodeKey = expected.ParentId,
                    media = attack == FileHarnessAttack.ExternalSource ? null : new { fileName = expected.FileName, contentType = expected.ContentType, base64Data = "c2FmZQ==" },
                    sourceUrl = attack == FileHarnessAttack.ExternalSource ? wire.BaseUrl + "/unexpected-source" : null
                }
            });
            var attackCall = writeAttack ? Call(ToolContractCatalog.WorkspaceWriteFile, new {
                path = attack == FileHarnessAttack.AnotherPath ? expected.WorkspacePath + ".changed" : expected.WorkspacePath,
                content = expected.Content, overwrite = attack == FileHarnessAttack.Overwrite
            }) : Asset(false);
            wire.HoldReply = false;
            wire.Steps = attack == FileHarnessAttack.SecondAttachment
                ? [_ => new([attackCall]), _ => new([Asset(true)]), _ => new([], "Stopped after the rejected second attachment.")]
                : [_ => new([attackCall]), _ => new([], "Stopped after refusal.")];
            await GrantFileJourneyAsync(host, oracle, page, fixture);
            var chat = await OpenFileChatAsync(host, oracle, page, fixture);
            var failure = await Record.ExceptionAsync(() => FileTurnAsync(host, page, chat, fixture.Agent.Id,
                "Submit only the synthetic test proposal and stop after the operator decision.", expected, requireCompleted: false));
            if (failure is not null) {
                Assert.IsType<InvalidOperationException>(failure);
                Assert.StartsWith("File proposal rejected by the fixture:", failure.Message, StringComparison.Ordinal);
            }
            await Assertions.Expect(chat.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync(TerminalPhase, new() { Timeout = 60_000 });
            var run = await host.SeedAsync(services => ReadLatestRunAsync(services, fixture.Agent.Id));
            evidence.RecordRun("refused-intent", run);
            Assert.Equal(attack == FileHarnessAttack.SecondAttachment ? 1 : 0, run.Approvals.Count(item => item.Status == ExecutionApprovalStatus.Approved));
            Assert.Equal(attack == FileHarnessAttack.SecondAttachment ? 1 : 0, Proposals(run).Count(item => item.EffectState == AgentToolEffectState.Committed));
            var tree = await host.SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>().GetStructureAsync(fixture.ProjectId, new(IncludeAssets: true)));
            Assert.Equal(attack == FileHarnessAttack.SecondAttachment ? 1 : 0, tree.Nodes.Count(item => item.Title == expected.Title));
            Assert.DoesNotContain(tree.Nodes, item => item.Title == expected.Title + " changed");
            Assert.Equal(fixture.SeedContent, await ReadFileContentAsync(host, fixture.ProjectId, fixture.SeedAssetId));
            Assert.Equal(fixture.SiblingContent, await ReadFileContentAsync(host, fixture.SiblingId, fixture.SiblingAssetId));
            var agent = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentEditorAsync(fixture.Agent.Id));
            Assert.Equal([fixture.ProjectId], agent.ProjectStructureAccess.AllowedProjectIds);
            Assert.False(agent.ProjectStructureAccess.AllowAllProjects);
            Assert.False(agent.WorkspaceToolAccess.CanWriteStorage);
            Assert.Equal(0, wire.ExternalRequests);
            evidence.Observations["refusal"] = new { attack, unexpectedSourceRequests = wire.ExternalRequests, canariesUnchanged = true };
            await oracle.AssertCleanAsync();
            evidence.Passed = true;
        } finally {
            await evidence.RecordLatestRunUnlessPassedAsync(host);
            await evidence.WriteAsync(host);
        }
    }

    private static AgentResponseFixture.ScriptedCall Call(string tool, object arguments) => new(tool, JsonSerializer.Serialize(arguments));

    private static string DecodeToolContent(JsonElement request)
        => Encoding.UTF8.GetString(Convert.FromBase64String(FindToolString(request, "base64Data")));

    private static string FindToolString(JsonElement request, string property, string? title = null) {
        foreach (var output in request.GetProperty("input").EnumerateArray().Reverse()) {
            if (output.TryGetProperty("type", out var type) && type.GetString() == "function_call_output" &&
                output.TryGetProperty("output", out var content) && Find(content) is { } found) {
                return found;
            }
        }
        throw new InvalidOperationException("The scripted model did not receive the expected owner tool output.");

        string? Find(JsonElement element) {
            if (element.ValueKind == JsonValueKind.Object) {
                if (element.TryGetProperty(property, out var candidate) && candidate.ValueKind == JsonValueKind.String &&
                    (title is null || element.TryGetProperty("title", out var actualTitle) && actualTitle.GetString() == title)) {
                    return candidate.GetString();
                }
                foreach (var child in element.EnumerateObject()) {
                    if (Find(child.Value) is { } found) {
                        return found;
                    }
                }
            } else if (element.ValueKind == JsonValueKind.Array) {
                foreach (var child in element.EnumerateArray()) {
                    if (Find(child) is { } found) {
                        return found;
                    }
                }
            } else if (element.ValueKind == JsonValueKind.String && element.GetString() is { } text &&
                (text.StartsWith('{') || text.StartsWith('['))) {
                using var nested = JsonDocument.Parse(text);
                return Find(nested.RootElement);
            }
            return null;
        }
    }
}
