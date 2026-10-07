using System.Data.Common;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Content.UI.Generation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePageSimpleMutationTests {
    public enum ImageTargetLoss { ProjectLifetime, PlaceholderOccurrence, Actor, Provider, ContentRevision }

    [Theory]
    [InlineData(ImageTargetLoss.ProjectLifetime, false)]
    [InlineData(ImageTargetLoss.ProjectLifetime, true)]
    [InlineData(ImageTargetLoss.PlaceholderOccurrence, false)]
    [InlineData(ImageTargetLoss.PlaceholderOccurrence, true)]
    [InlineData(ImageTargetLoss.Actor, false)]
    [InlineData(ImageTargetLoss.Actor, true)]
    [InlineData(ImageTargetLoss.Provider, false)]
    [InlineData(ImageTargetLoss.Provider, true)]
    [InlineData(ImageTargetLoss.ContentRevision, false)]
    [InlineData(ImageTargetLoss.ContentRevision, true)]
    public async Task Generated_image_retired_authority_never_attaches_to_a_replacement(ImageTargetLoss loss, bool afterExternal) {
        var image = new RecordingAgentImageGenerationService(waitForRelease: true);
        await using var harness = await ImageHarnessAsync(image);
        var (cut, queue) = await QueueNativeImageAsync(harness);
        var request = queue.Request!;
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var worker = harness.Context.Services.GetRequiredService<ProjectStructureDeferredNodeCompletionWorker>();
        if (afterExternal) {
            await worker.StartAsync(CancellationToken.None);
            await image.WaitForFirstRequestAsync();
        }
        try {
            await RetireImageTargetAsync(harness, request, loss);
            var before = Assert.Single((await workbench.GetStructureAsync(request.ProjectId)).Nodes, node => node.Id == request.NodeId);
            image.CompleteGeneration();
            if (!afterExternal) {
                await worker.StartAsync(CancellationToken.None);
            }
            var result = await queue.Handle!.Completion.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.False(result.IsSuccess);
            Assert.Equal(afterExternal, result.ProviderInvoked);
            Assert.Equal(afterExternal, result.ProviderCompleted);
            Assert.False(result.PersistenceAttempted);
            Assert.Null(result.StoredMedia);
            Assert.Equal(afterExternal ? 1 : 0, image.Requests.Count);
            var after = Assert.Single((await workbench.GetStructureAsync(request.ProjectId)).Nodes, node => node.Id == request.NodeId);
            Assert.Equal(before.RecordId, after.RecordId);
            Assert.Equal(before.StorageObjectReferenceJson, after.StorageObjectReferenceJson);
            Assert.NotEqual("image/png", after.MediaContentType);
            if (loss is ImageTargetLoss.ProjectLifetime or ImageTargetLoss.PlaceholderOccurrence or ImageTargetLoss.ContentRevision) {
                Assert.Equal(before.MetadataJson, after.MetadataJson);
                Assert.Equal(before.Notes, after.Notes);
            }
            cut.WaitForAssertion(() => Assert.Equal(afterExternal ? ContentImagePhase.ProviderCompleted : ContentImagePhase.Rejected,
                cut.FindComponent<ContentImageDialog>().Instance.State.Receipt!.Phase));
        } finally {
            image.CompleteGeneration();
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generated_image_media_write_and_node_acknowledgement_remain_separate(bool afterCommit) {
        var fault = new ImageMediaFault(afterCommit);
        var image = new RecordingAgentImageGenerationService();
        await using var harness = await ImageHarnessAsync(image, fault);
        var (cut, queue) = await QueueNativeImageAsync(harness);
        var request = queue.Request!;
        fault.ProjectId = request.ProjectId;
        await using var worker = await StartedDeferredCompletionWorker.StartAsync(harness);
        var result = await queue.Handle!.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(result.IsSuccess);
        Assert.True(result.ProviderInvoked);
        Assert.True(result.ProviderCompleted);
        Assert.True(result.PersistenceAttempted);
        Assert.NotEmpty(result.ContentSha256!);
        var receipt = Assert.IsType<ProjectStructureContentMediaReceipt>(result.StoredMedia);
        Assert.Equal((request.ProjectId, request.NodeId, request.Origin!.Placeholder.RecordId),
            (receipt.Project.ProjectId, receipt.NodeId, (Guid?)receipt.RecordId));
        Assert.Equal("image/png", receipt.ContentType);
        Assert.NotEmpty(receipt.StorageObjectReferenceJson);
        var stored = Assert.Single((await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(request.ProjectId)).Nodes,
            node => node.Id == request.NodeId);
        Assert.Equal(afterCommit ? receipt.StorageObjectReferenceJson : request.Origin.Placeholder.StorageObjectReferenceJson, stored.StorageObjectReferenceJson);
        Assert.Equal(afterCommit ? "image/png" : "image/svg+xml", stored.MediaContentType);
        cut.WaitForAssertion(() => Assert.Equal(ContentImagePhase.ProviderCompleted, cut.FindComponent<ContentImageDialog>().Instance.State.Receipt!.Phase));
        await cut.InvokeAsync(() => cut.FindComponent<ContentImageDialog>().Instance.Submit(new("Do not resend", "", "Again", request.GeneratedImage!.ProviderProfileId, "")));
        Assert.Single(image.Requests);
        Assert.Equal(1, queue.Calls);
        Assert.Contains(cut.Instance.AuthoringOutcomes, outcome => outcome.StoredMedia == receipt && outcome.Kind == ProjectStructureAuthoringResultKind.PartialCommit);
    }

    [Fact]
    public async Task Generated_image_interrupted_worker_keeps_observable_running_state_without_replay() {
        var image = new RecordingAgentImageGenerationService(waitForRelease: true);
        await using var harness = await ImageHarnessAsync(image);
        var (_, queue) = await QueueNativeImageAsync(harness);
        var worker = harness.Context.Services.GetRequiredService<ProjectStructureDeferredNodeCompletionWorker>();
        await worker.StartAsync(CancellationToken.None);
        await image.WaitForFirstRequestAsync();
        await worker.StopAsync(CancellationToken.None);
        var result = await queue.Handle!.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(result.ProviderInvoked);
        Assert.False(result.ProviderCompleted);
        Assert.False(result.IsSuccess);
        var node = Assert.Single((await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(queue.Request!.ProjectId)).Nodes,
            node => node.Id == queue.Request.NodeId);
        Assert.Equal(ProjectStructureDeferredNodeCompletionState.Running, ProjectObjectMetadataSerializer.Parse(node.MetadataJson).DeferredCompletion!.State);
        Assert.Single(image.Requests);
        Assert.Equal(1, queue.Calls);
    }

    [Fact]
    public async Task Generated_image_duplicate_queue_delivery_cannot_repeat_completed_provider() {
        var image = new RecordingAgentImageGenerationService();
        await using var harness = await ImageHarnessAsync(image);
        var (_, queue) = await QueueNativeImageAsync(harness);
        await using var worker = await StartedDeferredCompletionWorker.StartAsync(harness);
        var accepted = await queue.Handle!.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.True(accepted.IsSuccess);
        var duplicate = await harness.Context.Services.GetRequiredService<ProjectStructureDeferredNodeCompletionQueue>().EnqueueAsync(queue.Request!);
        var refused = await duplicate.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(refused.IsSuccess);
        Assert.False(refused.ProviderInvoked);
        Assert.Single(image.Requests);
        var stored = Assert.Single((await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(queue.Request!.ProjectId)).Nodes,
            node => node.Id == queue.Request.NodeId);
        Assert.Equal(accepted.UpdatedNode!.StorageObjectReferenceJson, stored.StorageObjectReferenceJson);
        Assert.Equal(ProjectStructureDeferredNodeCompletionState.Completed, ProjectObjectMetadataSerializer.Parse(stored.MetadataJson).DeferredCompletion!.State);
    }

    [Fact]
    public async Task Generated_image_placeholder_survives_enqueue_refusal_without_generation_or_repeat() {
        var image = new RecordingAgentImageGenerationService();
        await using var harness = await ImageHarnessAsync(image);
        var queue = harness.Context.Services.GetRequiredService<NativeImageQueueProbe>();
        queue.RefuseBeforeQueue = true;
        var provider = await SaveImageProviderAsync(harness.Context.Services.GetRequiredService<SecretService>(), harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>());
        var project = await CreateProjectAsync(harness.Context.Services.GetRequiredService<ProjectsService>(), "Enqueue refusal owner");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(page => page.ProjectId, project));
        await RefreshGeneratedImageCreateActionAsync(cut, WaitForCanvasWorkbench(cut));
        var submit = cut.FindComponent<ContentImageDialog>().Instance.Submit;
        var draft = new ContentImageDraft("Accepted placeholder", "", "Synthetic prompt", provider, "");
        await cut.InvokeAsync(() => submit(draft));
        await cut.InvokeAsync(() => submit(draft));
        var placeholder = Assert.Single((await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(project)).Nodes,
            node => node.ObjectType == ProjectObjectType.ImageAsset);
        Assert.Equal(queue.Request!.Origin!.Placeholder.RecordId, placeholder.RecordId);
        Assert.Equal(ContentImagePhase.PlaceholderSaved, cut.FindComponent<ContentImageDialog>().Instance.State.Receipt!.Phase);
        Assert.Empty(image.Requests);
        Assert.Equal(1, queue.Calls);
        Assert.Null(queue.Handle);
        Assert.Contains(cut.Instance.AuthoringOutcomes, outcome => outcome.Node?.RecordId == placeholder.RecordId &&
            outcome.Kind == ProjectStructureAuthoringResultKind.PartialCommit && outcome.ExternalEffect == ProjectStructureExternalEffectState.NotStarted);
    }

    private static Task<ComponentTestHarness> ImageHarnessAsync(RecordingAgentImageGenerationService image, ImageMediaFault? fault = null)
        => ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IAgentImageGenerationService>(image));
            services.AddSingleton<NativeImageQueueProbe>();
            services.Replace(ServiceDescriptor.Singleton<IProjectStructureDeferredNodeCompletionQueue>(sp => sp.GetRequiredService<NativeImageQueueProbe>()));
            if (fault is not null) {
                services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(sp => new PooledDbContextFactory<WorkbenchDbContext>(
                    new DbContextOptionsBuilder<WorkbenchDbContext>(sp.GetRequiredService<DbContextOptions<WorkbenchDbContext>>()).AddInterceptors(fault).Options));
            }
        });

    private static async Task<(IRenderedComponent<ProjectStructurePage> Page, NativeImageQueueProbe Queue)> QueueNativeImageAsync(ComponentTestHarness harness) {
        var provider = await SaveImageProviderAsync(harness.Context.Services.GetRequiredService<SecretService>(), harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>());
        var project = await CreateProjectAsync(harness.Context.Services.GetRequiredService<ProjectsService>(), "Original image owner");
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(page => page.ProjectId, project));
        var canvas = WaitForCanvasWorkbench(cut);
        await RefreshGeneratedImageCreateActionAsync(cut, canvas);
        var dialog = cut.FindComponent<ContentImageDialog>().Instance;
        await cut.InvokeAsync(() => dialog.Submit(new("Original image", "Native queue proof", "Synthetic bounded image", provider, "")));
        var queue = harness.Context.Services.GetRequiredService<NativeImageQueueProbe>();
        Assert.NotNull(queue.Request);
        Assert.NotNull(queue.Handle);
        Assert.Equal(ContentImagePhase.Queued, cut.FindComponent<ContentImageDialog>().Instance.State.Receipt!.Phase);
        return (cut, queue);
    }

    private static async Task RetireImageTargetAsync(ComponentTestHarness harness, ProjectStructureDeferredNodeCompletionRequest request, ImageTargetLoss loss) {
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var original = request.Origin!;
        switch (loss) {
            case ImageTargetLoss.Actor:
                original.Authority.Revoke();
                return;
            case ImageTargetLoss.Provider:
                var providers = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
                var editor = await providers.GetProviderEditorAsync(request.GeneratedImage!.ProviderProfileId);
                editor.IsEnabled = false;
                await providers.SaveProviderAsync(editor);
                return;
            case ImageTargetLoss.ContentRevision:
                await workbench.UpdateObjectMetadataAsync(request.ProjectId, request.NodeId, original.Placeholder.MetadataJson, notes: "New legitimate content revision");
                return;
            case ImageTargetLoss.ProjectLifetime:
                var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
                await projects.DeleteAsync(request.ProjectId, expectedProjectAdmission: original.Authority.Project);
                Assert.True((await projects.CreateAsync(request.ProjectId, new() { Name = "Replacement project with same public ID" })).IsSuccess);
                break;
            case ImageTargetLoss.PlaceholderOccurrence:
                Assert.Equal(1, await workbench.DeleteObjectAsync(request.ProjectId, request.NodeId));
                break;
        }
        await using var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        db.Set<ProjectObjectRecord>().Add(new() {
            Id = Guid.NewGuid(), NodeKey = request.NodeId, ProjectId = request.ProjectId,
            ObjectType = ProjectObjectType.ImageAsset, ObjectSubtype = "generated", Title = "Replacement image occurrence",
            ParentNodeKey = $"project:{request.ProjectId}", MetadataJson = original.Placeholder.MetadataJson,
            Notes = "Replacement notes", CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var current = await workbench.GetStructureAsync(request.ProjectId);
        Assert.NotEqual(original.Placeholder.RecordId, Assert.Single(current.Nodes, node => node.Id == request.NodeId).RecordId);
        if (loss == ImageTargetLoss.ProjectLifetime) {
            Assert.NotEqual(original.Authority.Project.LifetimeId, current.ExpectedProjectAdmission!.LifetimeId);
        }
    }

    private sealed class NativeImageQueueProbe(ProjectStructureDeferredNodeCompletionQueue queue) : IProjectStructureDeferredNodeCompletionQueue {
        public ProjectStructureDeferredNodeCompletionRequest? Request { get; private set; }
        public ProjectStructureDeferredNodeCompletionHandle? Handle { get; private set; }
        public int Calls { get; private set; }
        public bool RefuseBeforeQueue { get; set; }
        public async ValueTask<ProjectStructureDeferredNodeCompletionHandle> EnqueueAsync(ProjectStructureDeferredNodeCompletionRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            Request = request;
            if (RefuseBeforeQueue) {
                throw new OperationCanceledException("Synthetic enqueue refusal before channel admission.");
            }
            Handle = await queue.EnqueueAsync(request, cancellationToken);
            return Handle;
        }
    }

    private sealed class ImageMediaFault(bool afterCommit) : SaveChangesInterceptor, IDbTransactionInterceptor {
        private DbContext? accepted;
        public Guid? ProjectId { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (ProjectId is { } project && eventData.Context?.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry =>
                entry.State == EntityState.Modified && entry.Entity.ProjectId == project && entry.Entity.ObjectType == ProjectObjectType.ImageAsset &&
                ProjectObjectMetadataSerializer.Parse(entry.Entity.MetadataJson).DeferredCompletion?.State == ProjectStructureDeferredNodeCompletionState.Completed) is true) {
                ProjectId = null;
                if (!afterCommit) {
                    throw new IOException("Synthetic native refusal after media storage.");
                }
                accepted = eventData.Context;
            }
            return ValueTask.FromResult(result);
        }
        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (accepted is not null && ReferenceEquals(accepted, eventData.Context)) {
                accepted = null;
                throw new IOException("Synthetic lost acknowledgement after native media attachment.");
            }
            return Task.CompletedTask;
        }
    }
}
