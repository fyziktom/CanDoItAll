using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

internal enum ProjectReceiptFamilyKind { Definition, Lifecycle }

public sealed class ProjectDefinitionReceiptRetention(DbContextOptions<ProjectsDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IFiniteOperationReceiptRetentionParticipant {
    private readonly ProjectReceiptRetention owner = new(ProjectReceiptFamilyKind.Definition, options, transactions);
    public FiniteOperationPoolId Pool => owner.Pool;
    public FiniteReceiptFamilyId Family => owner.Family;
    public Task<FiniteReceiptPurgeObservation> PurgeRetiredBatchEnlistedAsync(FiniteReceiptPurgeRequest request, CancellationToken cancellationToken)
        => owner.PurgeRetiredBatchEnlistedAsync(request, cancellationToken);
    public Task<IReadOnlyList<FiniteOperationEpochAddress>> FindNonemptyEpochsEnlistedAsync(FiniteEpochInspectionRequest request, CancellationToken cancellationToken)
        => owner.FindNonemptyEpochsEnlistedAsync(request, cancellationToken);
}

public sealed class ProjectLifecycleReceiptRetention(DbContextOptions<ProjectsDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IFiniteOperationReceiptRetentionParticipant {
    private readonly ProjectReceiptRetention owner = new(ProjectReceiptFamilyKind.Lifecycle, options, transactions);
    public FiniteOperationPoolId Pool => owner.Pool;
    public FiniteReceiptFamilyId Family => owner.Family;
    public Task<FiniteReceiptPurgeObservation> PurgeRetiredBatchEnlistedAsync(FiniteReceiptPurgeRequest request, CancellationToken cancellationToken)
        => owner.PurgeRetiredBatchEnlistedAsync(request, cancellationToken);
    public Task<IReadOnlyList<FiniteOperationEpochAddress>> FindNonemptyEpochsEnlistedAsync(FiniteEpochInspectionRequest request, CancellationToken cancellationToken)
        => owner.FindNonemptyEpochsEnlistedAsync(request, cancellationToken);
}