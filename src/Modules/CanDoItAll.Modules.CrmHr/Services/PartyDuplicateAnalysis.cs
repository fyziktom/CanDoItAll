using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

internal static class PartyDuplicateAnalysis {
    internal const int MaximumInputs = 250;
    private const int MaximumDuplicateCandidatesPerRow = 20;
    private const int MaximumPreviewDuplicateCandidates = MaximumInputs * MaximumDuplicateCandidatesPerRow;

    internal static async Task<Error?> ValidateNewImportsAsync(CrmHrDbContext context,
        IReadOnlyList<PartyEditorModel> models, CancellationToken cancellationToken) {
        var inputs = models.Where(model => model.Id is null).Select(PartyDuplicateLookupInput.FromParty).ToArray();
        if (inputs.Length == 0) {
            return null;
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var contacts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in inputs) {
            if (names.Overlaps(input.NormalizedNames) || contacts.Overlaps(input.NormalizedContactValues) ||
                (input.NormalizedExternalCode.Length > 0 && !codes.Add(input.NormalizedExternalCode))) {
                return ImportConflict();
            }
            names.UnionWith(input.NormalizedNames);
            contacts.UnionWith(input.NormalizedContactValues);
        }
        try {
            var duplicates = await FindPotentialDuplicatesForInputsAsync(context, inputs, null, cancellationToken);
            return duplicates.Any(rows => rows.Count > 0) ? ImportConflict() : null;
        } catch (DuplicateAnalysisLimitException exception) {
            return Error.Validation(exception.Message, CrmHrEditingErrorCodes.PartyImportConflict);
        }
    }

    private static Error ImportConflict() => Error.Validation(
        "Potential duplicates now exist in the directory or this batch. Preview the import again before applying.",
        CrmHrEditingErrorCodes.PartyImportConflict);

    internal static async Task<IReadOnlyList<IReadOnlyList<PartyDuplicateCandidateModel>>> FindPotentialDuplicatesForInputsAsync(
        CrmHrDbContext dbContext,
        IReadOnlyList<PartyDuplicateLookupInput> inputs,
        Guid? excludePartyId,
        CancellationToken cancellationToken) {
        if (inputs.Count == 0) {
            return [];
        }

        var normalizedNames = inputs
            .SelectMany(input => input.NormalizedNames)
            .ToHashSet(StringComparer.Ordinal);
        var normalizedExternalCodes = inputs
            .Select(input => input.NormalizedExternalCode)
            .Where(value => !string.IsNullOrEmpty(value))
            .ToHashSet(StringComparer.Ordinal);
        var normalizedContacts = inputs
            .SelectMany(input => input.NormalizedContactValues)
            .ToHashSet(StringComparer.Ordinal);
        if (normalizedNames.Count == 0 && normalizedExternalCodes.Count == 0 && normalizedContacts.Count == 0) {
            return inputs.Select(_ => (IReadOnlyList<PartyDuplicateCandidateModel>)[]).ToList();
        }

        var candidateParties = await dbContext.Set<Party>()
            .AsNoTracking()
            .Where(item => !excludePartyId.HasValue || item.Id != excludePartyId.Value)
            .Where(item =>
                normalizedNames.Contains(item.NormalizedDisplayName)
                || normalizedNames.Contains(item.NormalizedLegalName)
                || normalizedNames.Contains(item.NormalizedPreferredName)
                || normalizedExternalCodes.Contains(item.NormalizedExternalCode)
                || dbContext.Set<PartyContactPoint>().Any(contactPoint =>
                    contactPoint.PartyId == item.Id
                    && normalizedContacts.Contains(contactPoint.NormalizedValue)))
            .OrderBy(item => item.DisplayName)
            .ThenBy(item => item.Id)
            .Take(MaximumPreviewDuplicateCandidates + 1)
            .Select(item => new DuplicatePartyProjection(
                item.Id,
                item.DisplayName,
                item.PartyType,
                item.LifecycleStatus,
                item.IsSensitive ? string.Empty : item.Summary,
                item.NormalizedDisplayName,
                item.NormalizedLegalName,
                item.NormalizedPreferredName,
                item.NormalizedExternalCode))
            .ToListAsync(cancellationToken);
        if (candidateParties.Count > MaximumPreviewDuplicateCandidates) {
            throw new DuplicateAnalysisLimitException(
                $"Duplicate analysis matched more than {MaximumPreviewDuplicateCandidates} directory records. Narrow the import batch before previewing it.");
        }

        var candidatePartyIds = candidateParties.Select(item => item.Id).ToArray();
        var matchingContactsByPartyId = candidatePartyIds.Length == 0 || normalizedContacts.Count == 0
            ? new Dictionary<Guid, HashSet<string>>()
            : (await dbContext.Set<PartyContactPoint>()
                .AsNoTracking()
                .Where(item => candidatePartyIds.Contains(item.PartyId) && normalizedContacts.Contains(item.NormalizedValue))
                .Select(item => new { item.PartyId, item.NormalizedValue })
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.PartyId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.NormalizedValue).ToHashSet(StringComparer.Ordinal));

        return inputs
            .Select(input => BuildDuplicateCandidates(input, candidateParties, matchingContactsByPartyId))
            .ToList();
    }

    private sealed record DuplicatePartyProjection(
        Guid Id,
        string DisplayName,
        PartyType PartyType,
        PartyLifecycleStatus LifecycleStatus,
        string Summary,
        string NormalizedDisplayName,
        string NormalizedLegalName,
        string NormalizedPreferredName,
        string NormalizedExternalCode);

    private sealed record DuplicateCandidateScore(PartyDuplicateCandidateModel Candidate, int Score);

    internal sealed class DuplicateAnalysisLimitException(string message) : Exception(message);

    internal sealed class PartyDuplicateLookupInput {
        public PartyDuplicateLookupInput(
            string displayName,
            string legalName,
            string preferredName,
            string externalCode,
            IEnumerable<string> normalizedContactValues) {
            NormalizedNames = new[] {
                    NormalizeName(displayName),
                    NormalizeName(legalName),
                    NormalizeName(preferredName)
                }
                .Where(value => !string.IsNullOrEmpty(value))
                .ToHashSet(StringComparer.Ordinal);
            NormalizedExternalCode = NormalizeExternalCode(externalCode);
            NormalizedContactValues = normalizedContactValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.Ordinal);
        }

        public HashSet<string> NormalizedNames { get; }

        public string NormalizedExternalCode { get; }

        public HashSet<string> NormalizedContactValues { get; }

        public static PartyDuplicateLookupInput FromParty(PartyEditorModel party) {
            return new PartyDuplicateLookupInput(
                party.DisplayName,
                party.LegalName,
                party.PreferredName,
                party.ExternalCode,
                party.ContactPoints.Select(item => NormalizeContactValue(item.ContactType, item.Value)));
        }
    }

    private static IReadOnlyList<PartyDuplicateCandidateModel> BuildDuplicateCandidates(
        PartyDuplicateLookupInput input,
        IReadOnlyList<DuplicatePartyProjection> candidateParties,
        IReadOnlyDictionary<Guid, HashSet<string>> matchingContactsByPartyId) {
        return candidateParties
            .Select(party => {
                var reasons = new List<string>();
                var score = 0;
                if (!string.IsNullOrEmpty(input.NormalizedExternalCode)
                    && string.Equals(input.NormalizedExternalCode, party.NormalizedExternalCode, StringComparison.Ordinal)) {
                    reasons.Add("matching external code");
                    score += 4;
                }

                if (input.NormalizedNames.Contains(party.NormalizedDisplayName)
                    || input.NormalizedNames.Contains(party.NormalizedLegalName)
                    || input.NormalizedNames.Contains(party.NormalizedPreferredName)) {
                    reasons.Add("matching normalized name");
                    score += 2;
                }

                if (matchingContactsByPartyId.TryGetValue(party.Id, out var partyContacts)
                    && partyContacts.Overlaps(input.NormalizedContactValues)) {
                    reasons.Add("matching contact value");
                    score += 3;
                }

                return new DuplicateCandidateScore(
                    new PartyDuplicateCandidateModel(
                        party.Id,
                        party.DisplayName,
                        party.PartyType,
                        party.LifecycleStatus,
                        party.Summary,
                        reasons),
                    score);
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Candidate.Id)
            .Take(MaximumDuplicateCandidatesPerRow)
            .Select(item => item.Candidate)
            .ToList();
    }

    internal static string NormalizeName(string value) {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    internal static string NormalizeExternalCode(string value) {
        return value.Trim().ToLowerInvariant();
    }

    internal static string NormalizeContactValue(PartyContactType contactType, string value) {
        var trimmedValue = value.Trim();
        return contactType switch {
            PartyContactType.Email or PartyContactType.Website or PartyContactType.Messaging or PartyContactType.Social => trimmedValue.ToLowerInvariant(),
            PartyContactType.Phone => new string(trimmedValue.Where(character => char.IsDigit(character) || character == '+').ToArray()),
            _ => trimmedValue.ToLowerInvariant()
        };
    }

}
