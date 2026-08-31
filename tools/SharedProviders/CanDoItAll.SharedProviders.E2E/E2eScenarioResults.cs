using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure;

namespace CanDoItAll.SharedProviders.E2E;

internal enum E2eScenarioStatus
{
    Pending,
    Passed,
    Failed
}

internal sealed record E2eScenarioCheckResult(
    string CheckId,
    bool Passed);

internal sealed record E2eScenarioStageResult(
    E2eScenarioPhase Phase,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long DurationMilliseconds,
    IReadOnlyList<E2eScenarioCheckResult> Checks);

internal sealed record E2eScenarioResult(
    string ScenarioId,
    E2eScenarioStatus Status,
    IReadOnlyList<E2eScenarioPhase> RequiredPhases,
    IReadOnlyList<E2eScenarioStageResult> Stages);

internal sealed record E2eScenarioReport(
    int SchemaVersion,
    DateTimeOffset UpdatedAtUtc,
    E2eScenarioStatus Status,
    int ScenarioCount,
    int PassedCount,
    int FailedCount,
    int PendingCount,
    IReadOnlyList<E2eScenarioResult> Scenarios);

internal sealed class E2eScenarioResultStore
{
    private const int SchemaVersion = 1;
    private const int MaximumMarkerBytes = 256;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false)
        }
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<E2eScenarioPhase>> RequiredPhases =
        new Dictionary<string, IReadOnlyList<E2eScenarioPhase>>(StringComparer.Ordinal)
        {
            [BackendCheckpointScenarioCatalog.CentralCatalogPublicationBoundary] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.ClientATextImportWithPersonalProvider] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.ClientBTextAndImageImports] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.SourceResyncIdempotencyAndStableLocalIds] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.DuplicateUpstreamModelRouting] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.ChatCompletionsAndResponsesBuffered] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.ChatCompletionsAndResponsesStreaming] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.FunctionToolCallRoundtrip] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.StructuredOutputCapabilityAllowDeny] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.OpenAiAndComfyUiImageGeneration] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.CatalogEtagNotModified] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.CatalogAndInferenceScopeIsolation] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.MalformedAccessContextRejected] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.AccessContextCentralOnly] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.UnpublishAndReappearance] =
                [E2eScenarioPhase.Normal, E2eScenarioPhase.Unpublished, E2eScenarioPhase.Republished],
            [BackendCheckpointScenarioCatalog.CentralOutageRecoveryNoFallback] =
                [E2eScenarioPhase.Normal, E2eScenarioPhase.Outage, E2eScenarioPhase.Recovery],
            [BackendCheckpointScenarioCatalog.SourceIdentityMismatch] =
                [E2eScenarioPhase.Normal, E2eScenarioPhase.IdentityMismatch, E2eScenarioPhase.IdentityRestored],
            [BackendCheckpointScenarioCatalog.StreamingDisconnectCancellation] =
                [E2eScenarioPhase.Normal],
            [BackendCheckpointScenarioCatalog.SecretContentAuditRedaction] =
                [E2eScenarioPhase.Normal, E2eScenarioPhase.Recovery]
        };

    private readonly string artifactRoot;
    private readonly DurableFileWriter writer = new(new PhysicalFileSystemPathPolicyFactory());

    public E2eScenarioResultStore(string artifactRoot)
    {
        this.artifactRoot = artifactRoot ?? throw new ArgumentNullException(nameof(artifactRoot));
        ValidateArtifactRoot();
        writer.EnsureDirectory(
            artifactRoot,
            Path.Combine(artifactRoot, "scenario-results"),
            requirePrivateUnixMode: true);
        writer.EnsureDirectory(
            artifactRoot,
            Path.Combine(artifactRoot, "handoff"),
            requirePrivateUnixMode: false);
    }

    public async Task<E2eScenarioReport> MergeAsync(
        E2eScenarioPhase phase,
        IReadOnlyList<E2eScenarioStageEvidence> evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var duplicate = evidence
            .GroupBy(item => item.ScenarioId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null || evidence.Any(item => !RequiredPhases.ContainsKey(item.ScenarioId)))
        {
            throw new E2eSafeException("The scenario phase produced an invalid scenario identity set.");
        }

        var previous = await ReadExistingAsync(cancellationToken);
        var updates = evidence.ToDictionary(
            item => item.ScenarioId,
            item => item.ToStageResult(phase),
            StringComparer.Ordinal);
        var scenarios = BackendCheckpointScenarioCatalog.All
            .Select(scenarioId => MergeScenario(previous, scenarioId, phase, updates))
            .ToArray();
        var passed = scenarios.Count(item => item.Status == E2eScenarioStatus.Passed);
        var failed = scenarios.Count(item => item.Status == E2eScenarioStatus.Failed);
        var pending = scenarios.Length - passed - failed;
        var report = new E2eScenarioReport(
            SchemaVersion,
            DateTimeOffset.UtcNow,
            failed > 0
                ? E2eScenarioStatus.Failed
                : pending > 0
                    ? E2eScenarioStatus.Pending
                    : E2eScenarioStatus.Passed,
            scenarios.Length,
            passed,
            failed,
            pending,
            scenarios);
        var json = JsonSerializer.Serialize(report, JsonOptions);
        await writer.WriteTextAsync(
            artifactRoot,
            Path.Combine(
                artifactRoot,
                "scenario-results",
                $"{E2eScenarioCommandLine.ToToken(phase)}.json"),
            json,
            DurableFileWriteOptions.Default,
            cancellationToken);
        await writer.WriteTextAsync(
            artifactRoot,
            Path.Combine(artifactRoot, "handoff", "scenario-results.json"),
            json,
            DurableFileWriteOptions.Default,
            cancellationToken);
        return report;
    }

    private E2eScenarioResult MergeScenario(
        E2eScenarioReport? previous,
        string scenarioId,
        E2eScenarioPhase phase,
        IReadOnlyDictionary<string, E2eScenarioStageResult> updates)
    {
        var priorStages = previous?.Scenarios
            .Single(item => string.Equals(item.ScenarioId, scenarioId, StringComparison.Ordinal))
            .Stages
            .Where(stage => stage.Phase != phase)
            .ToList() ?? [];
        if (updates.TryGetValue(scenarioId, out var update))
        {
            priorStages.Add(update);
        }

        var stages = priorStages
            .OrderBy(stage => stage.Phase)
            .ToArray();
        var required = RequiredPhases[scenarioId];
        var requiredStages = stages
            .Where(stage => required.Contains(stage.Phase))
            .ToArray();
        var status = requiredStages.Any(stage => stage.Checks.Any(check => !check.Passed))
            ? E2eScenarioStatus.Failed
            : required.All(requiredPhase => requiredStages.Any(stage => stage.Phase == requiredPhase))
                ? E2eScenarioStatus.Passed
                : E2eScenarioStatus.Pending;
        return new E2eScenarioResult(scenarioId, status, required, stages);
    }

    private async Task<E2eScenarioReport?> ReadExistingAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(artifactRoot, "handoff", "scenario-results.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var report = await JsonSerializer.DeserializeAsync<E2eScenarioReport>(
                stream,
                JsonOptions,
                cancellationToken);
            ValidateExisting(report);
            return report;
        }
        catch (JsonException exception)
        {
            throw new E2eSafeException("The existing scenario-results artifact is invalid.", exception);
        }
    }

    private static void ValidateExisting(E2eScenarioReport? report)
    {
        if (report is null ||
            report.SchemaVersion != SchemaVersion ||
            report.ScenarioCount != BackendCheckpointScenarioCatalog.All.Count ||
            !report.Scenarios.Select(item => item.ScenarioId)
                .SequenceEqual(BackendCheckpointScenarioCatalog.All, StringComparer.Ordinal) ||
            report.Scenarios.Any(item =>
                !RequiredPhases.TryGetValue(item.ScenarioId, out var requiredPhases) ||
                !item.RequiredPhases.SequenceEqual(requiredPhases) ||
                item.Stages.GroupBy(stage => stage.Phase).Any(group => group.Count() > 1)))
        {
            throw new E2eSafeException("The existing scenario-results artifact is invalid.");
        }
    }

    private void ValidateArtifactRoot()
    {
        var markerPath = Path.Combine(artifactRoot, E2ePreparationService.ToolStateMarkerFileName);
        if (!TryReadMarker(markerPath, out var marker) ||
            !string.Equals(marker, E2ePreparationService.ToolStateMarkerValue, StringComparison.Ordinal))
        {
            throw new E2eSafeException("The scenario artifact root is not the prepared E2E tool-state root.");
        }

        var rootAttributes = File.GetAttributes(artifactRoot);
        if (rootAttributes.HasFlag(FileAttributes.ReparsePoint) ||
            rootAttributes.HasFlag(FileAttributes.Device))
        {
            throw new E2eSafeException("The scenario artifact root cannot be a symbolic link or reparse point.");
        }
    }

    private static bool TryReadMarker(string path, out string marker)
    {
        marker = string.Empty;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists ||
                info.Attributes.HasFlag(FileAttributes.Directory) ||
                info.Attributes.HasFlag(FileAttributes.Device) ||
                info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                info.Length is <= 0 or > MaximumMarkerBytes)
            {
                return false;
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: MaximumMarkerBytes,
                FileOptions.SequentialScan);
            var bytes = new byte[MaximumMarkerBytes + 1];
            var total = 0;
            while (total < bytes.Length)
            {
                var read = stream.Read(bytes, total, bytes.Length - total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            if (total == 0 || total > MaximumMarkerBytes)
            {
                return false;
            }

            marker = System.Text.Encoding.UTF8.GetString(bytes, 0, total).Trim();
            return !marker.Contains('\0');
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal sealed record E2eScenarioStageEvidence(
    string ScenarioId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<E2eScenarioCheckResult> Checks)
{
    public E2eScenarioStageResult ToStageResult(E2eScenarioPhase phase)
        => new(
            phase,
            StartedAtUtc,
            CompletedAtUtc,
            Math.Max(0, (long)(CompletedAtUtc - StartedAtUtc).TotalMilliseconds),
            Checks);
}

internal sealed class E2eScenarioEvidenceBuilder
{
    private readonly List<E2eScenarioCheckResult> checks = [];

    public void Expect(string checkId, bool condition)
    {
        if (string.IsNullOrWhiteSpace(checkId) ||
            checkId.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            throw new ArgumentException("A scenario check id must be a non-empty safe token.", nameof(checkId));
        }

        checks.Add(new E2eScenarioCheckResult(checkId, condition));
    }

    public IReadOnlyList<E2eScenarioCheckResult> Build()
        => checks.Count > 0
            ? checks.ToArray()
            : throw new InvalidOperationException("A scenario stage must produce at least one check.");
}
