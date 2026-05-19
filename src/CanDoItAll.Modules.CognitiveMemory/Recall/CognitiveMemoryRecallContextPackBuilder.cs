using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;


public sealed partial class CognitiveMemoryRecallOrchestrator
{
    private async Task<CognitiveMemoryRecallContextPack> BuildContextPackAsync(
        AppDbContext dbContext,
        Guid traceId,
        CognitiveMemoryRecallRequest request,
        CognitiveMemoryWorkspaceFrameId? workspaceFrameId,
        IReadOnlyList<EvaluatedRecallCandidate> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        List<string> warnings,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var selected = candidates
            .Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected)
            .Take(request.Budget.DetailItemLimit)
            .ToList();
        var limitedByDetail = candidates.Count(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected) > selected.Count;
        var sourceRefs = await LoadSourceRefsAsync(dbContext, request, selected, cancellationToken);
        var sourceBudget = request.Budget.MaxSourceBytes;
        var remainingCharacters = request.Budget.ContextCharacterBudget;
        var sections = new List<CognitiveMemoryRecallContextSection>();
        var sequence = 0;

        foreach (var candidate in selected)
        {
            var candidateRefs = sourceRefs
                .Where(sourceRef => sourceRef.MemoryRecordId.Value == candidate.Record.Id)
                .ToArray();
            var content = BuildSectionContent(candidate, candidateRefs, request.PolicyContext, ref sourceBudget, warnings);
            if (content.Length > remainingCharacters)
            {
                var trimmed = content[..Math.Max(0, remainingCharacters)];
                warnings.Add($"Context character budget truncated section for '{candidate.Record.Title}'.");
                content = trimmed;
            }

            if (content.Length == 0)
            {
                warnings.Add($"Context character budget excluded section for '{candidate.Record.Title}'.");
                continue;
            }

            remainingCharacters -= content.Length;
            sections.Add(new CognitiveMemoryRecallContextSection(
                new CognitiveMemorySectionId($"selected-{sequence}"),
                CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                candidate.Record.Title,
                content,
                [new CognitiveMemoryRecordId(candidate.Record.Id)],
                candidate.SelectedClaimIds,
                candidateRefs));
            sequence++;

            if (remainingCharacters <= 0)
            {
                break;
            }
        }

        foreach (var inhibited in candidates.Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Inhibited))
        {
            if (remainingCharacters <= 0)
            {
                break;
            }

            var warning = $"Do not confuse with {inhibited.Record.Title}: {inhibited.Reason}";
            var content = warning.Length <= remainingCharacters
                ? warning
                : warning[..remainingCharacters];
            sections.Add(new CognitiveMemoryRecallContextSection(
                new CognitiveMemorySectionId($"inhibited-{sequence}"),
                CognitiveMemoryRecallContextSectionKind.DoNotConfuseWith,
                inhibited.Record.Title,
                content,
                [new CognitiveMemoryRecordId(inhibited.Record.Id)],
                inhibited.SelectedClaimIds,
                []));
            remainingCharacters -= content.Length;
            sequence++;
        }

        if (limitedByDetail)
        {
            warnings.Add("Recall detail item budget excluded one or more focused candidates from detailed source loading.");
        }

        var pack = new CognitiveMemoryRecallContextPack(
            CognitiveMemoryRecallContextPackId.New(),
            request.ProjectId,
            workspaceFrameId,
            $"Recall context for {request.Intent}",
            BuildPackSummary(selected, candidates),
            sections,
            sourceRefs,
            warnings.ToArray(),
            request.Metadata ?? EmptyMetadata);

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.DetailRetrieval,
            CognitiveMemoryRecallChannelKind.SourceDetail,
            CognitiveMemoryRecallStageStatus.Completed,
            selected.Count,
            sourceRefs.Count(sourceRef => sourceRef.IncludedInContext),
            sourceRefs.Count(sourceRef => !sourceRef.IncludedInContext),
            $"source-detail:refs:{sourceRefs.Count}",
            limitingBudget: limitedByDetail ? CognitiveMemoryBudgetLimit.DetailCount : null,
            completedAtUtc: nowUtc));
        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.ContextPackRendering,
            CognitiveMemoryRecallChannelKind.ContextPack,
            CognitiveMemoryRecallStageStatus.Completed,
            sections.Count,
            sections.Count,
            0,
            $"context-pack:chars:{request.Budget.ContextCharacterBudget - remainingCharacters}/{request.Budget.ContextCharacterBudget}",
            limitingBudget: remainingCharacters <= 0 ? CognitiveMemoryBudgetLimit.ByteCount : null,
            completedAtUtc: nowUtc));

        return pack;
    }

    private static string BuildSectionContent(
        EvaluatedRecallCandidate candidate,
        IReadOnlyList<CognitiveMemoryRecallSourceRef> sourceRefs,
        CognitiveMemoryPolicyContext policyContext,
        ref int remainingSourceBytes,
        List<string> warnings)
    {
        var builder = new StringBuilder();
        var appendedBlocks = new HashSet<string>(StringComparer.Ordinal);
        _ = AppendDistinctBlock(builder, candidate.Record.SummaryText, prefix: null, appendedBlocks);

        var canonical = candidate.Record.CanonicalText.Trim();
        if (canonical.Length > 0)
        {
            var bytes = Encoding.UTF8.GetByteCount(canonical);
            if (bytes <= remainingSourceBytes && PolicyCanRead(candidate.Record.AccessLevel, policyContext))
            {
                if (AppendDistinctBlock(builder, canonical, prefix: null, appendedBlocks))
                {
                    remainingSourceBytes -= bytes;
                }
            }
            else
            {
                warnings.Add($"Source byte or access budget prevented full canonical detail for '{candidate.Record.Title}'.");
            }
        }

        foreach (var sourceRef in sourceRefs.Where(sourceRef => sourceRef.IncludedInContext))
        {
            var sourceSummary = sourceRef.Summary.Trim();
            if (sourceSummary.Length == 0)
            {
                continue;
            }

            var bytes = Encoding.UTF8.GetByteCount(sourceSummary);
            if (bytes <= remainingSourceBytes)
            {
                if (AppendDistinctBlock(builder, sourceSummary, "Source detail: ", appendedBlocks))
                {
                    remainingSourceBytes -= bytes;
                }

                continue;
            }

            if (remainingSourceBytes > 0)
            {
                var snippet = sourceSummary[..Math.Min(sourceSummary.Length, remainingSourceBytes)];
                if (AppendDistinctBlock(builder, snippet, "Source detail: ", appendedBlocks))
                {
                    warnings.Add($"Source byte budget truncated source detail for '{candidate.Record.Title}'.");
                    remainingSourceBytes = 0;
                }
            }
            else
            {
                warnings.Add($"Source byte budget excluded source detail for '{candidate.Record.Title}'.");
            }
        }

        var unavailableReasons = new HashSet<CognitiveMemoryRecallExclusionReasonKind>();
        foreach (var sourceRef in sourceRefs.Where(sourceRef => !sourceRef.IncludedInContext))
        {
            if (!unavailableReasons.Add(sourceRef.ExclusionReasonKind))
            {
                continue;
            }

            builder.AppendLine($"Source unavailable: {sourceRef.ExclusionReasonKind}.");
        }

        return builder.ToString().Trim();
    }

    private static bool AppendDistinctBlock(
        StringBuilder builder,
        string? text,
        string? prefix,
        HashSet<string> appendedBlocks)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var redacted = RedactRecallContextText(text);
        var normalized = NormalizeContextBlock(redacted);
        if (normalized.Length == 0 || IsRepeatedContextBlock(normalized, appendedBlocks))
        {
            return false;
        }

        appendedBlocks.Add(normalized);
        builder.AppendLine(string.IsNullOrEmpty(prefix) ? redacted : $"{prefix}{redacted}");
        return true;
    }

    private static string RedactRecallContextText(string text)
    {
        var lines = text.Trim().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var builder = new StringBuilder(text.Length);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (RecallEmailRegex.IsMatch(trimmed) || RecallInternationalPhoneRegex.IsMatch(trimmed))
            {
                builder.AppendLine("[redacted-contact]");
                continue;
            }

            builder.AppendLine(trimmed);
        }

        return builder.ToString().Trim();
    }

    private static bool IsRepeatedContextBlock(
        string normalized,
        HashSet<string> appendedBlocks)
    {
        foreach (var appendedBlock in appendedBlocks)
        {
            if (appendedBlock.Equals(normalized, StringComparison.Ordinal))
            {
                return true;
            }

            if (Math.Min(appendedBlock.Length, normalized.Length) >= OverlapDeduplicationMinimumCharacters &&
                (appendedBlock.Contains(normalized, StringComparison.Ordinal) ||
                 normalized.Contains(appendedBlock, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeContextBlock(string text)
        => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private async Task<IReadOnlyList<CognitiveMemoryRecallSourceRef>> LoadSourceRefsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<EvaluatedRecallCandidate> selected,
        CancellationToken cancellationToken)
    {
        var memoryRecordIds = selected.Select(candidate => candidate.Record.Id).Distinct().ToArray();
        if (memoryRecordIds.Length == 0)
        {
            return [];
        }

        var sourceLinks = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => memoryRecordIds.Contains(link.MemoryRecordId))
            .Select(link => new SourceLinkSnapshot(
                link.MemoryRecordId,
                link.SourceItemId,
                link.Locator,
                link.QuoteHash,
                link.Summary))
            .ToListAsync(cancellationToken);
        var sourceItemIds = sourceLinks.Select(link => link.SourceItemId).Distinct().ToArray();
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => sourceItemIds.Contains(item.Id))
            .Select(item => new SourceItemSnapshot(
                item.Id,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemKey,
                item.Title,
                item.ContentText,
                item.Locator,
                item.RedactionState,
                item.AccessLevel))
            .ToListAsync(cancellationToken);
        var sourceItemsById = sourceItems.ToDictionary(item => item.Id);
        var evidenceLinks = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(link => memoryRecordIds.Contains(link.MemoryRecordId))
            .Select(link => new
            {
                link.MemoryRecordId,
                link.EvidenceAnchorId,
                link.Summary
            })
            .ToListAsync(cancellationToken);
        var evidenceAnchorIds = evidenceLinks.Select(link => link.EvidenceAnchorId).Distinct().ToArray();
        var anchors = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(anchor => evidenceAnchorIds.Contains(anchor.Id))
            .Select(anchor => new EvidenceAnchorSnapshot(
                anchor.Id,
                anchor.SourceItemId,
                anchor.SourceSystem,
                anchor.Locator,
                anchor.QuoteHash,
                anchor.RedactionState))
            .ToListAsync(cancellationToken);
        var anchorsById = anchors.ToDictionary(anchor => anchor.Id);
        var sourceRefs = new List<CognitiveMemoryRecallSourceRef>();

        foreach (var link in sourceLinks)
        {
            sourceItemsById.TryGetValue(link.SourceItemId, out var item);
            var accessLevel = item?.AccessLevel ?? CognitiveMemoryAccessLevel.Project;
            var redactionState = item?.RedactionState ?? CognitiveMemoryRedactionState.Unclassified;
            var included = CanIncludeSourceRef(accessLevel, redactionState, request.PolicyContext);
            sourceRefs.Add(new CognitiveMemoryRecallSourceRef(
                new CognitiveMemoryRecordId(link.MemoryRecordId),
                new CognitiveMemorySourceItemId(link.SourceItemId),
                null,
                item?.SourceSystem ?? string.Empty,
                item?.Locator ?? link.Locator ?? string.Empty,
                BuildSourceRefSummary(link.Summary, item),
                accessLevel,
                redactionState,
                included,
                included ? CognitiveMemoryRecallExclusionReasonKind.None : ResolveSourceRefExclusion(accessLevel, redactionState, request.PolicyContext)));
        }

        foreach (var evidenceLink in evidenceLinks)
        {
            if (!anchorsById.TryGetValue(evidenceLink.EvidenceAnchorId, out var anchor))
            {
                continue;
            }

            var included = CanIncludeSourceRef(CognitiveMemoryAccessLevel.Project, anchor.RedactionState, request.PolicyContext);
            sourceRefs.Add(new CognitiveMemoryRecallSourceRef(
                new CognitiveMemoryRecordId(evidenceLink.MemoryRecordId),
                anchor.SourceItemId is null ? null : new CognitiveMemorySourceItemId(anchor.SourceItemId.Value),
                new CognitiveMemoryEvidenceAnchorId(anchor.Id),
                anchor.SourceSystem,
                anchor.Locator,
                RedactRecallContextText(evidenceLink.Summary),
                CognitiveMemoryAccessLevel.Project,
                anchor.RedactionState,
                included,
                included ? CognitiveMemoryRecallExclusionReasonKind.None : ResolveSourceRefExclusion(CognitiveMemoryAccessLevel.Project, anchor.RedactionState, request.PolicyContext)));
        }

        return sourceRefs;
    }
}