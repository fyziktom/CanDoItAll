using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
internal sealed record CognitiveMemoryRecordSupport(
    Guid RecordId,
    IReadOnlyList<CognitiveMemorySourceLinkRecord> SourceLinks,
    IReadOnlyList<CognitiveMemorySourceItemRecord> SourceItems,
    IReadOnlyList<CognitiveMemoryRecordEvidenceAnchorRecord> EvidenceLinks,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorRecord> EvidenceAnchors,
    IReadOnlyList<CognitiveMemoryClaimRecord> Claims)
{
    public CognitiveMemoryRedactionState HighestRedactionState
        => SourceItems
            .Select(item => item.RedactionState)
            .Concat(EvidenceAnchors.Select(anchor => anchor.RedactionState))
            .DefaultIfEmpty(CognitiveMemoryRedactionState.Unclassified)
            .Max();

    public static CognitiveMemoryRecordSupport Empty(Guid recordId)
        => new(recordId, [], [], [], [], []);
}

internal sealed record CognitiveMemorySupportSnapshot(
    IReadOnlyDictionary<Guid, CognitiveMemoryRecordSupport> ByRecordId,
    IReadOnlyDictionary<Guid, CognitiveMemorySourceItemRecord> SourceItemsById);

internal static class CognitiveMemoryQualitySupportLoader
{
    public static async Task<CognitiveMemorySupportSnapshot> LoadAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> memoryRecordIds,
        CancellationToken cancellationToken)
    {
        if (memoryRecordIds.Count == 0)
        {
            return new CognitiveMemorySupportSnapshot(
                new Dictionary<Guid, CognitiveMemoryRecordSupport>(),
                new Dictionary<Guid, CognitiveMemorySourceItemRecord>());
        }

        var sourceLinks = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => memoryRecordIds.Contains(link.MemoryRecordId))
            .ToListAsync(cancellationToken);
        var sourceItemIds = sourceLinks
            .Select(link => link.SourceItemId)
            .Distinct()
            .ToArray();
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => sourceItemIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var evidenceLinks = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(link => memoryRecordIds.Contains(link.MemoryRecordId))
            .ToListAsync(cancellationToken);
        var evidenceAnchorIds = evidenceLinks
            .Select(link => link.EvidenceAnchorId)
            .Distinct()
            .ToArray();
        var evidenceAnchors = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(anchor => evidenceAnchorIds.Contains(anchor.Id) || (anchor.SourceItemId != null && sourceItemIds.Contains(anchor.SourceItemId.Value)))
            .ToListAsync(cancellationToken);
        var claims = await dbContext.Set<CognitiveMemoryClaimRecord>()
            .AsNoTracking()
            .Where(claim => claim.MemoryRecordId != null && memoryRecordIds.Contains(claim.MemoryRecordId.Value))
            .ToListAsync(cancellationToken);
        var sourceItemsById = sourceItems.ToDictionary(item => item.Id);
        var evidenceAnchorsBySourceItemId = evidenceAnchors
            .Where(anchor => anchor.SourceItemId is not null)
            .GroupBy(anchor => anchor.SourceItemId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var supportByRecordId = new Dictionary<Guid, CognitiveMemoryRecordSupport>();
        foreach (var memoryRecordId in memoryRecordIds)
        {
            var linksForRecord = sourceLinks.Where(link => link.MemoryRecordId == memoryRecordId).ToArray();
            var sourceItemsForRecord = linksForRecord
                .Select(link => sourceItemsById.GetValueOrDefault(link.SourceItemId))
                .OfType<CognitiveMemorySourceItemRecord>()
                .ToArray();
            var evidenceLinksForRecord = evidenceLinks.Where(link => link.MemoryRecordId == memoryRecordId).ToArray();
            var evidenceAnchorIdsForRecord = evidenceLinksForRecord.Select(link => link.EvidenceAnchorId).ToHashSet();
            foreach (var sourceItem in sourceItemsForRecord)
            {
                if (!evidenceAnchorsBySourceItemId.TryGetValue(sourceItem.Id, out var anchors))
                {
                    continue;
                }

                foreach (var anchor in anchors)
                {
                    evidenceAnchorIdsForRecord.Add(anchor.Id);
                }
            }

            var evidenceAnchorsForRecord = evidenceAnchors
                .Where(anchor => evidenceAnchorIdsForRecord.Contains(anchor.Id))
                .ToArray();
            supportByRecordId[memoryRecordId] = new CognitiveMemoryRecordSupport(
                memoryRecordId,
                linksForRecord,
                sourceItemsForRecord,
                evidenceLinksForRecord,
                evidenceAnchorsForRecord,
                claims.Where(claim => claim.MemoryRecordId == memoryRecordId).ToArray());
        }

        return new CognitiveMemorySupportSnapshot(supportByRecordId, sourceItemsById);
    }
}

internal static partial class CognitiveMemoryQualityText
{
    private static readonly Regex EmailRegex = CreateEmailRegex();
    private static readonly Regex PhoneRegex = CreatePhoneRegex();
    private static readonly Regex SecretAssignmentRegex = CreateSecretAssignmentRegex();

    private static readonly IReadOnlySet<string> StopWords = new HashSet<string>([
        "about",
        "after",
        "and",
        "are",
        "for",
        "from",
        "has",
        "into",
        "must",
        "not",
        "the",
        "this",
        "use",
        "uses",
        "with"
    ], StringComparer.Ordinal);

    public static string NormalizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (builder.Length > 0 && builder[^1] != '.')
            {
                builder.Append('.');
            }
        }

        var normalized = builder.ToString().Trim('.');
        return string.IsNullOrWhiteSpace(normalized)
            ? "unknown"
            : normalized;
    }

    public static IReadOnlyList<string> ExtractMeaningfulTokens(string text, int maxTokens)
        => Regex.Split(text.ToLowerInvariant(), "[^\\p{L}\\p{Nd}]+")
            .Where(token => token.Length >= 4 && !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .Take(maxTokens)
            .ToArray();

    public static IReadOnlyList<string> ResolveTaskIntents(string text)
    {
        var normalized = text.ToLowerInvariant();
        var intents = new List<string>();
        AddIfAny("procedure", ["procedure", "runbook", "step", "checklist"]);
        AddIfAny("workflow", ["workflow", "process", "automation"]);
        AddIfAny("failure", ["failure", "error", "incident", "rollback", "bug"]);
        AddIfAny("decision", ["decision", "approved", "chosen", "tradeoff"]);
        AddIfAny("testing", ["test", "validation", "verify", "regression"]);
        AddIfAny("architecture", ["architecture", "design", "component", "module"]);
        AddIfAny("deployment", ["deploy", "release", "production", "docker"]);
        AddIfAny("coverage", ["coverage", "missing", "gap", "refresh"]);
        return intents.Count == 0 ? ["general"] : intents;

        void AddIfAny(string intent, IReadOnlyList<string> terms)
        {
            if (terms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
            {
                intents.Add(intent);
            }
        }
    }

    public static string Redact(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var builder = new StringBuilder(text.Length);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (EmailRegex.IsMatch(trimmed) || PhoneRegex.IsMatch(trimmed))
            {
                builder.AppendLine("[redacted-contact]");
                continue;
            }

            builder.AppendLine(SecretAssignmentRegex.Replace(trimmed, "$1[redacted-secret]"));
        }

        return builder.ToString().Trim();
    }

    public static string TrimText(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    public static bool PolicyCanRead(
        CognitiveMemoryAccessLevel accessLevel,
        CognitiveMemoryPolicyContext policyContext)
        => accessLevel <= policyContext.AccessLevel ||
           accessLevel == CognitiveMemoryAccessLevel.Restricted && policyContext.AllowRestrictedContent;

    [GeneratedRegex("[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreateEmailRegex();

    [GeneratedRegex("\\+?\\d[\\d\\s().-]{7,}\\d", RegexOptions.CultureInvariant)]
    private static partial Regex CreatePhoneRegex();

    [GeneratedRegex("(?i)\\b(secret|token|password|api[_-]?key)\\s*[:=]\\s*\\S+")]
    private static partial Regex CreateSecretAssignmentRegex();
}
