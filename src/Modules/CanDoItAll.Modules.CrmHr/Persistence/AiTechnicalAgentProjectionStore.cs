using System.Collections.Immutable;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class AiTechnicalAgentProjectionStore(
    IDbContextFactory<CrmHrDbContext> contexts,
    ICanonicalRuntimeDatabase database,
    IClock clock) : IAiTechnicalAgentProjectionStore {
    private const string ProjectionActor = "agent-framework-sync";
    private const string ProjectionReason = "Projected from AgentFramework organization catalog.";

    public async Task<int> CountBoundAsync(CancellationToken cancellationToken = default) {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        return await context.Set<AiResourceBinding>().CountAsync(binding =>
            binding.TechnicalAgentId.HasValue && binding.BindingStatus == AiResourceBindingStatus.Bound, cancellationToken);
    }

    public async Task<AiTechnicalCatalogRepairFacts> ReadCatalogRepairFactsAsync(CancellationToken cancellationToken = default) {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await (from party in context.Set<Party>().AsNoTracking()
                          where party.PartyType == PartyType.AiAgent
                          join binding in context.Set<AiResourceBinding>().AsNoTracking() on party.Id equals binding.PartyId into bindings
                          from binding in bindings.DefaultIfEmpty()
                          select new { party.Id, TechnicalId = binding == null ? null : binding.TechnicalAgentId,
                              Availability = binding == null ? AiTechnicalProjectionAvailability.Unknown : binding.ProjectionAvailability })
            .ToListAsync(cancellationToken);
        var repairable = rows.Where(row => row.Availability is AiTechnicalProjectionAvailability.Unknown or AiTechnicalProjectionAvailability.Present).ToArray();
        return new(repairable.Select(row => row.Id).ToImmutableArray(), repairable.Where(row => row.TechnicalId.HasValue)
            .Select(row => new AiTechnicalBindingReference(row.Id, row.TechnicalId!.Value)).ToImmutableArray());
    }

    public async Task<AiTechnicalProjectionApplyResult> ApplyAsync(AiTechnicalCatalogProjection projection,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(projection.Source);
        if (projection.Source.DatabaseProfileId != database.Profile.Profile.Id) {
            throw new InvalidOperationException("The technical projection belongs to a different database profile.");
        }

        if (!projection.Revision.IsAssigned || projection.Agents.IsDefault) {
            throw new ArgumentException("A complete catalog projection and assigned revision are required.", nameof(projection));
        }

        var agents = Normalize(projection.Agents);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { SchemaVersion = 1, Agents = agents }))));
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        if (!context.Database.IsNpgsql() && !context.Database.IsInMemory()) {
            throw new NotSupportedException("Technical projection transactions require PostgreSQL; InMemory is supported only for functional test fixtures.");
        }

        await using var transaction = context.Database.IsNpgsql()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        var timestamp = clock.GetUtcNow();
        var cursor = await LockCursorAsync(context, projection.Source, timestamp, cancellationToken);
        if (projection.Revision.Value < cursor.CatalogRevision) {
            return new(AiTechnicalProjectionApplyDisposition.Stale, new(cursor.CatalogRevision));
        }

        if (projection.Revision.Value == cursor.CatalogRevision) {
            if (!string.Equals(hash, cursor.ProjectionSha256, StringComparison.Ordinal)) {
                throw new InvalidOperationException("The same source catalog revision has conflicting technical projection content.");
            }

            return new(AiTechnicalProjectionApplyDisposition.Replayed, projection.Revision);
        }

        await ReconcileAsync(context, projection.Source, projection.Revision, agents, timestamp, cancellationToken);
        cursor.CatalogRevision = projection.Revision.Value;
        cursor.ProjectionSha256 = hash;
        cursor.UpdatedAtUtc = timestamp;
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken);
        }

        return new(AiTechnicalProjectionApplyDisposition.Applied, projection.Revision);
    }

    public async Task<IReadOnlyDictionary<Guid, AiTechnicalProjectionResource>> ReadAsync(IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(partyIds);
        var ids = partyIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) {
            return new Dictionary<Guid, AiTechnicalProjectionResource>();
        }

        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        var rows = await (from party in context.Set<Party>().AsNoTracking()
                          where ids.Contains(party.Id)
                          join binding in context.Set<AiResourceBinding>().AsNoTracking() on party.Id equals binding.PartyId into bindings
                          from binding in bindings.DefaultIfEmpty()
                          select new { party.Id, party.PartyType, party.DisplayName, party.Summary, Binding = binding })
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        return ids.ToDictionary(id => id, id => {
            var row = rows.GetValueOrDefault(id);
            return ReadResource(id, row?.PartyType, row?.DisplayName ?? string.Empty, row?.Summary ?? string.Empty, row?.Binding);
        });
    }

    private static async Task<AiTechnicalProjectionCursor> LockCursorAsync(CrmHrDbContext context,
        AiTechnicalProjectionSource source, DateTimeOffset timestamp, CancellationToken cancellationToken) {
        if (context.Database.IsNpgsql()) {
            var kind = source.Scope.Kind.ToString();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "CrmHr_AiTechnicalProjectionCursors"
                    ("DatabaseProfileId", "SourceScopeKind", "SourceScopeKey", "CatalogRevision", "ProjectionSha256", "UpdatedAtUtc")
                VALUES ({source.DatabaseProfileId}, {kind}, {source.Scope.Key}, 0, '', {timestamp})
                ON CONFLICT ("DatabaseProfileId", "SourceScopeKind", "SourceScopeKey") DO NOTHING
                """, cancellationToken);
            return await context.Set<AiTechnicalProjectionCursor>().FromSqlInterpolated($"""
                SELECT * FROM "CrmHr_AiTechnicalProjectionCursors"
                WHERE "DatabaseProfileId" = {source.DatabaseProfileId}
                    AND "SourceScopeKind" = {kind} AND "SourceScopeKey" = {source.Scope.Key}
                FOR UPDATE
                """).SingleAsync(cancellationToken);
        }

        var cursor = await context.Set<AiTechnicalProjectionCursor>().SingleOrDefaultAsync(item =>
            item.DatabaseProfileId == source.DatabaseProfileId && item.SourceScopeKind == source.Scope.Kind &&
            item.SourceScopeKey == source.Scope.Key, cancellationToken);
        if (cursor is not null) {
            return cursor;
        }

        cursor = new() { DatabaseProfileId = source.DatabaseProfileId, SourceScopeKind = source.Scope.Kind, SourceScopeKey = source.Scope.Key };
        context.Add(cursor);
        return cursor;
    }

    private static async Task ReconcileAsync(CrmHrDbContext context, AiTechnicalProjectionSource source,
        CatalogDataRevision revision, ImmutableArray<AiTechnicalProjectionEntry> agents, DateTimeOffset timestamp,
        CancellationToken cancellationToken) {
        var bindings = await context.Set<AiResourceBinding>().ToListAsync(cancellationToken);
        var candidatePartyIds = agents.Where(agent => agent.PreferredPartyId.HasValue).Select(agent => agent.PreferredPartyId!.Value)
            .Concat(bindings.Select(binding => binding.PartyId)).Distinct().ToArray();
        var parties = await context.Set<Party>().Where(party => party.PartyType == PartyType.AiAgent || candidatePartyIds.Contains(party.Id))
            .ToDictionaryAsync(party => party.Id, cancellationToken);
        var byParty = bindings.ToDictionary(binding => binding.PartyId);
        var eligible = bindings.Where(binding => BelongsToSource(binding, source)).ToArray();
        var byAgent = eligible.Where(binding => binding.TechnicalAgentId.HasValue)
            .GroupBy(binding => binding.TechnicalAgentId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(binding => binding.BindingStatus == AiResourceBindingStatus.Bound)
                .ThenByDescending(binding => binding.UpdatedAtUtc).ThenBy(binding => binding.Id).ToArray());
        var currentIds = agents.Select(agent => agent.TechnicalAgentId).ToHashSet();
        foreach (var agent in agents) {
            var previous = byAgent.GetValueOrDefault(agent.TechnicalAgentId) ?? [];
            var binding = previous.FirstOrDefault(item => item.ProjectionAvailability != AiTechnicalProjectionAvailability.Superseded);
            if (binding is null && previous.Length > 0) {
                throw new InvalidOperationException($"Technical Agent '{agent.TechnicalAgentId:D}' has only superseded CRM bindings. Reconcile the original Party binding before applying the catalog.");
            }
            if (binding is null && agent.PreferredPartyId is Guid preferredId && byParty.TryGetValue(preferredId, out var preferredBinding) &&
                BelongsToSource(preferredBinding, source) &&
                (preferredBinding.TechnicalAgentId is null || preferredBinding.TechnicalAgentId == agent.TechnicalAgentId ||
                    preferredBinding.BindingStatus == AiResourceBindingStatus.PendingBackfill && preferredBinding.SourceDatabaseProfileId is null)) {
                binding = preferredBinding;
            }

            var partyId = binding?.PartyId;
            if (partyId is null && agent.PreferredPartyId is Guid preferredPartyId && !byParty.ContainsKey(preferredPartyId) &&
                (!parties.TryGetValue(preferredPartyId, out var preferredParty) || preferredParty.PartyType == PartyType.AiAgent)) {
                partyId = preferredPartyId;
            }

            if (partyId is Guid existingPartyId && parties.TryGetValue(existingPartyId, out var existingParty) && existingParty.PartyType != PartyType.AiAgent) {
                throw new InvalidOperationException($"Technical Agent '{agent.TechnicalAgentId:D}' has a binding to a non-Agent CRM Party.");
            }

            partyId ??= Guid.NewGuid();
            if (!parties.ContainsKey(partyId.Value)) {
                var party = new Party {
                    Id = partyId.Value, PartyType = PartyType.AiAgent, LifecycleStatus = InitialLifecycle(agent.LifecycleStatus),
                    DisplayName = agent.DisplayName, Summary = agent.Summary, LastChangedBy = ProjectionActor,
                    CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
                };
                context.Add(party);
                parties.Add(party.Id, party);
            }

            if (binding is null) {
                binding = new() { PartyId = partyId.Value, CreatedAtUtc = timestamp };
                context.Add(binding);
                byParty.Add(binding.PartyId, binding);
            }

            binding.TechnicalAgentId = agent.TechnicalAgentId;
            binding.BindingStatus = AiResourceBindingStatus.Bound;
            binding.BindingReason = ProjectionReason;
            binding.LastError = string.Empty;
            binding.ProjectedDisplayName = agent.DisplayName;
            binding.ProjectedSummary = agent.Summary;
            binding.ProjectedLifecycleStatus = agent.LifecycleStatus;
            binding.ProjectedExecutionMode = agent.ExecutionMode;
            binding.ProjectedProviderName = agent.ProviderName;
            binding.ProjectedDefaultModel = agent.DefaultModel;
            binding.ProjectedCapabilityCount = agent.Capabilities.Length;
            binding.ProjectedRoleTitle = agent.RoleTitle;
            binding.ProjectedInstructions = agent.Instructions;
            binding.ProjectedTemplateKey = agent.TemplateKey;
            binding.ProjectedTagsJson = JsonSerializer.Serialize(agent.Tags);
            binding.ProjectedCapabilitiesJson = JsonSerializer.Serialize(agent.Capabilities);
            Stamp(binding, source, revision, AiTechnicalProjectionAvailability.Present, timestamp);
            foreach (var duplicate in previous.Where(item => item.Id != binding.Id)) {
                MarkUnavailable(duplicate, source, revision, AiTechnicalProjectionAvailability.Superseded,
                    $"Technical Agent '{agent.TechnicalAgentId:D}' is bound to CRM Party '{binding.PartyId:D}'.", timestamp);
            }
        }

        foreach (var missing in eligible.Where(binding => binding.TechnicalAgentId.HasValue &&
            binding.ProjectionAvailability != AiTechnicalProjectionAvailability.Superseded && !currentIds.Contains(binding.TechnicalAgentId.Value))) {
            MarkUnavailable(missing, source, revision, AiTechnicalProjectionAvailability.Missing,
                "Referenced technical Agent is missing from the organization catalog. The last projection is retained as history.", timestamp);
        }
    }

    private static bool BelongsToSource(AiResourceBinding binding, AiTechnicalProjectionSource source)
        => binding.SourceDatabaseProfileId is null || binding.SourceDatabaseProfileId == source.DatabaseProfileId &&
            binding.SourceScopeKind == source.Scope.Kind && string.Equals(binding.SourceScopeKey, source.Scope.Key, StringComparison.Ordinal);

    private static void MarkUnavailable(AiResourceBinding binding, AiTechnicalProjectionSource source, CatalogDataRevision revision,
        AiTechnicalProjectionAvailability availability, string reason, DateTimeOffset timestamp) {
        binding.BindingStatus = AiResourceBindingStatus.Error;
        binding.BindingReason = reason;
        binding.LastError = reason;
        Stamp(binding, source, revision, availability, timestamp);
    }

    private static void Stamp(AiResourceBinding binding, AiTechnicalProjectionSource source, CatalogDataRevision revision,
        AiTechnicalProjectionAvailability availability, DateTimeOffset timestamp) {
        binding.SourceDatabaseProfileId = source.DatabaseProfileId;
        binding.SourceScopeKind = source.Scope.Kind;
        binding.SourceScopeKey = source.Scope.Key;
        binding.SourceCatalogRevision = revision.Value;
        binding.ProjectionAvailability = availability;
        binding.ProjectionUpdatedAtUtc = timestamp;
        binding.UpdatedAtUtc = timestamp;
    }

    private static ImmutableArray<AiTechnicalProjectionEntry> Normalize(ImmutableArray<AiTechnicalProjectionEntry> agents) {
        if (agents.Any(agent => agent is null || agent.TechnicalAgentId == Guid.Empty || string.IsNullOrWhiteSpace(agent.DisplayName) ||
            agent.DisplayName.Length > 200 || agent.Tags.IsDefault || agent.Capabilities.IsDefault) ||
            agents.Select(agent => agent.TechnicalAgentId).Distinct().Count() != agents.Length) {
            throw new ArgumentException("Technical projection entries require unique identities, names and complete collections.", nameof(agents));
        }

        return agents.OrderBy(agent => agent.TechnicalAgentId).Select(agent => agent with {
            DisplayName = agent.DisplayName.Trim(),
            Tags = agent.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim())
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ThenBy(tag => tag, StringComparer.Ordinal)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToImmutableArray(),
            Capabilities = agent.Capabilities.Select(capability => new AiTechnicalProjectionCapability(
                capability.Name?.Trim() ?? string.Empty, capability.Scope?.Trim() ?? string.Empty, capability.ToolAccess?.Trim() ?? string.Empty,
                capability.Limitations?.Trim() ?? string.Empty, capability.Notes?.Trim() ?? string.Empty)).ToImmutableArray()
        }).ToImmutableArray();
    }

    private static AiTechnicalProjectionResource ReadResource(Guid partyId, PartyType? partyType, string displayName,
        string summary, AiResourceBinding? binding) {
        var present = binding is { TechnicalAgentId: not null, BindingStatus: AiResourceBindingStatus.Bound, ProjectionUpdatedAtUtc: not null } &&
            binding.ProjectionAvailability is AiTechnicalProjectionAvailability.Unknown or AiTechnicalProjectionAvailability.Present;
        var reason = binding is null ? "No technical binding." : string.IsNullOrWhiteSpace(binding.BindingReason) ? binding.BindingStatus.ToString() : binding.BindingReason;
        var route = binding?.TechnicalAgentId is Guid id ? $"/agents?tab=agents&agentId={id:D}" : "/agents?tab=agents";
        var status = binding?.BindingStatus ?? AiResourceBindingStatus.Unbound;
        var mode = present ? binding!.ProjectedExecutionMode : null;
        var provider = present ? binding!.ProjectedProviderName : string.Empty;
        var model = present ? binding!.ProjectedDefaultModel : string.Empty;
        AiTechnicalProjectionProvenance? provenance = binding is { SourceDatabaseProfileId: Guid profile, SourceScopeKind: WorkspaceScopeKind kind, SourceCatalogRevision: long revision }
            ? new(new(profile, new(kind, binding.SourceScopeKey)), new(revision), binding.ProjectionAvailability,
                binding.ProjectedDisplayName, binding.ProjectedSummary, binding.ProjectedLifecycleStatus)
            : null;
        var directory = new AiTechnicalAgentDirectorySummary(binding?.TechnicalAgentId, status, reason, mode, provider, model,
            present ? binding!.ProjectedCapabilityCount : 0, present, route) { Projection = provenance };
        var staffing = new AiAgentStaffingFactModel(partyId, binding?.TechnicalAgentId, displayName,
            present ? binding!.ProjectedRoleTitle : string.Empty, summary, present ? binding!.ProjectedInstructions : string.Empty,
            status, reason, mode, provider, model, present ? binding!.ProjectedTemplateKey : string.Empty,
            present ? Deserialize<string>(binding!.ProjectedTagsJson, binding.Id) : [],
            present ? Deserialize<AiCapabilityEditorModel>(binding!.ProjectedCapabilitiesJson, binding.Id) : [], route);
        return new(partyId, partyType, displayName, summary, directory, staffing);
    }

    private static IReadOnlyList<T> Deserialize<T>(string json, Guid bindingId) {
        try {
            return JsonSerializer.Deserialize<List<T>>(json) ?? [];
        } catch (JsonException exception) {
            throw new InvalidOperationException($"AI resource binding '{bindingId:D}' contains invalid technical projection JSON.", exception);
        }
    }

    private static PartyLifecycleStatus InitialLifecycle(AgentLifecycleStatus status) => status switch {
        AgentLifecycleStatus.Active => PartyLifecycleStatus.Active,
        AgentLifecycleStatus.Suspended => PartyLifecycleStatus.Inactive,
        AgentLifecycleStatus.Archived => PartyLifecycleStatus.Archived,
        _ => PartyLifecycleStatus.Draft
    };
}
