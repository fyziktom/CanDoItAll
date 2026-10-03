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
    private const string FixturePng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jmioAAAAASUVORK5CYII=";

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Final_image_native_generation_attachment_download_and_vision_preserve_real_png_bytes() {
        await using var fixture = await SharedProviderConsumerFixture.StartAsync();
        var profile = await ImportedResponsesAsync(fixture);
        var images = Assert.Single(await SharedProviderNativeDefaultsUiTests.ProfilesAsync(fixture.Api),
            item => item.Name == "PP2 OpenAI image generation" && item.IsSourceManaged);
        var marker = "PP2C_MEDIA_" + Guid.NewGuid().ToString("N");
        var projectName = "PP2C media " + marker;
        var projectId = await fixture.PostAsync<Guid>("api/projects", new ProjectEditorModel { Name = projectName });
        var parentId = $"project:{projectId:D}";
        var agentId = await CreateFileAgentAsync(fixture, profile, profile.DefaultModel, marker, projectId, projectName, images);
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectFileNodeAsync(fixture.Page, parentId, projectName);
        var chat = await OpenFloatingChatFromCatalogAsync(fixture.Page, "project-structure-agents-toggle", agentId,
            Path.Combine(fixture.Settings.Evidence, "consumer-media-catalog.png"));
        var path = $"pp2c/{marker}/generated.png";
        var title = "PP2C generated image";
        var imageRequest = new ImageGenerationCreateInput("One harmless blue square " + marker, path,
            images.Id, images.DefaultModel, "1024x1024", "low", "png");
        var imageArguments = new { request = imageRequest };
        var assetArguments = new { projectId, request = new {
            objectType = nameof(ProjectObjectType.ImageAsset), title, parentNodeKey = parentId,
            sourceWorkspacePath = path, sourceFileName = "generated.png", sourceContentType = "image/png"
        } };
        await fixture.ScriptAsync(profile.GetModelDisplayName(profile.DefaultModel), marker,
            ToolsStep(Call(ImageGenerationToolPolicy.ImageGenerationCreate, imageArguments)),
            ToolsStep(Call(ProjectStructureToolPolicy.ProjectStructureAssetCreate, assetArguments)), TextStep("The exact image was generated and attached."));
        var run = await FileTurnAsync(fixture, chat, agentId, projectId, parentId,
            "Generate the agreed harmless image and attach the resulting workspace file to the selected project.",
            validate: (payload, accepted) => ValidateExactMediaProposal(payload, accepted, imageArguments, assetArguments));
        await fixture.AssertScriptCompleteAsync(3);
        foreach (var tool in new[] { ImageGenerationToolPolicy.ImageGenerationCreate, ProjectStructureToolPolicy.ProjectStructureAssetCreate }) {
            Assert.Contains(run.GetProperty("receipts").EnumerateArray(), receipt => receipt.GetProperty("toolName").GetString() == tool &&
                receipt.GetProperty("invocationOutcome").GetInt32() == (int)AgentToolInvocationOutcome.Succeeded);
        }
        var asset = Assert.Single((await TreeAsync(fixture, projectId)).Nodes, node => node.Title == title);
        Assert.Equal(parentId, asset.ParentId);
        Assert.Equal(ProjectObjectType.ImageAsset, asset.ObjectType);
        var stored = await AssertStoredAssetAsync(fixture, projectId, asset);
        var expected = Convert.FromBase64String(FixturePng);
        var hash = Convert.ToHexString(SHA256.HashData(expected));
        for (var index = 0; index < 2; index++) {
            var content = await fixture.GetAsync($"api/project-structure/projects/{projectId:D}/assets/{Uri.EscapeDataString(asset.Id)}/content");
            Assert.Equal("image/png", content.GetProperty("asset").GetProperty("mediaContentType").GetString());
            Assert.Equal(stored.NodeId, content.GetProperty("asset").GetProperty("nodeId").GetString());
            Assert.Equal(expected, Convert.FromBase64String(content.GetProperty("base64Data").GetString()!));
            Assert.Equal(stored.MediaRelativePath, Assert.Single((await TreeAsync(fixture, projectId)).Nodes, node => node.Id == asset.Id).MediaRelativePath);
        }
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectFileNodeAsync(fixture.Page, asset.Id, title);
        var preview = fixture.Page.GetByTestId("project-structure-selection-window");
        await preview.GetByRole(AriaRole.Button, new() { Name = "Expand preview", Exact = true }).ClickAsync();
        var viewer = fixture.Page.GetByRole(AriaRole.Dialog, new() { Name = "generated.png file interaction", Exact = true });
        await Assertions.Expect(viewer.Locator("img")).ToBeVisibleAsync();
        Assert.True(await viewer.Locator("img").EvaluateAsync<bool>("image => image.complete && image.naturalWidth > 0 && image.naturalHeight > 0"));
        await fixture.ScreenshotAsync("consumer-image-preview");
        await fixture.NavigateAsync("/projects");
        await fixture.Page.GetByTestId("projects-search-input").FillAsync(projectName);
        await fixture.Page.GetByTestId("project-card").Filter(new() { Has = fixture.Page.GetByText(projectName, new() { Exact = true }) })
            .GetByTestId("project-card-files-button").ClickAsync();
        var files = fixture.Page.GetByTestId("project-files-dialog");
        await Assertions.Expect(files).ToContainTextAsync("This folder is empty");
        var contentDownload = await fixture.GetAsync($"api/project-structure/projects/{projectId:D}/assets/{Uri.EscapeDataString(asset.Id)}/content");
        var downloadedBytes = Convert.FromBase64String(contentDownload.GetProperty("base64Data").GetString()!);
        Assert.Equal(expected, downloadedBytes);
        var downloaded = Path.Combine(fixture.Settings.Evidence, "consumer-generated.png");
        await File.WriteAllBytesAsync(downloaded, downloadedBytes);
        Assert.Equal(expected, await File.ReadAllBytesAsync(downloaded));
        await fixture.ClearScriptAsync();
        await fixture.NavigateAsync($"/projects/{projectId:D}/structure");
        await ReadyFileCanvasAsync(fixture.Page);
        await SelectFileNodeAsync(fixture.Page, parentId, projectName);
        chat = await OpenFloatingChatFromCatalogAsync(fixture.Page, "project-structure-agents-toggle", agentId,
            Path.Combine(fixture.Settings.Evidence, "consumer-vision-catalog.png"));
        var attachmentInput = chat.GetByTestId("chat-image-attachment-input");
        await Assertions.Expect(attachmentInput).ToBeEnabledAsync(new() { Timeout = 60_000 });
        await attachmentInput.SetInputFilesAsync(downloaded);
        await chat.GetByText("1 staged", new() { Exact = true }).WaitForAsync();
        var vision = await FileTurnAsync(fixture, chat, agentId, projectId, parentId,
            "Analyze this attached harmless image. Do not call any tools. " + marker);
        await Assertions.Expect(chat).ToContainTextAsync("deterministic fixture analyzed the attached image");
        var captures = (await fixture.ReadCapturesAsync()).GetProperty("requests").EnumerateArray().ToArray();
        var generation = Assert.Single(captures, call => call.GetProperty("path").GetString() == "/v1/images/generations" &&
            call.GetProperty("body").GetString()!.Contains(marker, StringComparison.Ordinal));
        using var generatedRequest = JsonDocument.Parse(generation.GetProperty("body").GetString()!);
        Assert.Equal(images.GetModelDisplayName(images.DefaultModel), generatedRequest.RootElement.GetProperty("model").GetString());
        var visionRequest = Assert.Single(captures, call => call.GetProperty("path").GetString() == "/v1/responses" &&
            call.GetProperty("body").GetString()!.Contains(marker, StringComparison.Ordinal) &&
            CapturedContainsImage(call, "data:image/png;base64," + FixturePng));
        Assert.Equal(profile.GetModelDisplayName(profile.DefaultModel), CapturedSourceModel(visionRequest));
        Assert.Equal(200, visionRequest.GetProperty("response_status_code").GetInt32());
        await fixture.ScreenshotAsync("consumer-image-vision");
        await fixture.EvidenceAsync("consumer-media", new { projectId, agentId, asset.Id, stored.MediaRelativePath, Sha256 = hash,
            Bytes = expected.Length, ContentType = "image/png", ImageProviderId = images.Id, ImageRoute = images.DefaultModel,
            ContentDownload = "native authorized Project Structure content API", ProjectFilesRemainsTextOnly = true, AuthorizedPreviewLoaded = true,
            VisionSourceModel = CapturedSourceModel(visionRequest), VisionBodyTruncated = visionRequest.GetProperty("body_truncated").GetBoolean(),
            VisionCapturedBodySha256 = SharedProviderConsumerFixture.Hash(visionRequest.GetProperty("body").GetString()!),
            ImageSourceModel = images.GetModelDisplayName(images.DefaultModel), GenerationRunId = run.GetProperty("runId"), VisionRunId = vision.GetProperty("runId") });
    }

    private static bool CapturedContainsImage(JsonElement request, string expected) {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(request.GetProperty("body").GetString()!),
            isFinalBlock: !request.GetProperty("body_truncated").GetBoolean(), state: default);
        while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("image_url") && reader.Read() &&
                reader.TokenType == JsonTokenType.String && reader.GetString() == expected) {
                return true;
            }
        }
        return false;
    }

    private static FileProposalRefusal ValidateExactMediaProposal(AgentToolPreparedPayload payload, IReadOnlySet<string> accepted,
        object imageArguments, object assetArguments) {
        if (accepted.Contains(payload.ToolName)) {
            return FileProposalRefusal.DuplicateEffect;
        }
        object? expected = payload.ToolName switch {
            ImageGenerationToolPolicy.ImageGenerationCreate => imageArguments,
            ProjectStructureToolPolicy.ProjectStructureAssetCreate => assetArguments,
            _ => null
        };
        if (expected is null) {
            return FileProposalRefusal.UnexpectedTool;
        }
        using var proposed = JsonDocument.Parse(payload.ArgumentsJson);
        return JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected, SharedProviderConsumerFixture.Json), proposed.RootElement)
            ? FileProposalRefusal.None : FileProposalRefusal.WrongTarget;
    }
}
