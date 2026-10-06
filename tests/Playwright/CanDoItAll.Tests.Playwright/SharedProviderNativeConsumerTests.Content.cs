using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class SharedProviderNativeConsumerTests {
    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_WB4_operator_creates_uploads_edits_and_exports_exact_native_content() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var name = "WB4 content " + Guid.NewGuid().ToString("N");
        var projectId = await CreatePlanningProjectAsync(fixture, name);
        var parentId = $"project:{projectId:D}";
        var accepted = new List<object>();
        ProjectStructureNodeSummary? editable = null;
        foreach (var (subtype, fileName, content) in new[] {
            ("text", "native.txt", "Exact native content\nŽluťoučký kůň\n"),
            ("json", "native.json", "{\"native\":73,\"owner\":\"WB4\"}"),
            ("markdown", "native.md", "# Accepted native Markdown\n\nContent is separate from Notes.\n"),
            ("mermaid", "native.mmd", "flowchart LR\n A[Original bytes] --> B[Governed viewer]\n"),
            ("log", "native.log", "2026-10-06T00:00:00Z accepted native log\n")
        }) {
            await OpenContentToolboxAsync(fixture, projectId, "add-file-" + subtype);
            var form = fixture.Page.GetByTestId("project-structure-text-asset-create-content");
            await form.GetByTestId("project-structure-text-asset-title").FillAsync("WB4 " + subtype);
            await form.GetByTestId("project-structure-text-asset-file-name").FillAsync(fileName);
            await form.GetByTestId("project-structure-text-asset-notes").FillAsync("Supplemental notes never replace file bytes.");
            if (subtype == "json") {
                var before = await TreeAsync(fixture, projectId);
                await form.GetByTestId("project-structure-text-asset-content").FillAsync("{ invalid JSON");
                await form.GetByTestId("project-structure-text-asset-submit").ClickAsync();
                await Assertions.Expect(form.GetByTestId("project-structure-text-asset-validation-error")).ToBeVisibleAsync();
                Assert.Equal(before.Nodes.Count, (await TreeAsync(fixture, projectId)).Nodes.Count);
            }
            await form.GetByTestId("project-structure-text-asset-content").FillAsync(content);
            await form.GetByTestId("project-structure-text-asset-submit").FocusAsync();
            await fixture.Page.Keyboard.PressAsync("Enter");
            await Assertions.Expect(form).Not.ToBeVisibleAsync();
            var node = Assert.Single((await TreeAsync(fixture, projectId, includeMetadata: true)).Nodes, node => node.Title == "WB4 " + subtype);
            Assert.Equal(parentId, node.ParentId);
            Assert.Equal(subtype, node.ObjectSubtype);
            var bytes = await ContentBytesAsync(fixture, projectId, node.Id);
            Assert.Equal(Encoding.UTF8.GetBytes(content), bytes);
            await AssertStoredAssetAsync(fixture, projectId, node);
            accepted.Add(new { node.Id, node.ParentId, node.MediaRelativePath, FileName = fileName, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) });
            await fixture.EvidenceAsync("wb4-operator-accepted-text", new { projectId, accepted });
            if (subtype == "text") {
                editable = node;
            }
        }
        await OpenContentToolboxAsync(fixture, projectId, "add-file-text");
        var upload = fixture.Page.GetByTestId("project-structure-text-asset-create-content");
        await upload.GetByTestId("project-structure-text-asset-source-upload").ClickAsync();
        var uploaded = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Native upload\r\nŽluťoučký\r\n")).ToArray();
        await upload.GetByTestId("project-structure-text-asset-title").FillAsync("WB4 uploaded bytes");
        await upload.GetByTestId("project-structure-text-asset-upload-input").SetInputFilesAsync(new FilePayload {
            Name = "upload.txt", MimeType = "text/plain", Buffer = uploaded
        });
        await upload.GetByTestId("project-structure-text-asset-submit").ClickAsync();
        await Assertions.Expect(upload).Not.ToBeVisibleAsync();
        var uploadNode = Assert.Single((await TreeAsync(fixture, projectId)).Nodes, node => node.Title == "WB4 uploaded bytes");
        Assert.Equal(uploaded, await ContentBytesAsync(fixture, projectId, uploadNode.Id));
        await fixture.EvidenceAsync("wb4-operator-upload", new { projectId, uploadNode.Id, uploadNode.ParentId,
            Bytes = uploaded.Length, Sha256 = Convert.ToHexString(SHA256.HashData(uploaded)) });

        Assert.NotNull(editable);
        await OpenContentPreviewAsync(fixture, projectId, editable);
        var interaction = fixture.Page.GetByTestId("project-structure-direct-file-interaction");
        await interaction.GetByTestId("interaction-mode-edit").ClickAsync();
        const string saved = "Exactly one native Save\nRevision checked by the storage owner.\n";
        await interaction.GetByTestId("interaction-text-editor").FillAsync(saved);
        await Assertions.Expect(interaction.GetByTestId("interaction-save-status")).ToHaveTextAsync("Unsaved changes");
        await interaction.GetByTestId("interaction-save").ClickAsync();
        await Assertions.Expect(interaction.GetByTestId("interaction-save-status")).ToHaveTextAsync("Saved");
        Assert.Equal(Encoding.UTF8.GetBytes(saved), await ContentBytesAsync(fixture, projectId, editable.Id));
        await fixture.EvidenceAsync("wb4-operator-save", new { projectId, editable.Id, Status = await interaction.GetByTestId("interaction-save-status").InnerTextAsync(), Sha256 = SharedProviderConsumerFixture.Hash(saved) });
        await fixture.ScreenshotAsync("wb4-operator-saved-content");
        await fixture.Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await fixture.Page.GetByTestId("project-structure-files-toggle").ClickAsync();
        var collection = fixture.Page.GetByTestId("project-structure-file-browser-window");
        await Assertions.Expect(collection).ToContainTextAsync(Path.GetFileName(editable.MediaRelativePath!));
        await fixture.ScreenshotAsync("wb4-native-collection");

        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectContentNodeAsync(fixture.Page, parentId, name);
        await fixture.Page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = "Summary", Exact = true }).ClickAsync();
        var summary = fixture.Page.GetByTestId("content-progress-summary");
        var beforeExports = await TreeAsync(fixture, projectId);
        foreach (var (button, subtype) in new[] { ("Export XLSX", "excel"), ("Export Gantt", "mermaid") }) {
            await summary.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();
            var title = name + (subtype == "excel" ? " progress workbook" : " gantt");
            var output = await WaitContentNodeAsync(fixture, projectId, title,
                node => node.ObjectSubtype == subtype && !beforeExports.Nodes.Any(original => original.Id == node.Id));
            var bytes = await ContentBytesAsync(fixture, projectId, output.Id);
            Assert.Equal(parentId, output.ParentId);
            if (subtype == "excel") {
                using var stream = new MemoryStream(bytes);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                using var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
                var xml = XDocument.Load(sheet).ToString();
                Assert.Contains("WB4 text", xml, StringComparison.Ordinal);
                Assert.Contains(name, xml, StringComparison.Ordinal);
            } else {
                Assert.StartsWith("gantt", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
                Assert.Contains("WB4 text", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            }
            await fixture.EvidenceAsync("wb4-stored-export-" + subtype, new { projectId, output.Id, output.ParentId, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) });
        }
        await fixture.ScreenshotAsync("wb4-progress-summary");
        await summary.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await SelectContentNodeAsync(fixture.Page, parentId, name);
        await fixture.Page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = "Export image", Exact = true }).ClickAsync();
        var canvasImage = await WaitContentNodeAsync(fixture, projectId, name + " mindmap image", node => !string.IsNullOrEmpty(node.MediaRelativePath));
        var canvasBytes = await ContentBytesAsync(fixture, projectId, canvasImage.Id);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, canvasBytes.Take(8));
        await fixture.EvidenceAsync("wb4-canvas-image", new { projectId, canvasImage.Id, canvasImage.ParentId, Bytes = canvasBytes.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(canvasBytes)) });
        await OpenContentPreviewAsync(fixture, projectId, canvasImage);
        var rendered = fixture.Page.GetByTestId("project-structure-direct-file-interaction").Locator("img");
        await Assertions.Expect(rendered).ToBeVisibleAsync();
        await rendered.EvaluateAsync("image => image.decode()");
        Assert.True(await rendered.EvaluateAsync<bool>("image => image.naturalWidth > 100 && image.naturalHeight > 100"));
        await fixture.ScreenshotAsync("wb4-native-canvas-image");
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_WB4_recording_scaffold_and_three_explicit_analyses_preserve_native_transcript() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var provider = await ImportedResponsesAsync(fixture);
        var projectId = await CreatePlanningProjectAsync(fixture, "WB4 transcript " + Guid.NewGuid().ToString("N"));
        var recording = await fixture.PostAsync<ProjectStructureNodeSummary>($"api/project-structure/projects/{projectId:D}/nodes",
            new ProjectStructureNodeCreateInput(ProjectObjectType.Recording, "WB4 recording", "Synthetic recording context", "", $"project:{projectId:D}"));
        var beforeCalls = (await fixture.ReadCapturesAsync()).GetProperty("requests").GetArrayLength();
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectContentNodeAsync(fixture.Page, recording.Id, recording.Title);
        await fixture.Page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = "Create transcript", Exact = true }).ClickAsync();
        var scaffold = await WaitContentNodeAsync(fixture, projectId, "WB4 recording transcript", _ => true);
        Assert.Equal(recording.Id, scaffold.ParentId);
        Assert.Equal(beforeCalls, (await fixture.ReadCapturesAsync()).GetProperty("requests").GetArrayLength());
        var marker = "WB4_TRANSCRIPT_" + Guid.NewGuid().ToString("N");
        var metadata = System.Text.Json.Nodes.JsonNode.Parse(scaffold.MetadataJson)!.AsObject();
        metadata["transcript"]!["transcriptText"] = "Synthetic exact transcript " + marker;
        metadata["futureContent"] = "Preserved unknown metadata";
        await fixture.PostAsync<ProjectStructureNodeSummary>($"api/project-structure/projects/{projectId:D}/nodes/{Uri.EscapeDataString(scaffold.Id)}/metadata",
            new ProjectStructureNodeMetadataInput(metadata.ToJsonString()));
        await fixture.EvidenceAsync("wb4-transcript-scaffold", new { projectId, recording.Id, TranscriptId = scaffold.Id, ExternalCalls = 0 });
        foreach (var (button, field, action) in new[] {
            ("Summarize", "summaryText", ProjectLlmActionKind.Summarize),
            ("Find my tasks", "myTasksText", ProjectLlmActionKind.FindMyTasks),
            ("Find others delivery to me", "othersDeliveriesText", ProjectLlmActionKind.FindOthersDeliveries)
        }) {
            await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
            await ReadyFileCanvasAsync(fixture.Page);
            await SelectContentNodeAsync(fixture.Page, scaffold.Id, scaffold.Title);
            var actionButton = fixture.Page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = button, Exact = true });
            await actionButton.ClickAsync();
            var confirmation = fixture.Page.GetByTestId("content-transcript-confirmation");
            await Assertions.Expect(confirmation).ToContainTextAsync("it does not create tasks");
            var beforeCancel = (await fixture.ReadCapturesAsync()).GetProperty("requests").GetArrayLength();
            await confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            Assert.Equal(beforeCancel, (await fixture.ReadCapturesAsync()).GetProperty("requests").GetArrayLength());
            await actionButton.ClickAsync();
            await confirmation.Locator("select").SelectOptionAsync(new SelectOptionValue { Label = provider.Name });
            var answer = $"{action}: exact analysis {marker}";
            await fixture.ScriptAsync(provider.GetModelDisplayName(provider.DefaultModel), marker, TextStep(answer));
            await confirmation.GetByRole(AriaRole.Button, new() { Name = "Send request", Exact = true }).ClickAsync();
            await Assertions.Expect(confirmation).ToContainTextAsync("was saved for the original transcript");
            await fixture.AssertScriptCompleteAsync(1);
            var tree = await TreeAsync(fixture, projectId, includeMetadata: true);
            var current = Assert.Single(tree.Nodes, node => node.Id == scaffold.Id);
            var stored = System.Text.Json.Nodes.JsonNode.Parse(current.MetadataJson)!;
            Assert.Equal(answer, stored["transcript"]![field]!.GetValue<string>());
            Assert.Equal("Preserved unknown metadata", stored["futureContent"]!.GetValue<string>());
            Assert.DoesNotContain(tree.Nodes, node => node.ObjectType == ProjectObjectType.WorkItem);
            await AssertRoutedAsync(fixture, provider, provider.DefaultModel, marker);
            await fixture.EvidenceAsync("wb4-transcript-" + action, new { projectId, current.Id, current.ParentId, current.MetadataJson,
                Action = action, ProviderId = provider.Id, Route = provider.DefaultModel, ExplicitRequests = 1, CreatedTasks = 0 });
            await fixture.ScreenshotAsync("wb4-transcript-" + action);
        }
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_WB4_both_clients_generate_default_and_nondefault_through_native_content_controls() {
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var client in Enum.GetValues<SharedProviderConsumerClient>()) {
            await using var fixture = await SharedProviderConsumerFixture.StartAsync(client);
            var provider = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api),
                profile => profile.Name == "PP2 OpenAI image generation" && profile.IsSourceManaged);
            var alternate = provider.ModelCatalog.First(model => model.Id != provider.DefaultModel);
            var projectName = "WB4 native images " + client + " " + Guid.NewGuid().ToString("N");
            var projectId = await CreatePlanningProjectAsync(fixture, projectName);
            foreach (var (useDefault, format) in new[] { (true, "Png"), (false, "Jpeg"), (false, "Webp") }) {
                var marker = "WB4_IMAGE_" + Guid.NewGuid().ToString("N");
                var route = useDefault ? provider.DefaultModel : alternate.Id;
                var model = provider.GetModelDisplayName(route);
                var title = "WB4 " + (useDefault ? "default" : "alternate " + format);
                await OpenContentToolboxAsync(fixture, projectId, "generate-image-asset");
                var form = fixture.Page.GetByTestId("content-image-dialog");
                await form.GetByTestId("content-image-provider").SelectOptionAsync(new SelectOptionValue { Label = provider.Name });
                await Assertions.Expect(form.GetByTestId("content-image-model").Locator("option").First).ToContainTextAsync(provider.GetModelDisplayName(provider.DefaultModel));
                if (!useDefault) {
                    await form.GetByTestId("content-image-model").SelectOptionAsync(new SelectOptionValue { Label = alternate.DisplayName });
                    await form.Locator("select").Last.SelectOptionAsync(new SelectOptionValue { Label = format });
                }
                await form.GetByTestId("content-image-title").FillAsync(title);
                await form.GetByTestId("content-image-prompt").FillAsync("A harmless geometric study " + marker);
                await form.GetByTestId("content-image-submit").ClickAsync();
                var node = await WaitContentNodeAsync(fixture, projectId, title, node =>
                    ProjectObjectMetadataSerializer.Parse(node.MetadataJson).DeferredCompletion?.State == ProjectStructureDeferredNodeCompletionState.Completed);
                var bytes = await ContentBytesAsync(fixture, projectId, node.Id);
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                hashes.Add(hash);
                var captures = (await fixture.ReadCapturesAsync()).GetProperty("requests").EnumerateArray();
                var capture = Assert.Single(captures, request => request.GetProperty("path").GetString() == "/v1/images/generations" &&
                    request.GetProperty("body").GetString()!.Contains(marker, StringComparison.Ordinal));
                using var request = JsonDocument.Parse(capture.GetProperty("body").GetString()!);
                Assert.Equal(model, request.RootElement.GetProperty("model").GetString());
                Assert.NotEqual(model, route);
                Assert.Equal($"project:{projectId:D}", node.ParentId);
                await fixture.EvidenceAsync($"wb4-image-{client}-{format}", new { projectId, node, provider.Id, ClientRoute = route, UpstreamModel = model,
                    Bytes = bytes.Length, Sha256 = hash, CaptureCount = 1, Receipt = await form.GetByTestId("content-image-receipt").InnerTextAsync() });
                await form.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
                await OpenContentPreviewAsync(fixture, projectId, node);
                var image = fixture.Page.GetByTestId("project-structure-direct-file-interaction").Locator("img");
                await Assertions.Expect(image).ToBeVisibleAsync();
                await image.EvaluateAsync("image => image.decode()");
                Assert.True(await image.EvaluateAsync<bool>("image => image.naturalWidth > 0 && image.naturalHeight > 0"));
                await fixture.ScreenshotAsync($"wb4-image-preview-{client}-{format}");
                Assert.Equal(bytes, await ContentBytesAsync(fixture, projectId, node.Id));
            }
        }
        Assert.Equal(3, hashes.Count);
    }

    private static async Task SelectContentNodeAsync(IPage page, string nodeId, string title) {
        foreach (var testId in new[] { "project-structure-toolbox-window", "project-structure-file-browser-window", "project-structure-signals-window" }) {
            var window = page.GetByTestId(testId);
            if (await window.IsVisibleAsync()) {
                await window.GetByRole(AriaRole.Button, new() { Name = "Hide window", Exact = true }).ClickAsync();
            }
        }
        await SelectFileNodeAsync(page, nodeId, title);
        await page.GetByTestId("project-structure-object-index-window")
            .GetByRole(AriaRole.Button, new() { Name = "Hide window", Exact = true }).ClickAsync();
    }

    private static async Task OpenContentToolboxAsync(SharedProviderConsumerFixture fixture, Guid projectId, string action) {
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        var root = Assert.Single((await TreeAsync(fixture, projectId)).Nodes, node => node.Id == $"project:{projectId:D}");
        await SelectContentNodeAsync(fixture.Page, root.Id, root.Title);
        var toolbox = fixture.Page.GetByTestId("project-structure-standard-blocks-toolbox");
        if (!await toolbox.IsVisibleAsync()) {
            await fixture.Page.GetByTestId("project-structure-toolbox-toggle").ClickAsync();
        }
        if (await toolbox.GetByTestId("project-structure-toolbox-" + action).CountAsync() == 0) {
            await toolbox.GetByTestId("project-structure-toolbox-group-assets").ClickAsync();
        }
        await toolbox.GetByTestId("project-structure-toolbox-" + action).ClickAsync();
    }

    private static async Task OpenContentPreviewAsync(SharedProviderConsumerFixture fixture, Guid projectId, ProjectStructureNodeSummary node) {
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectContentNodeAsync(fixture.Page, node.Id, node.Title);
        await fixture.Page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = "Expand preview", Exact = true }).ClickAsync();
        await Assertions.Expect(fixture.Page.GetByTestId("project-structure-direct-file-interaction")).ToBeVisibleAsync();
    }

    private static async Task<byte[]> ContentBytesAsync(SharedProviderConsumerFixture fixture, Guid projectId, string nodeId) {
        var content = await fixture.GetAsync($"api/project-structure/projects/{projectId:D}/assets/{Uri.EscapeDataString(nodeId)}/content");
        Assert.Equal(nodeId, content.GetProperty("asset").GetProperty("nodeId").GetString());
        return Convert.FromBase64String(content.GetProperty("base64Data").GetString()!);
    }

    private static async Task<ProjectStructureNodeSummary> WaitContentNodeAsync(SharedProviderConsumerFixture fixture, Guid projectId, string title,
        Func<ProjectStructureNodeSummary, bool> ready) {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline) {
            var nodes = (await TreeAsync(fixture, projectId, includeMetadata: true)).Nodes.Where(node => node.Title == title).ToArray();
            Assert.InRange(nodes.Length, 0, 1);
            if (nodes.Length == 1 && ready(nodes[0])) {
                return nodes[0];
            }
            await Task.Delay(150);
        }
        throw new TimeoutException("The original content operation did not reach its expected observable native state. Inspect its receipt; do not resubmit.");
    }
}
