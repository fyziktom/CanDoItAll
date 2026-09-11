using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed record AiAgentDiagnosticParty(Guid Id, string DisplayName);

public sealed record AiAgentDiagnosticBinding(
    Guid PartyId,
    Guid? TechnicalAgentId,
    AiResourceBindingStatus BindingStatus,
    string BindingReason);

public sealed partial class AiAgentService {
    public async Task<IReadOnlyList<AiAgentDiagnosticParty>> ListDiagnosticPartiesAsync(CancellationToken cancellationToken = default) {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Party>().AsNoTracking()
            .Where(party => party.PartyType == PartyType.AiAgent)
            .OrderBy(party => party.DisplayName)
            .Select(party => new AiAgentDiagnosticParty(party.Id, party.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiAgentDiagnosticBinding>> ListDiagnosticBindingsAsync(
        IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(partyIds);
        var ids = partyIds.ToArray();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<AiResourceBinding>().AsNoTracking()
            .Where(binding => ids.Contains(binding.PartyId))
            .Select(binding => new AiAgentDiagnosticBinding(
                binding.PartyId, binding.TechnicalAgentId, binding.BindingStatus, binding.BindingReason))
            .ToListAsync(cancellationToken);
    }
}
