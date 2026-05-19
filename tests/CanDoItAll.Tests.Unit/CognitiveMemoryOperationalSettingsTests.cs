using System.IO.Compression;
using System.Text;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryOperationalSettingsTests
{
    [Fact]
    public async Task AutomationSettingsService_PersistsScheduleAndSourceOptions()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryAutomationSettingsService(fixture.Factory, fixture.Clock);

        var saved = await service.SaveAsync(new CognitiveMemoryAutomationSettingsUpdate(
            CognitiveMemoryAutomationScheduleMode.ScheduledMoments,
            "01:30",
            45,
            ["03:00", "16:15"],
            AutoIngestProjectStructure: true,
            AutoIngestProcessRuntime: false,
            AutoConsolidateAfterIngestion: true,
            CognitiveMemoryModelAccessMode.SelectedProvidersOnly,
            DefaultProviderProfileId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DefaultAgentId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AllowedProviderProfileIds: [Guid.Parse("33333333-3333-3333-3333-333333333333")],
            UpdatedByActorId: "test:operator")
        {
            ModelExecutionProfiles =
            [
                CognitiveMemoryModelExecutionProfileDefaults.CreateOpenAi(CognitiveMemoryModelExecutionRole.Consolidation),
                CognitiveMemoryModelExecutionProfileDefaults.CreateOllama(CognitiveMemoryModelExecutionRole.Probe)
            ]
        });

        var loaded = await service.GetAsync();

        Assert.Equal(CognitiveMemoryAutomationScheduleMode.ScheduledMoments, saved.ScheduleMode);
        Assert.Equal("01:30", loaded.NightlyLocalTime);
        Assert.Equal(45, loaded.IdleMinutes);
        Assert.Equal(["03:00", "16:15"], loaded.ScheduledLocalTimes);
        Assert.True(loaded.AutoIngestProjectStructure);
        Assert.False(loaded.AutoIngestProcessRuntime);
        Assert.True(loaded.AutoConsolidateAfterIngestion);
        Assert.Equal(CognitiveMemoryModelAccessMode.SelectedProvidersOnly, loaded.ModelAccessMode);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), loaded.DefaultProviderProfileId);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), loaded.DefaultAgentId);
        Assert.Equal([Guid.Parse("33333333-3333-3333-3333-333333333333")], loaded.AllowedProviderProfileIds);
        Assert.Contains(loaded.ModelExecutionProfiles, profile =>
            profile.Role == CognitiveMemoryModelExecutionRole.Consolidation &&
            profile.ModelId.Value == CognitiveMemoryModelExecutionProfileDefaults.OpenAiDefaultModelId &&
            profile.MaxOutputTokens == CognitiveMemoryModelExecutionProfileDefaults.DefaultOpenAiMaxOutputTokens);
        Assert.Contains(loaded.ModelExecutionProfiles, profile =>
            profile.Role == CognitiveMemoryModelExecutionRole.Probe &&
            profile.ModelId.Value == CognitiveMemoryModelExecutionProfileDefaults.OllamaValidationModelId &&
            profile.MaxOutputTokens == CognitiveMemoryModelExecutionProfileDefaults.DefaultOllamaMaxOutputTokens &&
            profile.LocalOnly);
        Assert.Equal("test:operator", loaded.UpdatedByActorId);
    }

    [Fact]
    public async Task ExternalSourceIngestionService_CreatesSourceItemEvidenceAndOperationStatus()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryExternalSourceIngestionService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryExternalSourceIngestionService>.Instance);
        var projectId = Guid.NewGuid();

        var result = await service.IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.UploadedFile,
            projectId,
            "SaaS launch plan",
            "saas-launch-plan.md",
            "Detailed source content for a SaaS launch plan.",
            "text/markdown",
            47,
            "test:operator"));

        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, result.Status);
        Assert.Equal(100, result.ProgressPercent);
        Assert.NotNull(result.SourceManifestId);
        Assert.NotNull(result.SourceItemId);
        Assert.NotNull(result.EvidenceAnchorId);

        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Single(await dbContext.Set<CognitiveMemorySourceManifestRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemorySourceItemRecord>().ToListAsync());
        Assert.Single(await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>().ToListAsync());
        var operation = await dbContext.Set<CognitiveMemoryExternalSourceIngestionRecord>().SingleAsync();
        Assert.Equal(result.OperationId, operation.Id);
        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, operation.Status);
    }

    [Fact]
    public async Task ExternalSourceIngestionService_AllowsSameCallerIdempotencyKeyAcrossDistinctUploads()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryExternalSourceIngestionService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryExternalSourceIngestionService>.Instance);
        var projectId = Guid.NewGuid();

        var first = await service.IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.UploadedFile,
            projectId,
            "LB4U pitch deck",
            "LB4U.pptx",
            "Pitch deck source content.",
            "text/plain",
            26,
            "test:operator",
            "lb4u-stage02-duplicate-key"));
        var second = await service.IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.UploadedFile,
            projectId,
            "LB4U pitch PDF",
            "LB4U.pdf",
            "Pitch PDF source content.",
            "text/plain",
            25,
            "test:operator",
            "lb4u-stage02-duplicate-key"));

        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, first.Status);
        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, second.Status);
        await using var dbContext = fixture.Factory.CreateDbContext();
        Assert.Equal(2, await dbContext.Set<CognitiveMemoryRunRecord>().CountAsync());
    }


    [Fact]
    public async Task ExternalSourceIngestionService_SplitsMarkdownIntoReviewableSections()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryExternalSourceIngestionService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryExternalSourceIngestionService>.Instance);
        var projectId = Guid.NewGuid();
        var content = """
            # Follow-up source

            Intro text.

            ## FieldOps Mobile App

            Offline route planning, technician sync, photo evidence, and conflict review.

            ## ClinicFlow SaaS Business Plan

            Clinic scheduling, patient reminders, no-show reduction, and pilot risks.
            """;

        var result = await service.IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.UploadedFile,
            projectId,
            "sample-projects.md",
            "sample-projects.md",
            content,
            "text/markdown",
            content.Length,
            "test:operator"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .OrderBy(item => item.Title)
            .ToListAsync();
        var anchors = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>().ToListAsync();

        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, result.Status);
        Assert.Equal(2, sourceItems.Count);
        Assert.Equal(2, anchors.Count);
        Assert.All(sourceItems, item => Assert.Equal("UploadedFileChunk", item.SourceItemType));
        Assert.Contains(sourceItems, item => item.Title.Contains("FieldOps Mobile App", StringComparison.Ordinal));
        Assert.Contains(sourceItems, item => item.ContentText.Contains("Offline route planning", StringComparison.Ordinal));
        Assert.DoesNotContain(sourceItems.Single(item => item.Title.Contains("FieldOps", StringComparison.Ordinal)).ContentText, "Clinic scheduling", StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalSourceIngestionService_SplitsMermaidMindmapIntoReviewableBranches()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryExternalSourceIngestionService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryExternalSourceIngestionService>.Instance);
        var projectId = Guid.NewGuid();
        var content = """
            mindmap
              root((Follow-up sources))
                FieldOps Mobile App
                  Offline work orders
                    Conflict resolution
                    Idempotent sync
                Regional Economy
                  Inflation inputs
                    CPI
                    Wages
                  Business responses
                    Hiring delay
            """;

        var result = await service.IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.UploadedFile,
            projectId,
            "sample-projects.mmd",
            "sample-projects.mmd",
            content,
            "text/plain",
            content.Length,
            "test:operator"));

        await using var dbContext = fixture.Factory.CreateDbContext();
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .OrderBy(item => item.Title)
            .ToListAsync();

        Assert.Equal(CognitiveMemoryExternalSourceIngestionStatus.Succeeded, result.Status);
        Assert.Equal(2, sourceItems.Count);
        Assert.All(sourceItems, item => Assert.Equal("UploadedFileChunk", item.SourceItemType));
        Assert.Contains(sourceItems, item => item.Title.Contains("FieldOps Mobile App", StringComparison.Ordinal));
        Assert.Contains(sourceItems, item => item.Title.Contains("Regional Economy", StringComparison.Ordinal));
        Assert.Contains("Idempotent sync", sourceItems.Single(item => item.Title.Contains("FieldOps", StringComparison.Ordinal)).ContentText, StringComparison.Ordinal);
        Assert.DoesNotContain("Inflation inputs", sourceItems.Single(item => item.Title.Contains("FieldOps", StringComparison.Ordinal)).ContentText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalSourceIngestionService_ExtractsOfficeUploadsIntoSourceText()
    {
        var fixture = CreateFixture();
        var service = new CognitiveMemoryExternalSourceIngestionService(
            fixture.Factory,
            fixture.Clock,
            NullLogger<CognitiveMemoryExternalSourceIngestionService>.Instance);
        var projectId = Guid.NewGuid();

        var docx = CreateDocx("LB4U business plan describes phased recruitment and salary planning.");
        var pptx = CreatePptx("LB4U pitch deck", "Pilot launch and marketing activities.");
        var xlsx = CreateXlsx("Expense category", "Monthly payroll reserve");

        await service.IngestFileAsync(projectId, "LB4U-BP.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", new MemoryStream(docx), docx.Length, "test:operator");
        await service.IngestFileAsync(projectId, "LB4U-presentation.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", new MemoryStream(pptx), pptx.Length, "test:operator");
        await service.IngestFileAsync(projectId, "LB4U-financial-plan.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new MemoryStream(xlsx), xlsx.Length, "test:operator");

        await using var dbContext = fixture.Factory.CreateDbContext();
        var contentTexts = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .Select(item => item.ContentText)
            .ToListAsync();

        Assert.Contains(contentTexts, text => text.Contains("phased recruitment", StringComparison.Ordinal));
        Assert.Contains(contentTexts, text => text.Contains("Pilot launch", StringComparison.Ordinal));
        Assert.Contains(contentTexts, text => text.Contains("Monthly payroll reserve", StringComparison.Ordinal));
    }

    [Fact]
    public void StagedSourceManifestValidator_RejectsExcludedOrOutOfRootSources()
    {
        using var temporaryRoot = new TemporaryDirectory();
        var semanticPath = Path.Combine(temporaryRoot.FullPath, "LB4U-BP.docx");
        var secretDirectory = Path.Combine(temporaryRoot.FullPath, "routery hesla");
        Directory.CreateDirectory(secretDirectory);
        File.WriteAllText(semanticPath, "business plan", Encoding.UTF8);
        File.WriteAllText(Path.Combine(secretDirectory, "router-passwords.txt"), "router password", Encoding.UTF8);

        var manifest = new CognitiveMemoryStagedSourceManifest(
            "lb4u",
            temporaryRoot.FullPath,
            [
                new CognitiveMemoryStagedSourceStage(
                    "stage-01",
                    "Business plan",
                    [
                        new CognitiveMemoryStagedSourcePath(
                            CognitiveMemoryStagedSourceItemKind.SemanticSource,
                            semanticPath,
                            "LB4U business plan"),
                        new CognitiveMemoryStagedSourcePath(
                            CognitiveMemoryStagedSourceItemKind.SemanticSource,
                            Path.Combine(secretDirectory, "router-passwords.txt"),
                            "Do not ingest")
                    ])
            ],
            [secretDirectory]);

        var result = CognitiveMemoryStagedSourceManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Single(result.Sources);
        Assert.Contains(result.Violations, violation => violation.Contains("excluded", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Sources, source => source.FullPath.Contains("router-passwords", StringComparison.OrdinalIgnoreCase));
    }

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(CognitiveMemoryModuleAssemblyMarker).Assembly]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cognitive-memory-operational-{Guid.NewGuid():N}")
            .Options;
        return new TestFixture(new TestDbContextFactory(options), new FixedClock());
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        FixedClock Clock);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private static byte[] CreateDocx(string paragraph)
        => CreateZip(
            ("word/document.xml", $$"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                    <w:p><w:r><w:t>{{paragraph}}</w:t></w:r></w:p>
                  </w:body>
                </w:document>
                """));

    private static byte[] CreatePptx(string title, string body)
        => CreateZip(
            ("ppt/slides/slide1.xml", $$"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                  <p:cSld>
                    <p:spTree>
                      <p:sp><p:txBody><a:p><a:r><a:t>{{title}}</a:t></a:r></a:p><a:p><a:r><a:t>{{body}}</a:t></a:r></a:p></p:txBody></p:sp>
                    </p:spTree>
                  </p:cSld>
                </p:sld>
                """));

    private static byte[] CreateXlsx(string heading, string value)
        => CreateZip(
            ("xl/sharedStrings.xml", $$"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <si><t>{{heading}}</t></si>
                  <si><t>{{value}}</t></si>
                </sst>
                """),
            ("xl/worksheets/sheet1.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c></row>
                  </sheetData>
                </worksheet>
                """));

    private static byte[] CreateZip(params (string EntryName, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                var archiveEntry = archive.CreateEntry(entry.EntryName);
                using var writer = new StreamWriter(archiveEntry.Open(), Encoding.UTF8);
                writer.Write(entry.Content);
            }
        }

        return stream.ToArray();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            FullPath = Path.Combine(Path.GetTempPath(), $"cognitive-memory-{Guid.NewGuid():N}");
            Directory.CreateDirectory(FullPath);
        }

        public string FullPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(FullPath))
            {
                Directory.Delete(FullPath, recursive: true);
            }
        }
    }
}
