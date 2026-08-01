using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public static class CrmHrSchemaInitializer
{
    public static async Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var existingKeys = await dbContext.Set<CrmHrLookupOption>()
            .Select(option => new { option.CatalogKind, option.Key })
            .ToListAsync(cancellationToken);

        var existing = existingKeys
            .Select(item => $"{item.CatalogKind}:{item.Key}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var option in GetDefaultLookupOptions())
        {
            if (existing.Contains($"{option.CatalogKind}:{option.Key}"))
            {
                continue;
            }

            dbContext.Set<CrmHrLookupOption>().Add(option);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<CrmHrLookupOption> GetDefaultLookupOptions()
    {
        var updatedAtUtc = DateTimeOffset.UtcNow;
        return
        [
            CreateLookup(LookupCatalogKind.OpportunityStage, "Identified", "Identified", 10, updatedAtUtc),
            CreateLookup(LookupCatalogKind.OpportunityStage, "Qualified", "Qualified", 20, updatedAtUtc),
            CreateLookup(LookupCatalogKind.OpportunityStage, "Proposal", "Proposal", 30, updatedAtUtc),
            CreateLookup(LookupCatalogKind.OpportunityStage, "Negotiation", "Negotiation", 40, updatedAtUtc),
            CreateLookup(LookupCatalogKind.OpportunityStage, "Won", "Won", 50, updatedAtUtc),
            CreateLookup(LookupCatalogKind.OpportunityStage, "Lost", "Lost", 60, updatedAtUtc),
            CreateLookup(LookupCatalogKind.RelationshipStage, "Prospect", "Prospect", 10, updatedAtUtc),
            CreateLookup(LookupCatalogKind.RelationshipStage, "Qualified", "Qualified", 20, updatedAtUtc),
            CreateLookup(LookupCatalogKind.RelationshipStage, "ActiveCustomer", "Active customer", 30, updatedAtUtc),
            CreateLookup(LookupCatalogKind.RelationshipStage, "Dormant", "Dormant", 40, updatedAtUtc),
            CreateLookup(LookupCatalogKind.AssignmentKind, nameof(ProjectPartyAssignmentKind.Customer), "Customer", 10, updatedAtUtc),
            CreateLookup(LookupCatalogKind.AssignmentKind, nameof(ProjectPartyAssignmentKind.CustomerContact), "Customer contact", 20, updatedAtUtc),
            CreateLookup(LookupCatalogKind.AssignmentKind, nameof(ProjectPartyAssignmentKind.DeliveryUnit), "Delivery unit", 30, updatedAtUtc),
            CreateLookup(LookupCatalogKind.AssignmentKind, nameof(ProjectPartyAssignmentKind.TeamMember), "Team member", 40, updatedAtUtc),
            CreateLookup(LookupCatalogKind.AssignmentKind, nameof(ProjectPartyAssignmentKind.AiAgent), "AI agent", 50, updatedAtUtc)
        ];
    }

    private static CrmHrLookupOption CreateLookup(
        LookupCatalogKind catalogKind,
        string key,
        string displayName,
        int displayOrder,
        DateTimeOffset updatedAtUtc)
    {
        return new CrmHrLookupOption
        {
            CatalogKind = catalogKind,
            Key = key,
            DisplayName = displayName,
            DisplayOrder = displayOrder,
            IsSystemDefault = true,
            UpdatedAtUtc = updatedAtUtc
        };
    }
}
