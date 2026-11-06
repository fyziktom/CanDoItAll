using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Modules.CrmHr;

public sealed class PartyRelationshipService(IDbContextFactory<CrmHrDbContext> contexts, IClock clock, CrmHrChanges changes) : IPartyRelationshipService {
    internal static string ScopeKey(Guid partyId) => $"crmhr:relationships:{partyId:D}";

    public async Task<IReadOnlyList<PartyRelationshipListItemModel>> ListAsync(Guid partyId, CancellationToken cancellationToken = default, int? maximumRows = null) {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        return await ReadAsync(context, partyId, cancellationToken, maximumRows: maximumRows);
    }

    internal static async Task<IReadOnlyList<PartyRelationshipListItemModel>> ReadAsync(CrmHrDbContext context,
        Guid partyId, CancellationToken cancellationToken, bool supportOnly = false, int? maximumRows = null) {
        if (maximumRows is < 1 or int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(maximumRows));
        }
        var query =
            from row in Query(context, partyId, supportOnly).AsNoTracking()
            where row.SourcePartyId == partyId || row.TargetPartyId == partyId
            let relatedId = row.SourcePartyId == partyId ? row.TargetPartyId : row.SourcePartyId
            join candidate in context.Set<Party>().AsNoTracking() on relatedId equals candidate.Id into candidates
            from related in candidates.DefaultIfEmpty()
            select new {
                Present = related != null,
                Item = new PartyRelationshipListItemModel(row.Id, relatedId,
                    related == null ? string.Empty : related.DisplayName, related == null ? default : related.PartyType,
                    row.RelationshipKind, row.SourcePartyId == partyId, row.IsPrimary, row.StartDateUtc, row.EndDateUtc, row.Notes) { Version = row.Version }
            };
        if (maximumRows.HasValue) {
            query = query.Take(maximumRows.Value + 1);
        }
        var rows = await query.ToListAsync(cancellationToken);
        if (maximumRows.HasValue && rows.Count > maximumRows.Value) {
            throw new RelationshipReadLimitExceededException(maximumRows.Value);
        }
        if (rows.FirstOrDefault(row => !row.Present) is { } missing) {
            throw new InvalidOperationException($"Party relationship '{missing.Item.Id}' references missing party '{missing.Item.RelatedPartyId}'.");
        }
        return rows.Select(row => row.Item).OrderBy(row => row.RelatedPartyDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.RelationshipKind).ThenBy(row => row.RelatedPartyId).ThenBy(row => row.Id).ToArray();
    }

    public Task<Result<PartyRelationshipSaveReceipt>> ReplaceAsync(Guid partyId,
        IReadOnlyList<PartyRelationshipEditorModel> relationships, PartyRelationshipObservation? original, string actor,
        CancellationToken cancellationToken = default, Action<PartyRelationshipSaveReceipt>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(relationships);
        var submitted = relationships.Select(row => row.Snapshot()).ToArray();
        return SaveAsync(partyId, original, actor, submitted.Select(row => row.RelatedPartyId).ToArray(),
            _ => submitted, false, cancellationToken, onCommitted);
    }

    public Task<Result<PartyRelationshipSaveReceipt>> ReplaceSupportAsync(RecruitmentSupportAssignmentsEditorModel model,
        CancellationToken cancellationToken = default, Action<PartyRelationshipSaveReceipt>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        model = model.Snapshot();
        if (model.PartyId == Guid.Empty) {
            return Task.FromResult(Failure(Error.Validation("Choose a candidate first.", "crmhr.recruiting.support.party-required")));
        }
        if (new[] { model.ManagerPartyId, model.BuddyPartyId, model.MentorPartyId }.Any(id => id == model.PartyId)) {
            return Task.FromResult(Failure(Error.Validation("Candidate support assignments cannot reference the same party.", "crmhr.recruiting.support.self-reference")));
        }
        var endpoints = new[] { model.ManagerPartyId, model.BuddyPartyId, model.MentorPartyId }.OfType<Guid>().ToArray();
        return SaveAsync(model.PartyId, model.Original, model.LastChangedBy, endpoints,
            current => RecruitmentSupportRelationshipPolicy.Desired(model, current), true, cancellationToken, onCommitted);
    }

    private async Task<Result<PartyRelationshipSaveReceipt>> SaveAsync(Guid partyId, PartyRelationshipObservation? original,
        string actor, IReadOnlyList<Guid> newEndpoints,
        Func<IReadOnlyList<PartyRelationship>, IReadOnlyList<PartyRelationshipEditorModel>> desired,
        bool supportOnly, CancellationToken cancellationToken, Action<PartyRelationshipSaveReceipt>? onCommitted) {
        if (original?.Rows is null) {
            return Failure(Error.Validation("The complete original relationship observation is required, including an explicitly observed empty set.",
                CrmHrEditingErrorCodes.RelationshipOriginalRequired));
        }
        original = original.Snapshot();
        if (partyId == Guid.Empty || newEndpoints.Any(id => id == Guid.Empty || id == partyId)) {
            return Failure(Error.Validation("Relationships must target another saved party.", "crmhr.party.relationship-invalid"));
        }
        if (original.Rows.Any(row => row.Id == Guid.Empty || row.RelatedPartyId == Guid.Empty || row.RelatedPartyId == partyId || row.Version == 0) ||
            original.Rows.Select(row => row.Id).Distinct().Count() != original.Rows.Count) {
            return Failure(Error.Validation("Original relationship identities, endpoints and row versions must be nonempty and distinct.", CrmHrEditingErrorCodes.RelationshipIdentityInvalid));
        }
        var observed = original.Rows.ToDictionary(row => row.Id);
        var affected = original.Rows.Select(row => row.RelatedPartyId).Concat(newEndpoints).Append(partyId).Distinct().ToArray();
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(context, affected.Select(ScopeKey).ToArray(), cancellationToken);
        PartyRelationshipSaveReceipt receipt;
        var changed = false;
        try {
            var existing = await Query(context, partyId, supportOnly).ToListAsync(cancellationToken);
            if (!Matches(partyId, original, existing)) {
                return Failure(Conflict());
            }
            var submitted = desired(existing);
            foreach (var row in submitted) {
                row.Notes = row.Notes.Trim();
            }
            if (submitted.Any(row => row.RelatedPartyId == Guid.Empty || row.RelatedPartyId == partyId || !Enum.IsDefined(row.RelationshipKind))) {
                return Failure(Error.Validation("Relationships must target another saved party and have a supported kind.", "crmhr.party.relationship-invalid"));
            }
            if (submitted.Any(row => row.Id == Guid.Empty) || submitted.Where(row => row.Id.HasValue).Select(row => row.Id).Distinct().Count() != submitted.Count(row => row.Id.HasValue)) {
                return Failure(Error.Validation("Submitted relationship identities must be nonempty and distinct.", CrmHrEditingErrorCodes.RelationshipIdentityInvalid));
            }
            if (submitted.GroupBy(row => (row.RelatedPartyId, row.IsOutgoing, row.RelationshipKind,
                Role: RecruitmentSupportRelationshipPolicy.Role(row.RelationshipKind, row.Notes))).Any(group => group.Skip(1).Any())) {
                return Failure(Error.Validation("Duplicate relationships with the same source, target, kind and support role are not allowed.", "crmhr.party.relationship-duplicate"));
            }
            var required = submitted.Select(row => row.RelatedPartyId).Append(partyId).Distinct().ToArray();
            var present = await context.Set<Party>().AsNoTracking().Where(row => required.Contains(row.Id)).Select(row => new { row.Id, row.PartyType }).ToDictionaryAsync(row => row.Id, cancellationToken);
            if (!present.ContainsKey(partyId)) {
                return Failure(Error.Failure("The selected party was not found.", supportOnly ? "crmhr.recruiting.support.party-not-found" : "crmhr.party.not-found"));
            }
            if (supportOnly) {
                foreach (var row in submitted) {
                    if (!present.TryGetValue(row.RelatedPartyId, out var person) || person.PartyType != PartyType.Person) {
                        return Failure(InvalidSupportPerson(RecruitmentSupportRelationshipPolicy.Role(row.RelationshipKind, row.Notes)));
                    }
                }
            } else if (required.Any(id => !present.ContainsKey(id))) {
                return Failure(Error.Validation("One or more related parties no longer exist.", "crmhr.party.relationship-party-not-found"));
            }
            foreach (var row in submitted) {
                var personId = row.RelationshipKind switch {
                    PartyRelationshipKind.Supports when row.Notes is RecruitmentSupportRelationshipPolicy.Buddy or RecruitmentSupportRelationshipPolicy.Mentor => row.IsOutgoing ? partyId : row.RelatedPartyId,
                    PartyRelationshipKind.ManagedBy when row.Notes == RecruitmentSupportRelationshipPolicy.Manager => row.IsOutgoing ? row.RelatedPartyId : partyId,
                    _ => (Guid?)null
                };
                if (personId.HasValue && present[personId.Value].PartyType != PartyType.Person) {
                    return Failure(InvalidSupportPerson(RecruitmentSupportRelationshipPolicy.Role(row.RelationshipKind, row.Notes)));
                }
            }
            foreach (var row in submitted) {
                row.Id ??= Guid.NewGuid();
            }
            var byId = existing.ToDictionary(row => row.Id);
            var retained = submitted.Select(row => row.Id!.Value).ToHashSet();
            context.Set<PartyRelationship>().RemoveRange(existing.Where(row => !retained.Contains(row.Id)));
            foreach (var row in submitted) {
                if (!byId.TryGetValue(row.Id!.Value, out var entity)) {
                    entity = new() { Id = row.Id.Value };
                    context.Set<PartyRelationship>().Add(entity);
                } else {
                    context.Entry(entity).Property(item => item.Version).OriginalValue = observed[entity.Id].Version;
                }
                entity.SourcePartyId = row.IsOutgoing ? partyId : row.RelatedPartyId;
                entity.TargetPartyId = row.IsOutgoing ? row.RelatedPartyId : partyId;
                entity.RelationshipKind = row.RelationshipKind;
                entity.IsPrimary = row.IsPrimary;
                entity.StartDateUtc = row.StartDateUtc is { } start ? CrmHrEditVersions.Normalize(start) : null;
                entity.EndDateUtc = row.EndDateUtc is { } end ? CrmHrEditVersions.Normalize(end) : null;
                entity.Notes = row.Notes.Trim();
            }
            changed = context.ChangeTracker.Entries<PartyRelationship>().Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
            CrmHrAuditWriter.AddEntry(context, nameof(Party), partyId, "RelationshipsSaved",
                $"Saved {submitted.Count} relationship(s).", new { RelationshipCount = submitted.Count },
                string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim(), false, clock.GetUtcNow());
            await changes.DemandCommitAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            receipt = new(partyId, await ReadAsync(context, partyId, cancellationToken, supportOnly));
            await changes.DemandCommitAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        } catch (Exception exception) when (exception is DbUpdateConcurrencyException || SerializableMutationScope.IsConflict(exception) ||
            exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation } }) {
            return Failure(Conflict());
        }
        onCommitted?.Invoke(receipt);
        if (changed) {
            changes.PublishRelationships(affected);
        }
        return Result<PartyRelationshipSaveReceipt>.Success(receipt);
    }

    private static IQueryable<PartyRelationship> Query(CrmHrDbContext context, Guid partyId, bool supportOnly) => supportOnly
        ? context.Set<PartyRelationship>().Where(RecruitmentSupportRelationshipPolicy.Owned(partyId))
        : context.Set<PartyRelationship>().Where(row => row.SourcePartyId == partyId || row.TargetPartyId == partyId);

    internal static Task<bool> WouldInvalidateSupportPersonAsync(CrmHrDbContext context, Guid partyId, PartyType type, CancellationToken cancellationToken) =>
        type == PartyType.Person ? Task.FromResult(false) : context.Set<PartyRelationship>().AsNoTracking().AnyAsync(row =>
            row.SourcePartyId == partyId && row.RelationshipKind == PartyRelationshipKind.Supports &&
                (row.Notes == RecruitmentSupportRelationshipPolicy.Buddy || row.Notes == RecruitmentSupportRelationshipPolicy.Mentor) ||
            row.TargetPartyId == partyId && row.RelationshipKind == PartyRelationshipKind.ManagedBy && row.Notes == RecruitmentSupportRelationshipPolicy.Manager, cancellationToken);

    private static Error InvalidSupportPerson(RecruitmentSupportRole role) => role switch {
        RecruitmentSupportRole.Manager => Error.Validation("Manager must reference an existing person.", "crmhr.recruiting.support.manager-invalid"),
        RecruitmentSupportRole.Buddy => Error.Validation("Buddy must reference an existing person.", "crmhr.recruiting.support.buddy-invalid"),
        RecruitmentSupportRole.Mentor => Error.Validation("Mentor must reference an existing person.", "crmhr.recruiting.support.mentor-invalid"),
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    internal static bool Matches(Guid partyId, PartyRelationshipObservation original, IReadOnlyCollection<PartyRelationship> current) {
        var expected = original.Rows.ToDictionary(row => row.Id);
        return current.Count == expected.Count && current.All(row => expected.TryGetValue(row.Id, out var before) &&
            before.Version == row.Version && before.RelatedPartyId == (row.SourcePartyId == partyId ? row.TargetPartyId : row.SourcePartyId));
    }

    internal static Error Conflict() => Error.Failure("The saved relationship set or one of its rows changed. Read and explicitly reconcile its current values.", CrmHrEditingErrorCodes.RelationshipConflict);
    private static Result<PartyRelationshipSaveReceipt> Failure(Error error) => Result<PartyRelationshipSaveReceipt>.Failure(error);
}
