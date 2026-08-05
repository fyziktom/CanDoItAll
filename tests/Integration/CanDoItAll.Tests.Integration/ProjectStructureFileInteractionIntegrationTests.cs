using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tools.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureFileInteractionIntegrationTests
{
    [Fact]
    public async Task Direct_markdown_interaction_saves_with_current_revision_and_publishes_after_persistence()
    {
        await using var fixture = await InteractionFixture.CreateAsync();
        await using ProjectStructureKnownFileInteraction interaction = await fixture.Coordinator.OpenAsync(
            fixture.ProjectId,
            fixture.NodeKey);
        FileCatalogRevision before = fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id);
        const string replacement = "# Persisted interaction\n\nAwaited authorized save.";
        var args = CreateSaveArgs(interaction, replacement, interaction.Request.ContentRevision);

        await interaction.SaveAsync(args);

        Assert.True(interaction.CanEdit);
        Assert.Equal("text/markdown", interaction.Request.MediaType);
        Assert.True(args.HasPersistedRevision);
        Assert.NotEqual(interaction.Request.ContentRevision, args.PersistedRevision);
        Assert.Equal(replacement, await File.ReadAllTextAsync(fixture.FullPath));
        Assert.Equal(before.Scope + 1, fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id).Scope);
    }

    [Fact]
    public async Task Stale_and_overwrite_retries_fail_without_replacing_current_bytes_or_revision()
    {
        await using var fixture = await InteractionFixture.CreateAsync();
        await using ProjectStructureKnownFileInteraction interaction = await fixture.Coordinator.OpenAsync(
            fixture.ProjectId,
            fixture.NodeKey);
        FileCatalogRevision before = fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id);
        await File.WriteAllTextAsync(fixture.FullPath, "# External replacement");

        await Assert.ThrowsAsync<FileSaveConflictException>(
            () => interaction.SaveAsync(CreateSaveArgs(
                interaction,
                "# Stale local edit",
                interaction.Request.ContentRevision)));
        FileAccessDeniedException overwrite = await Assert.ThrowsAsync<FileAccessDeniedException>(
            () => interaction.SaveAsync(CreateSaveArgs(interaction, "# Forced overwrite", expectedRevision: null)));

        Assert.Equal(FileAccessFailureCode.OperationDenied, overwrite.Code);
        Assert.Equal("# External replacement", await File.ReadAllTextAsync(fixture.FullPath));
        Assert.Equal(before, fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id));
    }

    [Fact]
    public async Task Read_only_xlsx_preview_uses_only_authorized_session_content()
    {
        await using var fixture = await InteractionFixture.CreateSpreadsheetAsync();
        await using ProjectStructureKnownFileInteraction interaction = await fixture.Coordinator.OpenAsync(
            fixture.ProjectId,
            fixture.NodeKey);

        Assert.False(interaction.CanEdit);
        Assert.Equal(FileToolsKnownFileIntent.ReadOnly, interaction.Session.Intent);
        Assert.Null(interaction.Session.SaveTarget);
        Assert.Equal(ProjectStructureFileInteractionPolicy.XlsxMediaType, interaction.Request.MediaType);

        await using FileContentLease content = await interaction.Session.ContentSource.OpenReadAsync(
            new FileContentReadRequest(interaction.Session.File));
        using var authorizedContent = new MemoryStream();
        await content.Stream.CopyToAsync(authorizedContent);
        byte[] workbookBytes = authorizedContent.ToArray();
        Assert.NotEmpty(workbookBytes);

        var previewRequest = new SpreadsheetWorkbookContentPreviewRequest(
            interaction.Request.FileName,
            workbookBytes,
            MaxWorksheets: 1,
            MaxRows: 2,
            MaxColumns: 2);
        SpreadsheetWorkbookContentPreviewResult preview = fixture.SpreadsheetPreviews.PreviewWorkbook(previewRequest);

        Assert.Equal(Path.GetFileName(previewRequest.WorkbookName), previewRequest.WorkbookName);
        Assert.NotEqual(fixture.FullPath, previewRequest.WorkbookName);
        Assert.DoesNotContain(
            fixture.Storage.EndpointOrRoot,
            previewRequest.WorkbookName,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(previewRequest.WorkbookName, preview.DisplayName);
        SpreadsheetWorksheetPreview worksheet = Assert.Single(preview.Worksheets);
        Assert.Equal("Acceptance", worksheet.Name);
        Assert.Equal("A1:B2", worksheet.UsedRangeAddress);
        Assert.Equal("Check", worksheet.Values[0][0]);
        Assert.Equal("Value", worksheet.Values[0][1]);
        Assert.Equal("Formula", worksheet.Values[1][0]);
        Assert.Equal("=COUNTA(A1:A2)", worksheet.Values[1][1]);
    }

    private static FileInteractionSaveRequestedEventArgs CreateSaveArgs(
        ProjectStructureKnownFileInteraction interaction,
        string content,
        FileContentRevision? expectedRevision)
        => new(new FileSaveRequest(
            interaction.Session.File,
            editRevision: 1,
            new BufferedFileSaveContent(System.Text.Encoding.UTF8.GetBytes(content)),
            expectedRevision,
            "text/markdown",
            "utf-8"));

    private sealed class InteractionFixture : IAsyncDisposable
    {
        private readonly TestApplication application;
        private readonly AsyncServiceScope scope;

        private InteractionFixture(
            TestApplication application,
            AsyncServiceScope scope,
            ProjectStructureKnownFileInteractionCoordinator coordinator,
            IFileCatalogRevisionReader revisions,
            ISpreadsheetWorkbookContentPreviewService spreadsheetPreviews,
            FileToolsSemanticScope semanticScope,
            Guid projectId,
            string nodeKey,
            StorageCatalogRecord storage,
            string fullPath)
        {
            this.application = application;
            this.scope = scope;
            Coordinator = coordinator;
            Revisions = revisions;
            SpreadsheetPreviews = spreadsheetPreviews;
            Scope = semanticScope;
            ProjectId = projectId;
            NodeKey = nodeKey;
            Storage = storage;
            FullPath = fullPath;
        }

        public ProjectStructureKnownFileInteractionCoordinator Coordinator { get; }

        public IFileCatalogRevisionReader Revisions { get; }

        public ISpreadsheetWorkbookContentPreviewService SpreadsheetPreviews { get; }

        public FileToolsSemanticScope Scope { get; }

        public Guid ProjectId { get; }

        public string NodeKey { get; }

        public StorageCatalogRecord Storage { get; }

        public string FullPath { get; }

        public static Task<InteractionFixture> CreateAsync()
            => CreateAsync(FixtureFileKind.Markdown);

        public static Task<InteractionFixture> CreateSpreadsheetAsync()
            => CreateAsync(FixtureFileKind.Spreadsheet);

        private static async Task<InteractionFixture> CreateAsync(FixtureFileKind fileKind)
        {
            TestApplication application = await TestApplication.CreateAsync();
            AsyncServiceScope scope = application.Services.CreateAsyncScope();
            IServiceProvider services = scope.ServiceProvider;
            var projects = services.GetRequiredService<ProjectsService>();
            var storageCatalog = services.GetRequiredService<IStorageCatalogService>();
            var dbContextFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            var scopeProvider = services.GetRequiredService<IProjectStructureNodeFileScopeProvider>();
            var coordinator = services.GetRequiredService<ProjectStructureKnownFileInteractionCoordinator>();
            var revisions = services.GetRequiredService<IFileCatalogRevisionReader>();
            var documents = services.GetRequiredService<ISpreadsheetDocumentService>();
            var spreadsheetPreviews = services.GetRequiredService<ISpreadsheetWorkbookContentPreviewService>();
            var projectResult = await projects.SaveAsync(new ProjectEditorModel
            {
                Name = "Project interaction integration",
                Description = "Governed direct FileInteraction proof",
                Objective = "Prove revision-aware save",
                CurrentPhase = "Verification"
            });
            Assert.True(projectResult.IsSuccess, string.Join(" ", projectResult.Errors.Select(error => error.Message)));
            Guid projectId = projectResult.Value;
            StorageCatalogRecord storage = await storageCatalog.EnsureBootstrapFileSystemStorageAsync();
            (string extension, string objectSubtype, string mediaType) = fileKind switch
            {
                FixtureFileKind.Markdown => (".md", "markdown", "text/plain"),
                FixtureFileKind.Spreadsheet => (
                    ".xlsx",
                    "excel",
                    ProjectStructureFileInteractionPolicy.XlsxMediaType),
                _ => throw new ArgumentOutOfRangeException(nameof(fileKind))
            };
            string fileName = $"project-interaction-{Guid.NewGuid():N}{extension}";
            string relativePath = fileName;
            string fullPath = Path.Combine(storage.EndpointOrRoot, fileName);
            if (fileKind == FixtureFileKind.Spreadsheet)
            {
                documents.Write(new SpreadsheetWriteRequest(
                    fullPath,
                    fullPath,
                    "Acceptance",
                    [
                        new SpreadsheetCellWrite("A1", "Check"),
                        new SpreadsheetCellWrite("B1", "Value"),
                        new SpreadsheetCellWrite("A2", "Formula"),
                        new SpreadsheetCellWrite("B2", "=COUNTA(A1:A2)")
                    ],
                    [],
                    CreateWorkbookIfMissing: true,
                    Overwrite: true));
            }
            else
            {
                await File.WriteAllTextAsync(fullPath, "# Initial interaction");
            }

            string nodeKey = $"file:{Guid.NewGuid():N}";
            await using (AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync())
            {
                var node = new ProjectObjectRecord
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    NodeKey = nodeKey,
                    ObjectType = ProjectObjectType.File,
                    ObjectSubtype = objectSubtype,
                    Title = fileName,
                    MetadataJson = "{}"
                };
                dbContext.Set<ProjectObjectRecord>().Add(node);
                dbContext.Set<ProjectNodeBindingRecord>().Add(new ProjectNodeBindingRecord
                {
                    ProjectObjectId = node.Id,
                    MediaContentType = mediaType,
                    MediaOriginalFileName = fileName,
                    StorageObjectReferenceJson = StorageJson.SerializeReference(new StorageObjectReference(
                        storage.Id,
                        StorageProviderKind.FileSystem,
                        StorageLocatorKind.RelativePath,
                        relativePath,
                        fileName,
                        mediaType,
                        new FileInfo(fullPath).Length))
                });
                await dbContext.SaveChangesAsync();
            }

            FileToolsKnownFileScope resolved = await scopeProvider.ResolveKnownFileAsync(projectId, nodeKey);
            return new InteractionFixture(
                application,
                scope,
                coordinator,
                revisions,
                spreadsheetPreviews,
                resolved.Scope,
                projectId,
                nodeKey,
                storage,
                fullPath);
        }

        public async ValueTask DisposeAsync()
        {
            File.Delete(FullPath);
            await scope.DisposeAsync();
            await application.DisposeAsync();
        }

        private enum FixtureFileKind
        {
            Markdown,
            Spreadsheet
        }
    }
}
