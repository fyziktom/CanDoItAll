using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

internal static class ProjectsFilesBrowserProof {
    public static async Task DownloadAsync(LiveUiHost host, IPage page, ILocator surface, string fileName, byte[] expected, string evidenceName) {
        await surface.GetByRole(AriaRole.Button, new() { Name = $"Actions for {fileName}", Exact = true }).ClickAsync();
        var menu = surface.GetByRole(AriaRole.Group, new() { Name = $"Actions for {fileName}", Exact = true });
        await Assertions.Expect(menu).ToBeInViewportAsync(new() { Ratio = 1 });
        await page.ScreenshotAsync(new() { Path = host.Artifact(evidenceName + "-menu.png") });
        var download = await page.RunAndWaitForDownloadAsync(() => menu.GetByRole(AriaRole.Button, new() { Name = "Download", Exact = true }).ClickAsync());
        Assert.Equal(fileName, download.SuggestedFilename);
        Assert.Equal(Path.GetFileName(download.SuggestedFilename), download.SuggestedFilename);
        Assert.Null(await download.FailureAsync());
        string saved = host.Artifact(evidenceName + "-download.bin");
        await download.SaveAsAsync(saved);
        var actual = await File.ReadAllBytesAsync(saved);
        Assert.Equal(expected, actual);
        await File.WriteAllTextAsync(host.Artifact(evidenceName + "-download.json"), JsonSerializer.Serialize(new {
            download.SuggestedFilename, byteLength = actual.Length, sha256 = Convert.ToHexString(SHA256.HashData(actual)),
            capturedBrowserFile = true, viewport = new { width = 1920, height = 1080 }
        }));
    }

    public static async Task ReopenProducedAssetAsync(LiveUiHost host, IPage page, CrmHrBrowserOracle oracle,
        Guid projectId, string relativePath, string expected, string evidenceName) {
        Assert.False(string.IsNullOrWhiteSpace(relativePath));
        string fileName = Path.GetFileName(relativePath.Replace('\\', '/'));
        byte[] bytes = Encoding.UTF8.GetBytes(expected);
        var project = await host.SeedAsync(async services => {
            var project = await services.GetRequiredService<ProjectsService>().GetAsync(projectId);
            var coordinator = services.GetRequiredService<IProjectFilesPilotCoordinator>();
            await using var workspace = await coordinator.OpenAsync(projectId, project.Name);
            await workspace.Browser.InitializeAsync();
            var item = Assert.Single(workspace.Browser.Snapshot.Items, value => value.Name == fileName);
            await using var preview = await coordinator.ActivateAsync(workspace, item.Key);
            await using var content = await preview.Session.ContentSource.OpenReadAsync(new FileContentReadRequest(preview.Session.File));
            using var stream = new MemoryStream();
            await content.Stream.CopyToAsync(stream);
            Assert.Equal(bytes, stream.ToArray());
            return project;
        });
        await page.SetViewportSizeAsync(1920, 1080);
        await oracle.NavigateAsync(host.BaseUrl + "/projects");
        await Assertions.Expect(page.GetByTestId("projects-workspace")).ToHaveAttributeAsync("data-interactive", "true");
        await page.GetByTestId("projects-search-input").FillAsync(project.Name);
        var card = page.GetByTestId("project-card").Filter(new() { Has = page.GetByText(project.Name, new() { Exact = true }) });
        await Assertions.Expect(card).ToHaveCountAsync(1);
        await card.GetByTestId("project-card-files-button").ClickAsync();
        var dialog = page.GetByTestId("project-files-dialog");
        await DownloadAsync(host, page, dialog, fileName, bytes, evidenceName);
        await dialog.Locator(".ft-file-browser__item-main").Filter(new() { HasText = fileName }).PressAsync("Enter");
        string viewer = Path.GetExtension(fileName) == ".md" ? "interaction-markdown-view" : "interaction-text-view";
        await Assertions.Expect(dialog.GetByTestId(viewer)).ToContainTextAsync(expected.TrimStart('#', ' '));
        var bounds = await dialog.GetByTestId("file-interaction").BoundingBoxAsync();
        Assert.NotNull(bounds);
        Assert.True(bounds.Width >= 640);
        await Assertions.Expect(dialog.GetByTestId(viewer)).ToBeInViewportAsync();
        await Assertions.Expect(dialog.GetByTestId("interaction-mode-edit")).ToBeDisabledAsync();
        await page.ScreenshotAsync(new() { Path = host.Artifact(evidenceName + "-projects-preview.png") });
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
    }
}
