using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public interface IAiAgentCreationGuard {
    Task<Result<AiAgentCreationAdmission>> OpenAsync(AiAgentProfileEditorModel model, CancellationToken cancellationToken);
}

public sealed class AiAgentCreationAdmission : IAsyncDisposable {
    private readonly CrmHrDbContext context;
    private readonly SerializableMutationScope transaction;
    private readonly CrmHrChanges changes;

    internal AiAgentCreationAdmission(CrmHrDbContext context, SerializableMutationScope transaction, CrmHrChanges changes,
        AiStewardProfileObservation original) {
        this.context = context;
        this.transaction = transaction;
        this.changes = changes;
        Original = original;
    }

    public AiStewardProfileObservation Original { get; }
    internal CrmHrDbContext Context => context;
    public ValueTask DemandCommitAsync(CancellationToken cancellationToken) => changes.DemandCommitAsync(cancellationToken);
    internal Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public async ValueTask DisposeAsync() {
        try {
            await transaction.DisposeAsync();
        } finally {
            await context.DisposeAsync();
        }
    }
}

public sealed class AiAgentCreationGuard(IDbContextFactory<CrmHrDbContext> contexts, CrmHrChanges changes) : IAiAgentCreationGuard {
    public async Task<Result<AiAgentCreationAdmission>> OpenAsync(AiAgentProfileEditorModel model, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(model);
        var draft = model.Snapshot();
        if (draft.PartyId == Guid.Empty || draft.Id.HasValue || draft.Original is not null) {
            return Result<AiAgentCreationAdmission>.Failure(Error.Validation(
                "This command only creates a missing technical profile. Edit existing technical configuration through AgentFramework.", AiStewardEditingErrors.TechnicalReadOnly));
        }
        await changes.DemandCommitAsync(cancellationToken);
        var context = await contexts.CreateDbContextAsync(cancellationToken);
        SerializableMutationScope? transaction = null;
        var admitted = false;
        try {
            transaction = await SerializableMutationScope.BeginAsync(context,
                [OpportunityPartyReferences.ScopeKey(draft.PartyId), AiStewardEditValues.ScopeKey(draft.PartyId)], cancellationToken);
            var party = await context.Set<Party>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == draft.PartyId, cancellationToken);
            if (party?.PartyType != PartyType.AiAgent) {
                return Result<AiAgentCreationAdmission>.Failure(Error.Validation(
                    "Technical creation requires a current AI party.", AiStewardEditingErrors.PartyTypeConflict));
            }
            if (await context.Set<AiAgentProfile>().AnyAsync(row => row.PartyId == party.Id, cancellationToken) ||
                await context.Set<AiResourceBinding>().AnyAsync(row => row.PartyId == party.Id, cancellationToken)) {
                return Result<AiAgentCreationAdmission>.Failure(Error.Validation(
                    "This AI party already has a profile or binding. Reconcile it through its existing owner.", AiStewardEditingErrors.Conflict));
            }
            await changes.DemandCommitAsync(cancellationToken);
            var admission = new AiAgentCreationAdmission(context, transaction, changes, AiStewardEditValues.Observe(party.Id, party.PartyType, null));
            admitted = true;
            return Result<AiAgentCreationAdmission>.Success(admission);
        } finally {
            if (!admitted) {
                try {
                    if (transaction is not null) {
                        await transaction.DisposeAsync();
                    }
                } finally {
                    await context.DisposeAsync();
                }
            }
        }
    }
}
