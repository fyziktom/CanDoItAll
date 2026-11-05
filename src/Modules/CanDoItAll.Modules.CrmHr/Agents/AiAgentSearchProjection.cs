using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class AiAgentSearchProjection(IDbContextFactory<CrmHrDbContext> contexts, ISearchIndexService search,
    IAiTechnicalAgentBridge technical) {
    public async Task UpsertAsync(Guid partyId, CancellationToken cancellationToken) {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        var party = await context.Set<Party>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == partyId, cancellationToken);
        var profile = await context.Set<AiAgentProfile>().AsNoTracking().SingleOrDefaultAsync(row => row.PartyId == partyId, cancellationToken);
        if (party is null || profile is null || party.IsSensitive) {
            await search.DeleteAsync(CrmHrSearchSourceTypes.AiAgent, partyId.ToString("N"), cancellationToken);
            return;
        }
        var workspace = await technical.GetWorkspaceAsync(partyId, cancellationToken);
        var owner = profile.OwnerPartyId is { } ownerId
            ? await context.Set<Party>().AsNoTracking().Where(row => row.Id == ownerId).Select(row => row.DisplayName).SingleOrDefaultAsync(cancellationToken)
            : null;
        await search.UpsertAsync(new(CrmHrSearchSourceTypes.AiAgent, partyId.ToString("N"), "CRM / HR AI agent", party.DisplayName,
            $"{workspace.ExecutionMode} / {profile.ValidationStatus}", string.Join(Environment.NewLine,
                new[] { party.DisplayName, party.Summary, workspace.ProviderName, workspace.DefaultModel, owner, workspace.BindingSummary, profile.Notes }
                    .Where(value => !string.IsNullOrWhiteSpace(value))), $"/crm-hr/agents?partyId={partyId:D}"), cancellationToken);
    }
}
