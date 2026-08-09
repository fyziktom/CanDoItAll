using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure;

namespace CanDoItAll.Manager;

public sealed record CapsuleRecord(
    string SymbolId,
    string RelativePath,
    string Kind,
    string Name,
    string Summary,
    string Owns,
    string Dependencies,
    string Risks,
    string Tests,
    DateTimeOffset UpdatedAtUtc);

public sealed record CapsuleCoverageSummary(
    int TotalFiles,
    int CoveredFiles,
    int SkippedFiles,
    int MissingFiles,
    int MalformedFiles,
    IReadOnlyList<string> MissingPaths,
    IReadOnlyList<string> MalformedPaths,
    DateTimeOffset RefreshedAtUtc)
{
    public bool HasDrift => MissingFiles > 0 || MalformedFiles > 0;
}

public interface ICapsuleCatalogService
{
    Task RefreshAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<CapsuleRecord> GetIndex();

    CapsuleCoverageSummary GetCoverage();

    CapsuleRecord? GetSymbol(string symbolId);

    IReadOnlyList<CapsuleRecord> GetChangedSince(DateTimeOffset sinceUtc);
}

/* codex-capsule
kind: service
name: CapsuleCatalogService
summary: Scans source files for codex capsule comments, writes artifacts, and reports coverage or drift.
owns: capsule-index, capsule-coverage, changed-capsules
deps: ManagerOptions
risks: false-required-files, malformed-capsule-parse
tests: unit:CapsuleCatalogServiceTests
inputs: source files under workspace root
outputs: capsule json and markdown artifacts
*/
public sealed class CapsuleCatalogService(
    ILogger<CapsuleCatalogService> logger,
    IConfiguration configuration,
    DurableFileWriter durableFileWriter) : ICapsuleCatalogService
{
    private static readonly string[] RequiredFields = ["kind", "name", "summary", "owns", "deps", "risks", "tests"];

    private readonly ManagerOptions _options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new();
    private readonly object _gate = new();
    private List<CapsuleRecord> _records = [];
    private CapsuleCoverageSummary _coverage = new(0, 0, 0, 0, 0, [], [], DateTimeOffset.MinValue);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.WorkspaceRoot));
        var files = Directory.GetFiles(workspaceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.artifacts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(
                path => NormalizeEnumerationKey(Path.GetRelativePath(workspaceRoot, path)),
                StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToList();

        var records = new List<CapsuleRecord>();
        var missing = new List<string>();
        var malformed = new List<string>();
        var skipped = 0;

        foreach (var file in files)
        {
            var text = await File.ReadAllTextAsync(file, cancellationToken);
            var relativePath = NormalizeEnumerationKey(Path.GetRelativePath(workspaceRoot, file));
            if (text.Contains("codex-capsule-skip", StringComparison.Ordinal))
            {
                skipped++;
                continue;
            }

            var required = IsCapsuleRequired(file, text);
            var outcome = ParseCapsule(text, relativePath);
            if (!outcome.HasCapsule)
            {
                if (required)
                {
                    missing.Add(relativePath);
                }

                continue;
            }

            if (outcome.ErrorSummary is not null)
            {
                malformed.Add($"{relativePath}: {outcome.ErrorSummary}");
                continue;
            }

            records.Add(outcome.Record!);
        }

        var refreshedAtUtc = DateTimeOffset.UtcNow;
        var coverage = new CapsuleCoverageSummary(
            files.Count,
            records.Count,
            skipped,
            missing.Count,
            malformed.Count,
            missing,
            malformed,
            refreshedAtUtc);

        lock (_gate)
        {
            _records = records;
            _coverage = coverage;
        }

        logger.LogInformation(
            "Capsule refresh complete. Covered {Covered}/{Total}, skipped {Skipped}, missing {Missing}, malformed {Malformed}.",
            coverage.CoveredFiles,
            coverage.TotalFiles,
            coverage.SkippedFiles,
            coverage.MissingFiles,
            coverage.MalformedFiles);

        await WriteArtifactsAsync(workspaceRoot, records, coverage, cancellationToken);
    }

    private static string NormalizeEnumerationKey(string path)
        => path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    public IReadOnlyList<CapsuleRecord> GetIndex()
    {
        lock (_gate)
        {
            return _records.ToList();
        }
    }

    public CapsuleCoverageSummary GetCoverage()
    {
        lock (_gate)
        {
            return _coverage;
        }
    }

    public CapsuleRecord? GetSymbol(string symbolId)
    {
        lock (_gate)
        {
            return _records.FirstOrDefault(record => string.Equals(record.SymbolId, symbolId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<CapsuleRecord> GetChangedSince(DateTimeOffset sinceUtc)
    {
        lock (_gate)
        {
            return _records.Where(record => record.UpdatedAtUtc >= sinceUtc).ToList();
        }
    }

    private async Task WriteArtifactsAsync(string workspaceRoot, IReadOnlyList<CapsuleRecord> records, CapsuleCoverageSummary coverage, CancellationToken cancellationToken)
    {
        var artifactsRoot = Path.Combine(workspaceRoot, _options.CapsuleArtifactsRoot);
        var symbolsRoot = Path.Combine(artifactsRoot, "symbols");
        await using IAsyncDisposable coordination = await durableFileWriter.AcquireCoordinationAsync(
            workspaceRoot,
            Path.Combine(artifactsRoot, ".capsule-catalog.candoitall.lock"),
            TimeSpan.FromSeconds(15),
            requirePrivateUnixMode: false,
            cancellationToken);
        durableFileWriter.EnsureDirectory(workspaceRoot, artifactsRoot, requirePrivateUnixMode: false);
        durableFileWriter.EnsureDirectory(workspaceRoot, symbolsRoot, requirePrivateUnixMode: false);

        var desiredSymbolFiles = records
            .Select(record => Path.Combine(symbolsRoot, $"{record.SymbolId}.json"))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var existingFile in Directory.GetFiles(symbolsRoot, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                     .ThenBy(path => path, StringComparer.Ordinal))
        {
            if (!desiredSymbolFiles.Contains(existingFile))
            {
                await durableFileWriter.DeleteAsync(
                    workspaceRoot,
                    existingFile,
                    cancellationToken: cancellationToken);
            }
        }

        foreach (var record in records.OrderBy(record => record.SymbolId, StringComparer.Ordinal))
        {
            await durableFileWriter.WriteTextAsync(
                workspaceRoot,
                Path.Combine(symbolsRoot, $"{record.SymbolId}.json"),
                JsonSerializer.Serialize(record, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
                cancellationToken: cancellationToken);
        }

        await durableFileWriter.WriteTextAsync(
            workspaceRoot,
            Path.Combine(artifactsRoot, "index.json"),
            JsonSerializer.Serialize(records, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
            cancellationToken: cancellationToken);
        await durableFileWriter.WriteTextAsync(
            workspaceRoot,
            Path.Combine(artifactsRoot, "coverage.json"),
            JsonSerializer.Serialize(coverage, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
            cancellationToken: cancellationToken);
        await durableFileWriter.WriteTextAsync(
            workspaceRoot,
            Path.Combine(artifactsRoot, "index.md"),
            string.Join(
                Environment.NewLine,
                [
                    $"# Capsule Index ({records.Count})",
                    $"Missing: {coverage.MissingFiles}",
                    $"Malformed: {coverage.MalformedFiles}",
                    string.Empty,
                    .. records.Select(record => $"- `{record.SymbolId}` {record.Kind} {record.Name} ({record.RelativePath})")
                ]),
            cancellationToken: cancellationToken);
    }

    private static CapsuleParseOutcome ParseCapsule(string text, string relativePath)
    {
        var match = Regex.Match(text, @"codex-capsule(?<body>[\s\S]*?)\*/", RegexOptions.Multiline);
        if (!match.Success)
        {
            return CapsuleParseOutcome.NoCapsule;
        }

        var fields = match.Groups["body"].Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('*', ' ', '@'))
            .Where(line => line.Contains(':'))
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        var missingFields = RequiredFields.Where(field => !fields.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value)).ToList();
        if (missingFields.Count > 0)
        {
            return new CapsuleParseOutcome(true, null, $"Missing required fields: {string.Join(", ", missingFields)}.");
        }

        var record = new CapsuleRecord(
            BuildSymbolId(fields["kind"], fields["name"]),
            relativePath,
            fields["kind"].Trim(),
            fields["name"].Trim(),
            fields["summary"].Trim(),
            fields["owns"].Trim(),
            fields["deps"].Trim(),
            fields["risks"].Trim(),
            fields["tests"].Trim(),
            DateTimeOffset.UtcNow);
        return new CapsuleParseOutcome(true, record, null);
    }

    private static string BuildSymbolId(string kind, string name)
        => Regex.Replace($"{kind}-{name}".Trim().ToLowerInvariant(), @"[^a-z0-9\-]+", string.Empty);

    private static bool IsCapsuleRequired(string path, string text)
    {
        var fileName = Path.GetFileName(path);
        if (fileName is "Class1.cs" or "Component1.razor" or "ExampleJsInterop.cs")
        {
            return false;
        }

        return text.Contains("public sealed", StringComparison.Ordinal) ||
               text.Contains("public static", StringComparison.Ordinal) ||
               text.Contains("@page", StringComparison.Ordinal) ||
               text.Contains("public interface", StringComparison.Ordinal);
    }

    private sealed record CapsuleParseOutcome(bool HasCapsule, CapsuleRecord? Record, string? ErrorSummary)
    {
        public static CapsuleParseOutcome NoCapsule { get; } = new(false, null, null);
    }
}
