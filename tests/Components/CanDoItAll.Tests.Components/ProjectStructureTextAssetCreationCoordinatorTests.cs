using System.Text;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureTextAssetCreationCoordinatorTests
{
    [Fact]
    public async Task Create_direct_json_upload_canonicalizes_media_and_invokes_the_extracted_creator()
    {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<ProjectAssetCreationService>();
        context.Services.AddScoped<ProjectStructureTextAssetCreationCoordinator>();
        var coordinator = context.Services.GetRequiredService<ProjectStructureTextAssetCreationCoordinator>();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
            "add-file-json",
            out ProjectStructureCreateLeafDefinition definition));
        byte[] content = Encoding.UTF8.GetBytes("{\"enabled\":true}");
        var request = new CanvasWorkbenchCreateActionRequest(
            definition.ActionId,
            "source-node",
            420,
            260,
            "parent-node",
            string.Empty,
            "config",
            "Runtime settings.",
            "child",
            "dialog",
            definition.ObjectSubtype,
            new CanvasWorkbenchUploadedFile
            {
                FileName = "SETTINGS.JSON",
                ContentType = "application/octet-stream",
                Base64Data = Convert.ToBase64String(content)
            });
        CanvasWorkbenchCreateActionRequest? capturedRequest = null;
        ProjectObjectMediaPayload? capturedMedia = null;
        CancellationToken capturedCancellationToken = default;
        using var cancellationSource = new CancellationTokenSource();
        var creationContext = new ProjectStructureTextAssetCreationContext(
            Guid.NewGuid(),
            (capturedDefinition, submittedRequest, media, cancellationToken) =>
            {
                Assert.Same(definition, capturedDefinition);
                capturedRequest = submittedRequest;
                capturedMedia = media;
                capturedCancellationToken = cancellationToken;
                return Task.FromResult<ProjectStructureNode?>(CreateNode());
            });

        await coordinator.CreateAsync(
            creationContext,
            definition,
            request,
            cancellationSource.Token);

        Assert.NotNull(capturedRequest);
        Assert.Equal("SETTINGS", capturedRequest.Title);
        Assert.Null(capturedRequest.UploadedFile);
        Assert.NotNull(capturedMedia);
        Assert.Equal("SETTINGS.json", capturedMedia.FileName);
        Assert.Equal("application/json", capturedMedia.ContentType);
        Assert.Equal(content, Convert.FromBase64String(capturedMedia.Base64Data));
        Assert.Equal(cancellationSource.Token, capturedCancellationToken);
    }

    [Fact]
    public async Task Dismissing_the_dialog_cancels_in_flight_persistence()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<ProjectAssetCreationService>();
        context.Services.AddScoped<ProjectStructureTextAssetCreationCoordinator>();
        var coordinator = context.Services.GetRequiredService<ProjectStructureTextAssetCreationCoordinator>();
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
            "add-file-markdown",
            out ProjectStructureCreateLeafDefinition definition));
        var request = new CanvasWorkbenchCreateActionRequest(
            definition.ActionId,
            "source-node",
            420,
            260,
            "parent-node",
            string.Empty,
            "docs",
            string.Empty,
            "child",
            "create",
            definition.ObjectSubtype,
            null);
        var persistenceStarted = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var nodeCreated = false;
        var creationContext = new ProjectStructureTextAssetCreationContext(
            Guid.NewGuid(),
            async (_, _, _, cancellationToken) =>
            {
                persistenceStarted.TrySetResult(cancellationToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                nodeCreated = true;
                return CreateNode();
            });

        Task createTask = coordinator.CreateAsync(creationContext, definition, request);
        host.WaitForElement("[data-testid='project-structure-text-asset-title']").Input("Architecture notes");
        host.Find("[data-testid='project-structure-text-asset-content']").Input("# Architecture");
        DialogReference dialog = Assert.Single(dialogService.Dialogs);
        Assert.False(dialog.Options.CloseOnBackdrop);
        Assert.False(dialog.Options.ShowCloseButton);
        Task submitTask = host.InvokeAsync(
            () => host.Find("[data-testid='project-structure-text-asset-submit']").Click());
        CancellationToken persistenceToken = await persistenceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await dialog.CloseAsync();
        await createTask.WaitAsync(TimeSpan.FromSeconds(2));
        await submitTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(persistenceToken.IsCancellationRequested);
        Assert.False(nodeCreated);
        Assert.Empty(dialogService.Dialogs);
    }

    [Fact]
    public async Task Committed_asset_with_follow_up_failure_closes_the_dialog_without_a_duplicate_retry()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<ProjectAssetCreationService>();
        context.Services.AddScoped<ProjectStructureTextAssetCreationCoordinator>();
        var coordinator = context.Services.GetRequiredService<ProjectStructureTextAssetCreationCoordinator>();
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
            "add-file-markdown",
            out ProjectStructureCreateLeafDefinition definition));
        var request = new CanvasWorkbenchCreateActionRequest(
            definition.ActionId,
            "source-node",
            420,
            260,
            "parent-node",
            string.Empty,
            "docs",
            string.Empty,
            "child",
            "create",
            definition.ObjectSubtype,
            null);
        ProjectStructureNode committedNode = CreateNode();
        var invocationCount = 0;
        var creationContext = new ProjectStructureTextAssetCreationContext(
            Guid.NewGuid(),
            (_, _, _, _) =>
            {
                invocationCount++;
                return Task.FromException<ProjectStructureNode?>(
                    new ProjectStructureNodeCreatedWithFollowUpFailureException(
                        committedNode,
                        new InvalidOperationException("Surface refresh failed.")));
            });

        Task createTask = coordinator.CreateAsync(creationContext, definition, request);
        host.WaitForElement("[data-testid='project-structure-text-asset-title']").Input("Architecture notes");
        host.Find("[data-testid='project-structure-text-asset-content']").Input("# Architecture");
        Task submitTask = host.InvokeAsync(
            () => host.Find("[data-testid='project-structure-text-asset-submit']").Click());

        await createTask.WaitAsync(TimeSpan.FromSeconds(2));
        await submitTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, invocationCount);
        Assert.Empty(dialogService.Dialogs);
    }

    private static ProjectStructureNode CreateNode()
        => new(
            "custom:text-asset",
            "parent-node",
            ProjectObjectType.File,
            "json",
            "Settings",
            "config",
            "Draft",
            "Runtime settings.",
            string.Empty,
            "JSON",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "#64748b", "JS", "JSON"),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
}
