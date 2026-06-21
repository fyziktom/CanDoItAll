using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryAutomationSettingsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryAutomationSettingsService
{
    public async ValueTask<CognitiveMemoryAutomationSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<CognitiveMemoryAutomationSettingsRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                settings => settings.SettingsKey == CognitiveMemoryAutomationSettingsKeys.Default,
                cancellationToken);

        return record is null
            ? CognitiveMemoryAutomationSettings.Defaults(clock.GetUtcNow())
            : Map(record);
    }

    public async ValueTask<CognitiveMemoryAutomationSettings> SaveAsync(
        CognitiveMemoryAutomationSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        Validate(update);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var nowUtc = clock.GetUtcNow();
        var record = await dbContext.Set<CognitiveMemoryAutomationSettingsRecord>()
            .SingleOrDefaultAsync(
                settings => settings.SettingsKey == CognitiveMemoryAutomationSettingsKeys.Default,
                cancellationToken);
        if (record is null)
        {
            record = new CognitiveMemoryAutomationSettingsRecord
            {
                SettingsKey = CognitiveMemoryAutomationSettingsKeys.Default,
                CreatedAtUtc = nowUtc,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(record);
        }

        record.IsEnabled = update.IsEnabled;
        record.ScheduleMode = update.ScheduleMode;
        record.NightlyLocalTime = NormalizeLocalTime(update.NightlyLocalTime, nameof(update.NightlyLocalTime));
        record.IdleMinutes = update.IdleMinutes;
        record.ScheduledLocalTimes = SerializeScheduledTimes(update.ScheduledLocalTimes);
        record.AutoIngestProjectStructure = update.AutoIngestProjectStructure;
        record.AutoIngestProcessRuntime = update.AutoIngestProcessRuntime;
        record.AutoConsolidateAfterIngestion = update.AutoConsolidateAfterIngestion;
        record.ModelAccessMode = update.ModelAccessMode;
        record.DefaultProviderProfileId = update.DefaultProviderProfileId;
        record.DefaultAgentId = update.DefaultAgentId;
        record.AllowedProviderProfileIds = SerializeProviderProfileIds(update.AllowedProviderProfileIds);
        record.ExecutionProfilesJson = SerializeModelExecutionProfiles(update.ModelExecutionProfiles);
        record.UpdatedByActorId = CognitiveMemoryGuard.EnsureText(update.UpdatedByActorId, nameof(update.UpdatedByActorId));
        record.UpdatedAtUtc = nowUtc;
        record.ConcurrencyToken = Guid.NewGuid();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(record);
    }

    private static CognitiveMemoryAutomationSettings Map(CognitiveMemoryAutomationSettingsRecord record)
    {
        return new CognitiveMemoryAutomationSettings(
            record.IsEnabled,
            record.ScheduleMode,
            record.NightlyLocalTime,
            record.IdleMinutes,
            DeserializeScheduledTimes(record.ScheduledLocalTimes),
            record.AutoIngestProjectStructure,
            record.AutoIngestProcessRuntime,
            record.AutoConsolidateAfterIngestion,
            record.ModelAccessMode,
            record.DefaultProviderProfileId,
            record.DefaultAgentId,
            DeserializeProviderProfileIds(record.AllowedProviderProfileIds),
            record.UpdatedByActorId,
            record.UpdatedAtUtc)
        {
            ModelExecutionProfiles = DeserializeModelExecutionProfiles(record.ExecutionProfilesJson)
        };
    }

    private static void Validate(CognitiveMemoryAutomationSettingsUpdate update)
    {
        _ = NormalizeLocalTime(update.NightlyLocalTime, nameof(update.NightlyLocalTime));
        if (update.IdleMinutes is < 5 or > 1440)
        {
            throw new ArgumentOutOfRangeException(nameof(update.IdleMinutes), "Idle minutes must be between 5 and 1440.");
        }

        foreach (var scheduledTime in update.ScheduledLocalTimes)
        {
            _ = NormalizeLocalTime(scheduledTime, nameof(update.ScheduledLocalTimes));
        }

        if (update.ModelAccessMode == CognitiveMemoryModelAccessMode.SelectedProvidersOnly &&
            update.DefaultProviderProfileId is null &&
            NormalizeProviderProfileIds(update.AllowedProviderProfileIds).Count == 0)
        {
            throw new ArgumentException(
                "Selected provider access requires a default provider or at least one allowed provider.",
                nameof(update.AllowedProviderProfileIds));
        }

        _ = NormalizeModelExecutionProfiles(update.ModelExecutionProfiles);
    }

    private static string NormalizeLocalTime(string value, string parameterName)
    {
        var text = CognitiveMemoryGuard.EnsureText(value, parameterName);
        return TimeOnly.TryParse(text, out var time)
            ? time.ToString("HH:mm")
            : throw new ArgumentException("Time values must use HH:mm local time format.", parameterName);
    }

    private static string SerializeScheduledTimes(IReadOnlyList<string> values)
    {
        return string.Join(
            "\n",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => NormalizeLocalTime(value, nameof(values)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static IReadOnlyList<string> DeserializeScheduledTimes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string SerializeProviderProfileIds(IReadOnlyList<Guid> values)
    {
        return string.Join(
            "\n",
            NormalizeProviderProfileIds(values).Select(value => value.ToString("D")));
    }

    private static IReadOnlyList<Guid> DeserializeProviderProfileIds(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var providerId) ? providerId : Guid.Empty)
            .Where(providerId => providerId != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<Guid> NormalizeProviderProfileIds(IReadOnlyList<Guid> values)
        => values
            .Where(providerId => providerId != Guid.Empty)
            .Distinct()
            .OrderBy(providerId => providerId)
            .ToList();

    private static string SerializeModelExecutionProfiles(IReadOnlyList<CognitiveMemoryModelExecutionProfile> values)
        => JsonSerializer.Serialize(
            NormalizeModelExecutionProfiles(values).ToArray(),
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryModelExecutionProfileArray);

    private static IReadOnlyList<CognitiveMemoryModelExecutionProfile> DeserializeModelExecutionProfiles(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CognitiveMemoryModelExecutionProfileDefaults.OpenAiProfiles;
        }

        var profiles = JsonSerializer.Deserialize(
            value,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryModelExecutionProfileArray);
        return NormalizeModelExecutionProfiles(profiles ?? []);
    }

    private static IReadOnlyList<CognitiveMemoryModelExecutionProfile> NormalizeModelExecutionProfiles(
        IReadOnlyList<CognitiveMemoryModelExecutionProfile> values)
    {
        if (values.Count == 0)
        {
            return CognitiveMemoryModelExecutionProfileDefaults.OpenAiProfiles;
        }

        var profiles = values
            .Select(NormalizeModelExecutionProfile)
            .GroupBy(profile => profile.Role)
            .Select(group => group.Last())
            .OrderBy(profile => profile.Role)
            .ToList();

        var missingRoles = Enum.GetValues<CognitiveMemoryModelExecutionRole>()
            .Where(role => profiles.All(profile => profile.Role != role))
            .Select(CognitiveMemoryModelExecutionProfileDefaults.CreateOpenAi);
        profiles.AddRange(missingRoles);

        return profiles.OrderBy(profile => profile.Role).ToList();
    }

    private static CognitiveMemoryModelExecutionProfile NormalizeModelExecutionProfile(CognitiveMemoryModelExecutionProfile profile)
    {
        var modelId = new CognitiveMemoryExecutionModelId(profile.ModelId.Value);
        if (profile.MaxOutputTokens is < 256 or > 65536)
        {
            throw new ArgumentOutOfRangeException(nameof(profile.MaxOutputTokens), "Max output tokens must be between 256 and 65536.");
        }

        if (profile.TimeoutSeconds is < 5 or > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(profile.TimeoutSeconds), "Timeout seconds must be between 5 and 600.");
        }

        return profile with
        {
            ModelId = modelId,
            Notes = profile.Notes?.Trim() ?? string.Empty
        };
    }
}

public sealed class CognitiveMemoryExternalSourceIngestionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ILogger<CognitiveMemoryExternalSourceIngestionService> logger) : ICognitiveMemoryExternalSourceIngestionService
{
    private const string UploadedFileSourceSystem = "ExternalFile";
    private const string WebsiteSourceSystem = "ExternalWebsite";
    private const string UploadedFileChunkType = "UploadedFileChunk";
    private const string WebsiteChunkType = "WebsiteLinkChunk";
    private static readonly Regex MarkdownSectionHeadingRegex = new(
        @"(?m)^##+\s+(?<title>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    public async ValueTask<CognitiveMemoryExternalSourceIngestResult> IngestFileAsync(
        Guid? projectId,
        string fileName,
        string contentType,
        Stream content,
        long contentLength,
        string actorId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var normalizedFileName = CognitiveMemoryGuard.EnsureText(fileName, nameof(fileName));
        EnsureFileSizeWithinLimit(contentLength);
        var contentText = await ExtractFileTextAsync(
            normalizedFileName,
            contentType,
            content,
            cancellationToken);

        return await IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.UploadedFile,
            projectId,
            Path.GetFileName(normalizedFileName),
            normalizedFileName,
            contentText,
            string.IsNullOrWhiteSpace(contentType) ? "text/plain" : contentType.Trim(),
            contentLength,
            actorId,
            idempotencyKey), cancellationToken);
    }

    public async ValueTask<CognitiveMemoryExternalSourceIngestResult> IngestWebsiteAsync(
        Guid? projectId,
        Uri uri,
        string actorId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Only HTTP and HTTPS URLs can be ingested.", nameof(uri));
        }

        CognitiveMemoryExternalSourceIngestionPolicy.EnsureUriAllowed(uri);
        using var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            throw new InvalidOperationException($"Website returned {(int)response.StatusCode} and cannot be ingested.");
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var contentText = await ExtractWebsiteTextAsync(
            uri,
            response.Content.Headers.ContentType?.MediaType ?? "text/plain",
            stream,
            cancellationToken);
        var title = ResolveWebsiteTitle(uri, contentText);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/plain";

        return await IngestAsync(new CognitiveMemoryExternalSourceIngestRequest(
            CognitiveMemoryExternalSourceKind.WebsiteLink,
            projectId,
            title,
            uri.AbsoluteUri,
            contentText,
            contentType,
            contentText.Length,
            actorId,
            idempotencyKey), cancellationToken);
    }

    public async ValueTask<CognitiveMemoryExternalSourceIngestResult> IngestAsync(
        CognitiveMemoryExternalSourceIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var nowUtc = clock.GetUtcNow();
        var operation = new CognitiveMemoryExternalSourceIngestionRecord
        {
            ProjectId = request.ProjectId,
            SourceKind = request.SourceKind,
            Status = CognitiveMemoryExternalSourceIngestionStatus.Running,
            Title = Truncate(request.Title, 300),
            Locator = Truncate(request.Locator, 1000),
            ContentType = Truncate(request.ContentType, 120),
            ContentLength = request.ContentLength,
            ProgressPercent = 10,
            StatusMessage = "Starting ingestion.",
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(operation);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            CognitiveMemoryExternalSourceIngestionPolicy.EnsureContentAllowed(request.ContentText);
            var persisted = await PersistSourceAsync(dbContext, operation, request, nowUtc, cancellationToken);
            operation.Status = CognitiveMemoryExternalSourceIngestionStatus.Succeeded;
            operation.ProgressPercent = 100;
            operation.StatusMessage = $"{persisted.SourceChunkCount} source chunk(s) and evidence anchor(s) were ingested.";
            operation.SourceManifestId = persisted.ManifestId;
            operation.SourceItemId = persisted.SourceItemId;
            operation.EvidenceAnchorId = persisted.EvidenceAnchorId;
            operation.CompletedAtUtc = clock.GetUtcNow();
            operation.UpdatedAtUtc = operation.CompletedAtUtc.Value;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Map(operation);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            operation.Status = CognitiveMemoryExternalSourceIngestionStatus.Failed;
            operation.ProgressPercent = 100;
            operation.StatusMessage = "External source ingestion failed.";
            operation.FailureMessage = Truncate(exception.Message, 4000);
            operation.CompletedAtUtc = clock.GetUtcNow();
            operation.UpdatedAtUtc = operation.CompletedAtUtc.Value;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogError(
                exception,
                "Cognitive memory external source ingestion failed. OperationId={OperationId} SourceKind={SourceKind} Locator={Locator}",
                operation.Id,
                operation.SourceKind,
                CognitiveMemoryExternalSourceIngestionPolicy.SafeLocatorForLog(operation.Locator));

            return Map(operation);
        }
    }

    public async ValueTask<CognitiveMemoryExternalSourceIngestResult?> GetAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var operation = await dbContext.Set<CognitiveMemoryExternalSourceIngestionRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);

        return operation is null ? null : Map(operation);
    }

    private static async Task<CognitiveMemoryExternalSourcePersistenceResult> PersistSourceAsync(
        AppDbContext dbContext,
        CognitiveMemoryExternalSourceIngestionRecord operation,
        CognitiveMemoryExternalSourceIngestRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var sourceSystem = request.SourceKind == CognitiveMemoryExternalSourceKind.WebsiteLink
            ? WebsiteSourceSystem
            : UploadedFileSourceSystem;
        var sourceScopeKey = request.ProjectId?.ToString("D") ?? "global";
        var wholeContentHash = CognitiveMemoryHash.FromUtf8(request.ContentText).Value;
        var snapshotId = CognitiveMemoryHash.FromUtf8($"{sourceSystem}|{sourceScopeKey}|{request.Locator}|{wholeContentHash}").Value;
        var chunks = CreateSourceChunks(request);

        var run = new CognitiveMemoryRunRecord
        {
            ProjectId = request.ProjectId,
            RunKind = CognitiveMemoryRunKind.SourceScan,
            Status = CognitiveMemoryRunStatus.Succeeded,
            OperationMode = CognitiveMemoryOperationMode.Observe,
            IdempotencyKey = CreateExternalSourceRunIdempotencyKey(request.IdempotencyKey, operation.Id),
            InputHash = snapshotId,
            AlgorithmVersion = "external-source-ingestion-v1",
            Cursor = string.Empty,
            StartedAtUtc = nowUtc,
            CompletedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(run);

        var manifest = await dbContext.Set<CognitiveMemorySourceManifestRecord>()
            .SingleOrDefaultAsync(item =>
                item.SourceSystem == sourceSystem &&
                item.SourceScopeKey == sourceScopeKey &&
                item.SourceSnapshotId == snapshotId,
                cancellationToken);
        if (manifest is null)
        {
            manifest = new CognitiveMemorySourceManifestRecord
            {
                ProjectId = request.ProjectId,
                SourceSystem = sourceSystem,
                SourceScopeKey = sourceScopeKey,
                SourceSnapshotId = snapshotId,
                SnapshotHash = snapshotId,
                ProviderVersion = "external-source-ingestion-v1",
                ScanStatus = CognitiveMemoryRunStatus.Succeeded,
                ObservedAtUtc = nowUtc,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(manifest);
        }
        else
        {
            manifest.ProjectId = request.ProjectId;
            manifest.ScanStatus = CognitiveMemoryRunStatus.Succeeded;
            manifest.ObservedAtUtc = nowUtc;
            manifest.UpdatedAtUtc = nowUtc;
            manifest.ConcurrencyToken = Guid.NewGuid();
        }

        operation.ProgressPercent = 45;
        operation.StatusMessage = "Source manifest was recorded.";
        operation.UpdatedAtUtc = nowUtc;
        await dbContext.SaveChangesAsync(cancellationToken);

        Guid? primarySourceItemId = null;
        Guid? primaryEvidenceAnchorId = null;
        var chunkIndex = 0;
        foreach (var chunk in chunks)
        {
            chunkIndex++;
            var sourceItemKey = Truncate($"{sourceSystem.ToLowerInvariant()}:{chunk.ContentHash}", 500);
            var sourceItem = await dbContext.Set<CognitiveMemorySourceItemRecord>()
                .SingleOrDefaultAsync(item =>
                    item.SourceManifestId == manifest.Id &&
                    item.SourceItemKey == sourceItemKey,
                    cancellationToken);
            if (sourceItem is null)
            {
                sourceItem = new CognitiveMemorySourceItemRecord
                {
                    SourceManifestId = manifest.Id,
                    ProjectId = request.ProjectId,
                    SourceSystem = sourceSystem,
                    SourceItemKey = sourceItemKey,
                    SourceItemType = chunks.Count == 1 ? request.SourceKind.ToString() : chunk.SourceItemType,
                    CreatedAtUtc = nowUtc,
                    ConcurrencyToken = Guid.NewGuid()
                };
                dbContext.Add(sourceItem);
            }

            sourceItem.Title = Truncate(chunk.Title, 300);
            sourceItem.ContentText = chunk.ContentText;
            sourceItem.Locator = Truncate(chunk.Locator, 1000);
            sourceItem.ContentHash = chunk.ContentHash;
            sourceItem.RedactionState = CognitiveMemoryRedactionState.Safe;
            sourceItem.AccessLevel = request.ProjectId.HasValue ? CognitiveMemoryAccessLevel.Project : CognitiveMemoryAccessLevel.Public;
            sourceItem.AccessScope = Truncate(request.ProjectId?.ToString("D") ?? "global", 240);
            sourceItem.ProvenanceJson = SerializeProvenance(request, sourceSystem, chunk.ContentHash);
            sourceItem.ObservedAtUtc = nowUtc;
            sourceItem.UpdatedAtUtc = nowUtc;
            sourceItem.ConcurrencyToken = Guid.NewGuid();

            operation.ProgressPercent = 45 + Math.Min(30, chunkIndex * 30 / chunks.Count);
            operation.StatusMessage = $"Source chunk {chunkIndex} of {chunks.Count} was recorded.";
            operation.UpdatedAtUtc = nowUtc;
            await dbContext.SaveChangesAsync(cancellationToken);

            var evidenceAnchor = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
                .SingleOrDefaultAsync(anchor =>
                    anchor.SourceItemId == sourceItem.Id &&
                    anchor.SourceHash == chunk.ContentHash,
                    cancellationToken);
            if (evidenceAnchor is null)
            {
                evidenceAnchor = new CognitiveMemoryEvidenceAnchorRecord
                {
                    ProjectId = request.ProjectId,
                    AnchorKind = request.SourceKind == CognitiveMemoryExternalSourceKind.UploadedFile
                        ? CognitiveMemoryEvidenceAnchorKind.FilePath
                        : CognitiveMemoryEvidenceAnchorKind.TextSpan,
                    SourceManifestId = manifest.Id,
                    SourceItemId = sourceItem.Id,
                    SourceSystem = sourceSystem,
                    Locator = Truncate(chunk.Locator, 1000),
                    StructuredPath = chunk.StructuredPath,
                    TextStart = chunk.TextStart,
                    TextEnd = chunk.TextEnd,
                    QuoteHash = chunk.ContentHash,
                    TrustLevel = request.SourceKind == CognitiveMemoryExternalSourceKind.WebsiteLink
                        ? CognitiveMemorySourceTrustLevel.ExternalUnverified
                        : CognitiveMemorySourceTrustLevel.HumanReview,
                    RedactionState = CognitiveMemoryRedactionState.Safe,
                    SourceHash = chunk.ContentHash,
                    ObservedAtUtc = nowUtc,
                    CreatedAtUtc = nowUtc,
                    ConcurrencyToken = Guid.NewGuid()
                };
                dbContext.Add(evidenceAnchor);
            }

            if (primarySourceItemId is null)
            {
                primarySourceItemId = sourceItem.Id;
                primaryEvidenceAnchorId = evidenceAnchor.Id;
            }
        }

        operation.ProgressPercent = 90;
        operation.StatusMessage = $"{chunks.Count} source chunk(s) and evidence anchor(s) were recorded.";
        operation.UpdatedAtUtc = nowUtc;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryExternalSourcePersistenceResult(
            manifest.Id,
            primarySourceItemId ?? throw new InvalidOperationException("External source ingestion did not create a source item."),
            primaryEvidenceAnchorId ?? throw new InvalidOperationException("External source ingestion did not create an evidence anchor."),
            chunks.Count);
    }

    private static IReadOnlyList<ExternalSourceChunk> CreateSourceChunks(CognitiveMemoryExternalSourceIngestRequest request)
    {
        var markdownSections = CreateMarkdownSectionChunks(request);
        var mermaidMindmapBranches = markdownSections.Count == 0
            ? CreateMermaidMindmapBranchChunks(request)
            : [];
        var baseChunks = markdownSections.Count > 0
            ? markdownSections
            : mermaidMindmapBranches.Count > 0
                ? mermaidMindmapBranches
                : [CreateWholeSourceChunk(request)];
        return baseChunks
            .SelectMany(SplitLargeChunk)
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.ContentText))
            .ToArray();
    }

    private static IReadOnlyList<ExternalSourceChunk> CreateMarkdownSectionChunks(CognitiveMemoryExternalSourceIngestRequest request)
    {
        if (!LooksLikeMarkdown(request))
        {
            return [];
        }

        var text = request.ContentText.Trim();
        var matches = MarkdownSectionHeadingRegex.Matches(text);
        if (matches.Count == 0)
        {
            return [];
        }

        var chunks = new List<ExternalSourceChunk>(matches.Count + 1);
        if (matches[0].Index >= CognitiveMemoryExternalSourceIngestionLimits.MinChunkCharacters)
        {
            var overview = text[..matches[0].Index].Trim();
            chunks.Add(CreateChunk(
                request,
                Truncate($"{request.Title} - Overview", 300),
                $"{request.Locator}#overview",
                "markdown.overview",
                overview,
                0,
                matches[0].Index));
        }

        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var start = match.Index;
            var end = index + 1 < matches.Count ? matches[index + 1].Index : text.Length;
            var content = text[start..end].Trim();
            if (content.Length < CognitiveMemoryExternalSourceIngestionLimits.MinChunkCharacters)
            {
                continue;
            }

            var heading = CleanHeading(match.Groups["title"].Value);
            chunks.Add(CreateChunk(
                request,
                Truncate($"{request.Title} - {heading}", 300),
                $"{request.Locator}#section-{index + 1:D2}-{CreateSlug(heading)}",
                $"markdown.section[{index}]",
                content,
                start,
                end));
        }

        return chunks;
    }

    private static IReadOnlyList<ExternalSourceChunk> CreateMermaidMindmapBranchChunks(CognitiveMemoryExternalSourceIngestRequest request)
    {
        if (!LooksLikeMermaidMindmap(request))
        {
            return [];
        }

        var text = request.ContentText.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var lines = ReadLineSegments(text);
        if (lines.Count < 3)
        {
            return [];
        }

        var rootIndex = lines.FindIndex(line => line.Text.TrimStart().StartsWith("root", StringComparison.OrdinalIgnoreCase));
        if (rootIndex < 1)
        {
            return [];
        }

        var rootIndent = lines[rootIndex].Indent;
        var branchIndent = lines
            .Skip(rootIndex + 1)
            .Where(line => !string.IsNullOrWhiteSpace(line.Text) && line.Indent > rootIndent)
            .Select(line => line.Indent)
            .DefaultIfEmpty(-1)
            .Min();
        if (branchIndent <= rootIndent)
        {
            return [];
        }

        var branchIndexes = lines
            .Select((line, index) => new { Line = line, Index = index })
            .Where(item => item.Index > rootIndex && item.Line.Indent == branchIndent && !string.IsNullOrWhiteSpace(item.Line.Text))
            .Select(item => item.Index)
            .ToArray();
        if (branchIndexes.Length == 0)
        {
            return [];
        }

        var chunks = new List<ExternalSourceChunk>(branchIndexes.Length);
        for (var index = 0; index < branchIndexes.Length; index++)
        {
            var startLineIndex = branchIndexes[index];
            var endLineIndex = index + 1 < branchIndexes.Length ? branchIndexes[index + 1] : lines.Count;
            var contentBuilder = new StringBuilder();
            contentBuilder.AppendLine(lines[0].Text);
            contentBuilder.AppendLine(lines[rootIndex].Text);
            for (var lineIndex = startLineIndex; lineIndex < endLineIndex; lineIndex++)
            {
                contentBuilder.AppendLine(lines[lineIndex].Text);
            }

            var content = contentBuilder.ToString().Trim();
            if (content.Length < CognitiveMemoryExternalSourceIngestionLimits.MinChunkCharacters)
            {
                continue;
            }

            var branchTitle = CleanMindmapNodeTitle(lines[startLineIndex].Text);
            chunks.Add(CreateChunk(
                request,
                Truncate($"{request.Title} - {branchTitle}", 300),
                $"{request.Locator}#mindmap-branch-{index + 1:D2}-{CreateSlug(branchTitle)}",
                $"mermaid.mindmap.branch[{index}]",
                content,
                lines[startLineIndex].Start,
                lines[endLineIndex - 1].End));
        }

        return chunks;
    }

    private static ExternalSourceChunk CreateWholeSourceChunk(CognitiveMemoryExternalSourceIngestRequest request)
        => CreateChunk(
            request,
            request.Title,
            request.Locator,
            request.SourceKind.ToString(),
            request.ContentText.Trim(),
            0,
            request.ContentText.Length);

    private static IReadOnlyList<ExternalSourceChunk> SplitLargeChunk(ExternalSourceChunk chunk)
    {
        if (chunk.ContentText.Length <= CognitiveMemoryExternalSourceIngestionLimits.MaxChunkCharacters)
        {
            return [chunk];
        }

        var parts = new List<ExternalSourceChunk>();
        var offset = 0;
        var part = 0;
        while (offset < chunk.ContentText.Length)
        {
            var remaining = chunk.ContentText.Length - offset;
            var take = Math.Min(CognitiveMemoryExternalSourceIngestionLimits.MaxChunkCharacters, remaining);
            if (remaining > CognitiveMemoryExternalSourceIngestionLimits.MaxChunkCharacters)
            {
                var boundary = chunk.ContentText.LastIndexOf("\n\n", offset + take - 1, take, StringComparison.Ordinal);
                if (boundary > offset + CognitiveMemoryExternalSourceIngestionLimits.MinChunkCharacters)
                {
                    take = boundary - offset;
                }
            }

            part++;
            var content = chunk.ContentText.Substring(offset, take).Trim();
            parts.Add(chunk with
            {
                Title = Truncate($"{chunk.Title} ({part})", 300),
                Locator = Truncate($"{chunk.Locator}#part-{part:D2}", 1000),
                StructuredPath = $"{chunk.StructuredPath}.part[{part - 1}]",
                ContentText = content,
                ContentHash = CognitiveMemoryHash.FromUtf8(content).Value,
                TextStart = chunk.TextStart + offset,
                TextEnd = chunk.TextStart + offset + take
            });
            offset += take;
        }

        return parts;
    }

    private static ExternalSourceChunk CreateChunk(
        CognitiveMemoryExternalSourceIngestRequest request,
        string title,
        string locator,
        string structuredPath,
        string content,
        int textStart,
        int textEnd)
    {
        var normalized = content.Trim();
        return new ExternalSourceChunk(
            request.SourceKind == CognitiveMemoryExternalSourceKind.WebsiteLink ? WebsiteChunkType : UploadedFileChunkType,
            title,
            Truncate(locator, 1000),
            structuredPath,
            normalized,
            CognitiveMemoryHash.FromUtf8(normalized).Value,
            textStart,
            textEnd);
    }

    private static bool LooksLikeMarkdown(CognitiveMemoryExternalSourceIngestRequest request)
        => request.ContentType.Contains("markdown", StringComparison.OrdinalIgnoreCase) ||
           request.Locator.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            request.ContentText.Contains("\n## ", StringComparison.Ordinal);

    private static bool LooksLikeMermaidMindmap(CognitiveMemoryExternalSourceIngestRequest request)
    {
        var trimmed = request.ContentText.TrimStart();
        return request.Locator.EndsWith(".mmd", StringComparison.OrdinalIgnoreCase) ||
            request.ContentType.Contains("mermaid", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("mindmap", StringComparison.OrdinalIgnoreCase);
    }

    private static List<LineSegment> ReadLineSegments(string text)
    {
        var lines = new List<LineSegment>();
        var offset = 0;
        while (offset <= text.Length)
        {
            var nextBreak = text.IndexOf('\n', offset);
            var end = nextBreak < 0 ? text.Length : nextBreak;
            var line = text[offset..end];
            lines.Add(new LineSegment(line, offset, end, CountLeadingSpaces(line)));
            if (nextBreak < 0)
            {
                break;
            }

            offset = nextBreak + 1;
        }

        return lines;
    }

    private static int CountLeadingSpaces(string value)
    {
        var count = 0;
        while (count < value.Length && value[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static string CleanMindmapNodeTitle(string value)
    {
        var text = CleanHeading(value);
        while (text.StartsWith("(", StringComparison.Ordinal) ||
               text.StartsWith("[", StringComparison.Ordinal) ||
               text.StartsWith("{", StringComparison.Ordinal))
        {
            text = text[1..].TrimStart();
        }

        while (text.EndsWith(")", StringComparison.Ordinal) ||
               text.EndsWith("]", StringComparison.Ordinal) ||
               text.EndsWith("}", StringComparison.Ordinal))
        {
            text = text[..^1].TrimEnd();
        }

        return string.IsNullOrWhiteSpace(text) ? "Mindmap branch" : text;
    }

    private static string CleanHeading(string value)
        => string.Join(" ", value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string CreateSlug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "section" : Truncate(slug, 80);
    }

    private static CognitiveMemoryExternalSourceIngestResult Map(CognitiveMemoryExternalSourceIngestionRecord record)
    {
        return new CognitiveMemoryExternalSourceIngestResult(
            record.Id,
            record.SourceKind,
            record.Status,
            record.ProgressPercent,
            record.StatusMessage,
            record.ProjectId,
            record.SourceManifestId,
            record.SourceItemId,
            record.EvidenceAnchorId,
            string.IsNullOrWhiteSpace(record.FailureMessage) ? null : record.FailureMessage);
    }

    private static void Validate(CognitiveMemoryExternalSourceIngestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = CognitiveMemoryGuard.EnsureText(request.Title, nameof(request.Title));
        _ = CognitiveMemoryGuard.EnsureText(request.Locator, nameof(request.Locator));
        _ = CognitiveMemoryGuard.EnsureText(request.ContentText, nameof(request.ContentText));
        _ = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId));
        if (request.ContentLength > CognitiveMemoryExternalSourceIngestionLimits.MaxFileBytes)
        {
            throw new InvalidOperationException($"External source content is limited to {FormatBytes(CognitiveMemoryExternalSourceIngestionLimits.MaxFileBytes)}.");
        }

        if (request.ContentText.Length > CognitiveMemoryExternalSourceIngestionLimits.MaxTextCharacters)
        {
            throw new InvalidOperationException($"External source text exceeds the {CognitiveMemoryExternalSourceIngestionLimits.MaxTextCharacters} character ingestion limit.");
        }
    }

    private static string ResolveWebsiteTitle(Uri uri, string contentText)
    {
        const string startMarker = "<title>";
        const string endMarker = "</title>";
        var start = contentText.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return uri.Host;
        }

        start += startMarker.Length;
        var end = contentText.IndexOf(endMarker, start, StringComparison.OrdinalIgnoreCase);
        if (end <= start)
        {
            return uri.Host;
        }

        var title = WebUtility.HtmlDecode(contentText[start..end]).Trim();
        return string.IsNullOrWhiteSpace(title) ? uri.Host : title;
    }

    private static string CreateExternalSourceRunIdempotencyKey(string? requestedKey, Guid operationId)
    {
        var operationKey = operationId.ToString("N");
        return string.IsNullOrWhiteSpace(requestedKey)
            ? $"external-source:{operationKey}"
            : $"external-source:{CognitiveMemoryHash.FromUtf8(requestedKey.Trim()).Value[..16]}:{operationKey}";
    }

    private static string SerializeProvenance(
        CognitiveMemoryExternalSourceIngestRequest request,
        string sourceSystem,
        string contentHash)
    {
        var payload = new Dictionary<string, string>
        {
            ["sourceSystem"] = sourceSystem,
            ["sourceKind"] = request.SourceKind.ToString(),
            ["locator"] = request.Locator,
            ["contentType"] = request.ContentType,
            ["contentLength"] = request.ContentLength.ToString(),
            ["contentHash"] = contentHash,
            ["actorId"] = request.ActorId,
            ["ingestedAtUtc"] = DateTimeOffset.UtcNow.ToString("O")
        };

        return JsonSerializer.Serialize(
            payload,
            CognitiveMemoryJsonSerializerContext.Default.DictionaryStringString);
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static async Task<string> ExtractFileTextAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CognitiveMemoryExternalSourceTextExtractor.ExtractAsync(
                fileName,
                contentType,
                content,
                CognitiveMemoryExternalSourceIngestionLimits.MaxTextCharacters,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"External source file '{Path.GetFileName(fileName)}' could not be extracted as text. {exception.Message}",
                exception);
        }
    }

    private static async Task<string> ExtractWebsiteTextAsync(
        Uri uri,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CognitiveMemoryExternalSourceTextExtractor.ExtractAsync(
                uri.AbsolutePath,
                contentType,
                content,
                CognitiveMemoryExternalSourceIngestionLimits.MaxTextCharacters,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"External source website '{uri.Host}' could not be extracted as text. {exception.Message}",
                exception);
        }
    }

    private static void EnsureFileSizeWithinLimit(long contentLength)
    {
        if (contentLength > CognitiveMemoryExternalSourceIngestionLimits.MaxFileBytes)
        {
            throw new InvalidOperationException($"File uploads for cognitive memory ingestion are limited to {FormatBytes(CognitiveMemoryExternalSourceIngestionLimits.MaxFileBytes)}.");
        }
    }

    private static string FormatBytes(long bytes)
        => $"{bytes / 1024 / 1024} MB";

    private sealed record CognitiveMemoryExternalSourcePersistenceResult(
        Guid ManifestId,
        Guid SourceItemId,
        Guid EvidenceAnchorId,
        int SourceChunkCount);

    private sealed record LineSegment(
        string Text,
        int Start,
        int End,
        int Indent);

    private sealed record ExternalSourceChunk(
        string SourceItemType,
        string Title,
        string Locator,
        string StructuredPath,
        string ContentText,
        string ContentHash,
        int TextStart,
        int TextEnd);
}
