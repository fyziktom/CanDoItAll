using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
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
    public async Task Final_image_simple_chat_saves_default_and_nondefault_and_persists_exact_transcripts() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var profile = await ImportedResponsesAsync(fixture);
        var models = profile.SuggestedModels.Select(profile.GetModelDisplayName).Distinct(StringComparer.Ordinal).ToArray();
        var defaultName = profile.GetModelDisplayName(profile.DefaultModel);
        var alternate = profile.ModelCatalog.First(model => model.Id != profile.DefaultModel && profile.SuggestedModels.Contains(model.Id));
        foreach (var (route, label, suffix) in new[] {
            (profile.DefaultModel, $"Provider default ({defaultName})", "default"),
            (alternate.Id, alternate.DisplayName, "nondefault")
        }) {
            var marker = "PP2C_CHAT_" + Guid.NewGuid().ToString("N");
            var answer = "Native transcript " + marker;
            await fixture.ScriptAsync(profile.GetModelDisplayName(route), marker, TextStep(answer));
            await SharedProviderMetadataUiChecks.ExerciseSimpleChatAsync(fixture.Page, fixture.Settings.Clients[0], profile.Name,
                defaultName, models, label, fixture.Settings.Evidence, marker, answer, marker);
            var definitions = await fixture.GetAsync("api/llm-chats?take=100");
            var definition = Assert.Single(definitions.GetProperty("items").EnumerateArray(), item => item.GetProperty("name").GetString() == "UI shared catalog " + marker);
            Assert.Equal(profile.Id, definition.GetProperty("providerProfileId").GetGuid());
            Assert.Equal(route, definition.GetProperty("model").GetString());
            var conversations = await fixture.GetAsync($"api/llm-conversations?definitionId={definition.GetProperty("id").GetGuid():D}");
            var conversation = Assert.Single(conversations.GetProperty("items").EnumerateArray());
            var detail = await fixture.GetAsync($"api/llm-conversations/{conversation.GetProperty("id").GetGuid():D}");
            var assistant = Assert.Single(detail.GetProperty("messages").EnumerateArray(), item => item.GetProperty("role").GetString() == "assistant");
            Assert.Equal(answer, assistant.GetProperty("content").GetString());
            Assert.Equal(route, assistant.GetProperty("model").GetString());
            var operation = await fixture.GetAsync($"api/llm-chat-operations/{assistant.GetProperty("turnId").GetGuid():D}");
            Assert.Equal("succeeded", operation.GetProperty("status").GetString());
            Assert.Equal(assistant.GetProperty("entryId").GetGuid(), operation.GetProperty("assistantMessage").GetProperty("entryId").GetGuid());
            Assert.All(operation.GetProperty("invocationAttempts").EnumerateArray(), attempt => Assert.Equal(route, attempt.GetProperty("model").GetString()));
            await fixture.AssertScriptCompleteAsync(1);
            await AssertRoutedAsync(fixture, profile, route, marker);
            await AssertCanonicalHistoryAsync(fixture, profile, route, CanDoItAll.AgentFramework.ProviderHistory.HistorySourceKind.SimpleChat,
                assistant.GetProperty("turnId").GetGuid(), answer, "simple-chat-" + suffix);
            await fixture.EvidenceAsync("consumer-simple-chat-" + suffix, new {
                ProviderId = profile.Id, Route = route, UpstreamModel = profile.GetModelDisplayName(route),
                DefinitionId = definition.GetProperty("id").GetGuid(), ConversationId = conversation.GetProperty("id").GetGuid(),
                OperationId = assistant.GetProperty("turnId").GetGuid(), EntryId = assistant.GetProperty("entryId").GetGuid(),
                Sha256 = SharedProviderConsumerFixture.Hash(answer)
            });
            await RenameAndArchiveSimpleChatAsync(fixture, detail, marker, suffix);
        }
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public Task Final_image_agent_reads_hidden_content_and_creates_attaches_reads_downloads_with_exact_approvals() =>
        RunFileConsumerAsync(planning: false);

    private static async Task RunFileConsumerAsync(bool planning) {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var profile = await ImportedResponsesAsync(fixture);
        var marker = "PP2C_FILES_" + Guid.NewGuid().ToString("N");
        var projectName = "PP2C files " + marker;
        var projectId = planning
            ? await CreatePlanningProjectAsync(fixture, projectName)
            : await fixture.PostAsync<Guid>("api/projects", new ProjectEditorModel { Name = projectName });
        var siblingId = await fixture.PostAsync<Guid>("api/projects", new ProjectEditorModel { Name = "PP2C untouched " + marker });
        var parent = planning
            ? await CreatePlanningTaskAsync(fixture, projectId)
            : await fixture.PostAsync<ProjectStructureNodeSummary>($"api/project-structure/projects/{projectId:D}/nodes",
                new ProjectStructureNodeCreateInput(ProjectObjectType.ProjectBlock, "Selected files", "", "", $"project:{projectId:D}", X: 450, Y: 220));
        var hiddenContent = "Hidden file canary " + Guid.NewGuid().ToString("N");
        var siblingContent = "Unchanged sibling " + Guid.NewGuid().ToString("N");
        var seed = await SeedAssetAsync(fixture, projectId, parent.Id, "seed.txt", hiddenContent);
        var sibling = await SeedAssetAsync(fixture, siblingId, $"project:{siblingId:D}", "canary.txt", siblingContent);
        var alternate = profile.ModelCatalog.First(model => model.Id != profile.DefaultModel && profile.SuggestedModels.Contains(model.Id));
        var agentId = await CreateFileAgentAsync(fixture, profile, alternate.Id, marker, projectId, projectName, planning: planning);
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectFileNodeAsync(fixture.Page, parent.Id, parent.Title);
        var chat = await OpenFloatingChatFromCatalogAsync(fixture.Page, "project-structure-agents-toggle", agentId,
            Path.Combine(fixture.Settings.Evidence, "consumer-file-catalog-failure.png"));
        await fixture.ScriptAsync(alternate.DisplayName, marker, ReadStep(projectId, seed.Id), new { kind = "echo_tool_content" });
        var read = await FileTurnAsync(fixture, chat, agentId, projectId, parent.Id, "Read the selected seed asset and report the actual file content.");
        Assert.Contains(hiddenContent, await chat.InnerTextAsync(), StringComparison.Ordinal);
        await fixture.AssertScriptCompleteAsync(2);
        var expected = new FileApprovalIntent(projectId, parent.Id, "PP2C produced file", "# Native file\n" + marker,
            "roundtrip.md", "text/markdown", $"pp2c/{marker}/roundtrip.md");
        await fixture.ScriptAsync(alternate.DisplayName, marker,
            ToolsStep(Call(ToolContractCatalog.WorkspaceWriteFile, new { path = expected.WorkspacePath, content = expected.Content, overwrite = false })),
            ToolsStep(AttachCall(expected)), TextStep("The native file was written and attached."));
        var write = await FileTurnAsync(fixture, chat, agentId, projectId, parent.Id, "Write the agreed new file and attach it to the selected node.", expected);
        await fixture.AssertScriptCompleteAsync(3);
        foreach (var tool in new[] { ToolContractCatalog.WorkspaceWriteFile, ProjectStructureToolPolicy.ProjectStructureAssetCreate }) {
            Assert.Contains(write.GetProperty("receipts").EnumerateArray(), item => item.GetProperty("toolName").GetString() == tool &&
                item.GetProperty("invocationOutcome").GetInt32() == (int)AgentToolInvocationOutcome.Succeeded);
        }
        var tree = await TreeAsync(fixture, projectId);
        var created = Assert.Single(tree.Nodes, node => node.Title == expected.Title);
        Assert.Equal(parent.Id, created.ParentId);
        var stored = await AssertStoredAssetAsync(fixture, projectId, created);
        await fixture.ScriptAsync(alternate.DisplayName, marker, ReadStep(projectId, created.Id), new { kind = "echo_tool_content" });
        var readback = await FileTurnAsync(fixture, chat, agentId, projectId, parent.Id, "Read metadata and content of the new attachment.");
        await fixture.AssertScriptCompleteAsync(2);
        Assert.Contains(marker, await chat.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Equal(expected.Content, await ContentAsync(fixture, projectId, created.Id));
        Assert.Equal(hiddenContent, await ContentAsync(fixture, projectId, seed.Id));
        Assert.Equal(siblingContent, await ContentAsync(fixture, siblingId, sibling.Id));
        if (planning) {
            await UpdatePlanningTaskAsync(fixture, chat, profile, alternate.Id, marker, agentId, projectId, parent);
        }
        var denied = expected with { Title = "PP2C explicitly denied attachment" };
        await fixture.ScriptAsync(alternate.DisplayName, marker, ToolsStep(AttachCall(denied)), TextStep("The proposed attachment was denied."));
        var denial = await FileTurnAsync(fixture, chat, agentId, projectId, parent.Id, "Propose the second attachment for an explicit operator denial.", denied, reject: true);
        await fixture.AssertScriptCompleteAsync(2);
        Assert.Contains(denial.GetProperty("proposals").EnumerateArray(), item =>
            item.GetProperty("approvalStatus") is { ValueKind: JsonValueKind.Number } status && status.GetInt32() == (int)ExecutionApprovalStatus.Rejected);
        Assert.DoesNotContain((await TreeAsync(fixture, projectId)).Nodes, node => node.Title == denied.Title);
        await PreviewAndDownloadAsync(fixture, projectId, projectName, created, expected.Content);
        if (planning) {
            await AssertPlanningConsumersAsync(fixture, projectId, parent);
            Assert.Equal(siblingContent, await ContentAsync(fixture, siblingId, sibling.Id));
        }
        await AssertRoutedAsync(fixture, profile, alternate.Id, marker);
        await AssertCanonicalHistoryAsync(fixture, profile, alternate.Id, CanDoItAll.AgentFramework.ProviderHistory.HistorySourceKind.AgentConversation,
            readback.GetProperty("runId").GetGuid(), expected.Content, "agent-files");
        await fixture.EvidenceAsync("consumer-files", new {
            projectId, parent.Id, AgentId = agentId, ProviderId = profile.Id, Route = alternate.Id, UpstreamModel = alternate.DisplayName,
            CreatedNodeId = created.Id, stored.MediaRelativePath, Sha256 = SharedProviderConsumerFixture.Hash(expected.Content),
            HiddenCanarySha256 = SharedProviderConsumerFixture.Hash(hiddenContent), SiblingSha256 = SharedProviderConsumerFixture.Hash(siblingContent),
            ReadRun = read.GetProperty("runId"), WriteRun = write.GetProperty("runId"), ReadbackRun = readback.GetProperty("runId"), DeniedRun = denial.GetProperty("runId")
        });
    }

    private static async Task<ProviderProfile> ImportedResponsesAsync(SharedProviderConsumerFixture fixture) =>
        Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api), profile => profile.IsSourceManaged && profile.Name == "PP2 OpenAI default");

    private static object Call(string name, object arguments) => new { name, arguments };
    private static object ToolsStep(params object[] calls) => new { kind = "tool_calls", tool_calls = calls };
    private static object TextStep(string text) => new { kind = "text", text };
    private static object ReadStep(Guid projectId, string nodeId) => ToolsStep(
        Call(ProjectStructureToolPolicy.ProjectStructureAssetGet, new { projectId, nodeId }),
        Call(ProjectStructureToolPolicy.ProjectStructureAssetContentGet, new { projectId, nodeId }));
    private static object AttachCall(FileApprovalIntent expected) => Call(ProjectStructureToolPolicy.ProjectStructureAssetCreate,
        new { projectId = expected.ProjectId, request = new { objectType = "File", title = expected.Title, parentNodeKey = expected.ParentId,
            sourceWorkspacePath = expected.WorkspacePath, sourceFileName = expected.FileName, sourceContentType = expected.ContentType } });

    private static Task<ProjectStructureNodeSummary> SeedAssetAsync(SharedProviderConsumerFixture fixture, Guid projectId, string parent, string name, string content) =>
        fixture.PostAsync<ProjectStructureNodeSummary>($"api/project-structure/projects/{projectId:D}/assets", new {
            objectType = "File", title = name, subtitle = "", notes = "", parentNodeKey = parent,
            media = new { fileName = name, contentType = "text/plain", base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) }
        });

    private static Task<ProjectStructureReadResponse> TreeAsync(SharedProviderConsumerFixture fixture, Guid projectId, bool includeMetadata = false) =>
        fixture.PostAsync<ProjectStructureReadResponse>($"api/project-structure/projects/{projectId:D}/structure/read", new { includeAssets = true, includeMetadata });

    private static async Task<string> ContentAsync(SharedProviderConsumerFixture fixture, Guid projectId, string nodeId) {
        var content = await fixture.GetAsync($"api/project-structure/projects/{projectId:D}/assets/{Uri.EscapeDataString(nodeId)}/content");
        Assert.False(content.GetProperty("base64DataOmitted").GetBoolean());
        return Encoding.UTF8.GetString(Convert.FromBase64String(content.GetProperty("base64Data").GetString()!));
    }

    private static async Task<ProjectStructureAssetDescriptor> AssertStoredAssetAsync(SharedProviderConsumerFixture fixture,
        Guid projectId, ProjectStructureNodeSummary node) {
        var stored = (await fixture.GetAsync($"api/project-structure/projects/{projectId:D}/assets/{Uri.EscapeDataString(node.Id)}"))
            .Deserialize<ProjectStructureAssetDescriptor>(SharedProviderConsumerFixture.ReadJson)!;
        Assert.Equal(projectId, stored.ProjectId);
        Assert.Equal(node.Id, stored.NodeId);
        Assert.Equal(node.ObjectType, stored.ObjectType);
        Assert.False(string.IsNullOrWhiteSpace(stored.MediaRelativePath));
        Assert.Equal(node.MediaRelativePath, stored.MediaRelativePath);
        Assert.True(stored.IsReadonly);
        Assert.Equal(stored.MediaRelativePath, Assert.Single((await TreeAsync(fixture, projectId)).Nodes, item => item.Id == node.Id).MediaRelativePath);
        return stored;
    }

    private static async Task<Guid> CreateFileAgentAsync(SharedProviderConsumerFixture fixture, ProviderProfile profile, string alternate,
        string marker, Guid projectId, string projectName, ProviderProfile? imageProvider = null, bool planning = false) {
        await fixture.NavigateAsync("/agents?tab=agents");
        await fixture.Page.GetByTestId("agents-catalog-new").ClickAsync();
        var dialog = fixture.Page.GetByTestId("agents-details-dialog").Last;
        await dialog.GetByTestId("agents-catalog-name").WaitForAsync();
        await Assertions.Expect(dialog.GetByRole(AriaRole.Tab)).ToHaveCountAsync(10);
        var name = "PP2C file agent " + marker;
        await dialog.GetByTestId("agents-catalog-name").FillAsync(name);
        await dialog.GetByTestId("agents-catalog-instructions").FillAsync(marker + ". Use the exact scoped file tools and honor every approval decision.");
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        await dialog.Locator("select").Filter(new() { Has = fixture.Page.Locator("option[value='Active']") }).SelectOptionAsync(nameof(AgentLifecycleStatus.Active));
        await dialog.GetByTestId("agents-catalog-provider").SelectOptionAsync(new SelectOptionValue { Label = profile.Name });
        await dialog.GetByTestId("agents-catalog-model-choice").SelectOptionAsync(new SelectOptionValue {
            Label = $"Provider default ({profile.GetModelDisplayName(profile.DefaultModel)})"
        });
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-auto-approval")).Not.ToBeCheckedAsync();
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Project Structure Access", Exact = true }).ClickAsync();
        await dialog.GetByTestId("agents-catalog-project-structure-read").CheckAsync();
        await dialog.GetByTestId("agents-catalog-project-structure-non-task-write").CheckAsync();
        if (planning) {
            await dialog.GetByTestId("agents-catalog-project-structure-task-write").CheckAsync();
        }
        await dialog.GetByTestId("agents-catalog-project-structure-projects").GetByLabel(projectName, new() { Exact = true }).CheckAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-project-structure-all")).Not.ToBeCheckedAsync();
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Workspace Tools", Exact = true }).ClickAsync();
        await dialog.GetByTestId("agents-catalog-workspace-profile").SelectOptionAsync(nameof(AgentWorkspaceToolProfileKind.Custom));
        await dialog.GetByTestId("agents-catalog-workspace-read").CheckAsync();
        await dialog.GetByTestId("agents-catalog-workspace-write").CheckAsync();
        if (imageProvider is not null) {
            await dialog.GetByRole(AriaRole.Tab, new() { Name = "Images", Exact = true }).ClickAsync();
            await dialog.GetByTestId("agents-catalog-image-generation-enabled").CheckAsync();
            await dialog.GetByTestId("agents-catalog-image-generation-project-assets").CheckAsync();
            await dialog.GetByTestId("agents-catalog-image-generation-provider").SelectOptionAsync(new SelectOptionValue { Label = imageProvider.Name });
            await dialog.GetByTestId("agents-catalog-image-model-choice").SelectOptionAsync(new SelectOptionValue {
                Label = $"Provider default ({imageProvider.GetModelDisplayName(imageProvider.DefaultModel)})"
            });
        }
        await dialog.GetByTestId("agents-catalog-save").ClickAsync();
        await fixture.Page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
        var agents = await fixture.GetAsync("api/agents");
        var agentId = Assert.Single(agents.EnumerateArray(), item => item.GetProperty("name").GetString() == name).GetProperty("id").GetGuid();
        var initial = await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{agentId:D}", SharedProviderConsumerFixture.Json);
        Assert.NotNull(initial);
        Assert.Equal(profile.Id, initial.ProviderProfileId);
        Assert.Empty(initial.Model);
        Assert.Equal([projectId], initial.ProjectStructureAccess.AllowedProjectIds);
        Assert.False(initial.ProjectStructureAccess.AllowAllProjects);
        Assert.False(initial.WorkspaceToolAccess.CanWriteStorage);
        Assert.Empty(initial.AllowedSecretReferences);
        await fixture.NavigateAsync($"/agents?tab=agents&agentId={agentId:D}");
        foreach (var label in new[] { "Identity", "Runtime", "Images", "Voice", "Memory", "Project Structure Access", "Workspace Tools", "Secrets", "Process Access", "Capabilities" }) {
            await dialog.GetByRole(AriaRole.Tab, new() { Name = label, Exact = true }).ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agents-catalog-save")).ToBeInViewportAsync(new() { Ratio = 1 });
        }
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        var alternateLabel = alternate == profile.DefaultModel ? $"Provider default ({profile.GetModelDisplayName(alternate)})" : profile.GetModelDisplayName(alternate);
        await dialog.GetByTestId("agents-catalog-model-choice").SelectOptionAsync(new SelectOptionValue { Label = alternateLabel });
        await dialog.GetByTestId("agents-catalog-save").ClickAsync();
        await fixture.Page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
        var saved = await fixture.Api.GetFromJsonAsync<AgentEditorModel>($"api/agents/{agentId:D}", SharedProviderConsumerFixture.Json);
        Assert.Equal(alternate == profile.DefaultModel ? string.Empty : alternate, saved!.Model);
        Assert.Equal(JsonSerializer.Serialize(initial.ProjectStructureAccess), JsonSerializer.Serialize(saved.ProjectStructureAccess));
        Assert.Equal(JsonSerializer.Serialize(initial.WorkspaceToolAccess), JsonSerializer.Serialize(saved.WorkspaceToolAccess));
        await fixture.NavigateAsync($"/agents?tab=agents&agentId={agentId:D}");
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-model-choice").Locator("option:checked")).ToHaveTextAsync(alternateLabel);
        await fixture.ScreenshotAsync("consumer-agent-saved-runtime");
        return agentId;
    }

    private static async Task<JsonElement> FileTurnAsync(SharedProviderConsumerFixture fixture, ILocator chat, Guid agentId,
        Guid projectId, string parentId, string prompt, FileApprovalIntent? expected = null, bool reject = false,
        Func<AgentToolPreparedPayload, IReadOnlySet<string>, FileProposalRefusal>? validate = null, bool planning = false) {
        var previous = (await fixture.GetAsync($"api/agents/{agentId:D}/execution-runs")).EnumerateArray().Select(run => run.GetProperty("id").GetGuid()).ToHashSet();
        await SendAsync(chat, prompt);
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        var decided = new HashSet<string>(StringComparer.Ordinal);
        var deadline = DateTimeOffset.UtcNow.AddMinutes(8);
        Guid? runId = null;
        while (DateTimeOffset.UtcNow < deadline) {
            var current = (await fixture.GetAsync($"api/agents/{agentId:D}/execution-runs")).EnumerateArray()
                .FirstOrDefault(run => !previous.Contains(run.GetProperty("id").GetGuid()));
            if (current.ValueKind == JsonValueKind.Undefined) {
                await Task.Delay(200);
                continue;
            }
            runId ??= current.GetProperty("id").GetGuid();
            Assert.Equal(runId, current.GetProperty("id").GetGuid());
            var state = (ExecutionState)current.GetProperty("state").GetInt32();
            if (state == ExecutionState.WaitingOnTool) {
                var visibleApprovals = await chat.Locator("[data-testid^='chat-approval-approve-']:enabled")
                    .EvaluateAllAsync<string[]>("elements => elements.map(element => element.dataset.testid.substring('chat-approval-approve-'.length))");
                if (!visibleApprovals.Except(decided, StringComparer.Ordinal).Any()) {
                    await Task.Delay(200);
                    continue;
                }
                var owner = await fixture.ReadAgentAsync(agentId, planning);
                Assert.Equal(runId, owner.GetProperty("runId").GetGuid());
                Assert.Equal(AgentChatTrustedSourceKinds.ProjectStructure, owner.GetProperty("sourceKind").GetString());
                Assert.Equal(projectId.ToString("D"), owner.GetProperty("sourceId").GetString());
                Assert.Equal(agentId, owner.GetProperty("session").GetProperty("agentId").GetGuid());
                var snapshot = Assert.Single(owner.GetProperty("snapshots").EnumerateArray());
                Assert.Equal(projectId, snapshot.GetProperty("projectId").GetGuid());
                Assert.Equal([parentId], snapshot.GetProperty("selectedNodeIds").EnumerateArray().Select(node => node.GetString()));
                foreach (var proposal in owner.GetProperty("proposals").EnumerateArray().Where(item =>
                    item.GetProperty("approvalStatus") is { ValueKind: JsonValueKind.Number } status && status.GetInt32() == (int)ExecutionApprovalStatus.Pending)) {
                    var approvalId = proposal.GetProperty("approvalId").GetString()!;
                    if (decided.Contains(approvalId)) {
                        continue;
                    }
                    var decision = chat.GetByTestId($"chat-approval-{(reject ? "reject" : "approve")}-{approvalId}");
                    if (!await decision.IsVisibleAsync() || !await decision.IsEnabledAsync()) {
                        continue;
                    }
                    var payload = proposal.GetProperty("payload").Deserialize<AgentToolPreparedPayload>(SharedProviderConsumerFixture.Json)!;
                    var refusal = validate?.Invoke(payload, accepted) ?? expected?.Validate(payload, accepted) ?? FileProposalRefusal.UnexpectedTool;
                    if (refusal != FileProposalRefusal.None) {
                        await chat.GetByTestId("chat-approval-reject-" + approvalId).ClickAsync();
                        Assert.Fail($"The exact native file proposal was rejected: {refusal}.");
                    }
                    await fixture.EvidenceAsync($"consumer-approval-{runId}-" + SharedProviderConsumerFixture.Hash(approvalId)[..12], owner);
                    decided.Add(approvalId);
                    await fixture.ScreenshotAsync($"consumer-approval-{runId}-{decided.Count}");
                    await decision.ClickAsync();
                    if (!reject) {
                        accepted.Add(payload.ToolName);
                    }
                }
            }
            if (state is ExecutionState.Completed or ExecutionState.Failed) {
                var owner = await fixture.ReadAgentAsync(agentId, planning);
                Assert.Equal(runId, owner.GetProperty("runId").GetGuid());
                await fixture.EvidenceAsync("consumer-run-" + runId, owner);
                Assert.Equal(ExecutionState.Completed, state);
                return owner;
            }
            await Task.Delay(200);
        }
        var finalOwner = await fixture.ReadAgentAsync(agentId, planning);
        Assert.Equal(runId, finalOwner.GetProperty("runId").GetGuid());
        if (finalOwner.GetProperty("state").GetInt32() == (int)ExecutionState.Completed) {
            await fixture.EvidenceAsync("consumer-run-" + runId, finalOwner);
            return finalOwner;
        }
        await fixture.EvidenceAsync("consumer-timeout-" + runId, finalOwner);
        throw new TimeoutException($"The owned file run {runId} did not finish.");
    }

    private static async Task PreviewAndDownloadAsync(SharedProviderConsumerFixture fixture, Guid projectId, string projectName,
        ProjectStructureNodeSummary node, string expected) {
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectFileNodeAsync(fixture.Page, node.Id, node.Title);
        await fixture.Page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = "Expand preview", Exact = true }).ClickAsync();
        await Assertions.Expect(fixture.Page.GetByRole(AriaRole.Dialog, new() { Name = "roundtrip.md file interaction", Exact = true })).ToContainTextAsync(expected.TrimStart('#', ' '));
        await fixture.ScreenshotAsync("consumer-file-preview");
        await fixture.NavigateAsync("/projects");
        await Assertions.Expect(fixture.Page.GetByTestId("projects-workspace")).ToHaveAttributeAsync("data-interactive", "true");
        await fixture.Page.GetByTestId("projects-search-input").FillAsync(projectName);
        await Assertions.Expect(fixture.Page.GetByTestId("project-card")).ToHaveCountAsync(1);
        var card = fixture.Page.GetByTestId("project-card").Filter(new() { Has = fixture.Page.GetByText(projectName, new() { Exact = true }) });
        await card.GetByTestId("project-card-files-button").ClickAsync();
        var dialog = fixture.Page.GetByTestId("project-files-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        var storedName = Path.GetFileName(node.MediaRelativePath.Replace('\\', '/'));
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Actions for " + storedName, Exact = true }).ClickAsync();
        var menu = dialog.GetByRole(AriaRole.Group, new() { Name = "Actions for " + storedName, Exact = true });
        var download = await fixture.Page.RunAndWaitForDownloadAsync(() => menu.GetByRole(AriaRole.Button, new() { Name = "Download", Exact = true }).ClickAsync());
        Assert.Equal(storedName, download.SuggestedFilename);
        Assert.Null(await download.FailureAsync());
        var path = Path.Combine(fixture.Settings.Evidence, "consumer-roundtrip-download.bin");
        await download.SaveAsAsync(path);
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
        await fixture.EvidenceAsync("consumer-file-download", new { node.Id, node.MediaRelativePath, Bytes = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) });
    }

    private static async Task AssertRoutedAsync(SharedProviderConsumerFixture fixture, ProviderProfile profile, string route, string marker) {
        var captures = await fixture.ReadCapturesAsync();
        var calls = captures.GetProperty("requests").EnumerateArray().Where(request => request.GetProperty("body").GetString()!.Contains(marker, StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(calls);
        foreach (var call in calls) {
            Assert.Equal(profile.GetModelDisplayName(route), CapturedSourceModel(call));
            Assert.Equal("/v1/responses", call.GetProperty("path").GetString());
        }
        await fixture.EvidenceAsync("consumer-route-" + marker, new { profile.Id, Route = route, SourceModel = profile.GetModelDisplayName(route),
            Requests = calls.Select(call => new {
                Sequence = call.GetProperty("sequence"), Path = call.GetProperty("path"), Status = call.GetProperty("response_status_code"),
                BodyTruncated = call.GetProperty("body_truncated"), CapturedBodySha256 = SharedProviderConsumerFixture.Hash(call.GetProperty("body").GetString()!)
            }) });
    }

    private static string CapturedSourceModel(JsonElement capture) {
        var body = capture.GetProperty("body").GetString()!;
        if (!capture.GetProperty("body_truncated").GetBoolean()) {
            using var document = JsonDocument.Parse(body);
            return Assert.IsType<string>(document.RootElement.GetProperty("model").GetString());
        }
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(body), isFinalBlock: false, state: default);
        while (reader.Read()) {
            if (reader.CurrentDepth == 1 && reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("model")) {
                Assert.True(reader.Read(), "The captured source-model value must be complete.");
                Assert.Equal(JsonTokenType.String, reader.TokenType);
                return Assert.IsType<string>(reader.GetString());
            }
        }
        throw new Xunit.Sdk.XunitException("The complete source-model field is missing from the bounded request capture.");
    }
}
