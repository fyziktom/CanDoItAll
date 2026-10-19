using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessDecisionMaterialStore(ProcessPersistenceDbContext dbContext, ICanonicalRuntimeDatabase? runtimeDatabase = null)
    : IProcessDecisionMaterialReader {
    public async Task<ProcessDecisionMaterial?> ReadAsync(ProcessDecisionMaterialScope originalScope, ProcessDecisionMaterialReference reference,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(originalScope);
        ArgumentNullException.ThrowIfNull(reference);
        originalScope.Validate();
        reference.Validate();
        RequireProfile(originalScope);
        if (reference.ScopeId != originalScope.Id) {
            throw new InvalidDataException("The material reference belongs to another original scope.");
        }
        var row = await (from material in dbContext.DecisionMaterials.AsNoTracking()
            join scope in dbContext.DecisionMaterialScopes.AsNoTracking() on material.ScopeId equals scope.ScopeId
            where material.MaterialId == reference.MaterialId.Value && material.ScopeId == reference.ScopeId.Value &&
                material.Generation == reference.Generation && scope.DatabaseProfileId == originalScope.DatabaseProfileId &&
                scope.WorkspaceNamespaceId == originalScope.WorkspaceNamespaceId && scope.RootRunId == originalScope.RootRunId.Value &&
                scope.OwnerRunId == originalScope.OwnerRunId.Value && scope.StepInstanceId == (originalScope.StepInstanceId.HasValue ? originalScope.StepInstanceId.Value.Value : (Guid?)null)
            select new { Material = material, Scope = scope }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : Read(row.Scope, row.Material);
    }

    internal async Task RequireOriginalAsync(ProcessDecisionMaterial original, CancellationToken cancellationToken) {
        var retained = await ReadAsync(original.Scope, original.Reference, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original Process material is missing from canonical storage.");
        original.RequireOriginal(retained);
    }

    internal async Task StageAsync(ProcessRuntimeStateSnapshot state, ProcessDecisionDataPolicyAdmission admission,
        ProcessDecisionCommand command, ProcessDecisionMaterial? material, ProcessDecisionMaterialId? previousMaterialId,
        CancellationToken cancellationToken) {
        RequireCommandMaterial(command, material);
        if (material is null) {
            if (admission.ExecutionContract is not null) {
                throw new InvalidDataException("The admitted Process execution contract requires its original canonical material.");
            }
            return;
        }
        RequireProfile(material.Scope);
        admission.RequireExecutionContract(material.ExecutionContract);
        if (admission.MaterialProtocol != ProcessDecisionMaterialProtocol.CanonicalSnapshotV1) {
            throw new InvalidDataException("The original Process admission does not declare canonical material support.");
        }
        if (material.Scope.RootRunId != state.RootRunId || material.Scope.OwnerRunId != state.RunId ||
                material.Scope.WorkspaceNamespaceId != admission.WorkspaceNamespaceId || material.Scope.ReadBasisMode != admission.Policy.ReadBasisMode ||
                material.Scope.StepInstanceId is { } step && step != command.StepInstanceId ||
                state.ProjectAdmission is { } project && project.DatabaseProfileId != material.Scope.DatabaseProfileId) {
            throw new InvalidDataException("The material scope differs from its original Process owner and policy admission.");
        }
        if (command is ProcessDecisionRefresh && material.Scope.ReadBasisMode == ReadBasisMode.RunSnapshot) {
            throw new InvalidOperationException("Run-scope refresh requires the shared parent reconciliation owner.");
        }
        if (dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("Process material requires the runtime owner's fenced transaction.");
        }
        if (previousMaterialId is { } previous) {
            var retainedPrevious = await dbContext.DecisionMaterials.AsNoTracking().Where(item => item.MaterialId == previous.Value)
                .Select(item => new { item.ScopeId, item.Generation, item.CapturedAtUtcTicks }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (retainedPrevious is null || retainedPrevious.ScopeId != material.Scope.Id.Value ||
                    material.Reference.Generation != retainedPrevious.Generation + 1 || material.CapturedAtUtc.UtcTicks < retainedPrevious.CapturedAtUtcTicks) {
                throw new InvalidDataException("A refresh must append to its original material scope and capture history.");
            }
        } else if (material.Reference.Generation != 1) {
            throw new InvalidDataException("A Process decision must start with its original material generation.");
        }
        var scopeRow = await dbContext.DecisionMaterialScopes.AsNoTracking().SingleOrDefaultAsync(item =>
            item.ScopeId == material.Scope.Id.Value, cancellationToken).ConfigureAwait(false);
        if (scopeRow is null) {
            if (material.Reference.Generation != 1) {
                throw new InvalidDataException("The original Process material scope must begin with generation one.");
            }
            dbContext.DecisionMaterialScopes.Add(new() {
                ScopeId = material.Scope.Id.Value, DatabaseProfileId = material.Scope.DatabaseProfileId,
                WorkspaceNamespaceId = material.Scope.WorkspaceNamespaceId, RootRunId = state.RootRunId.Value, OwnerRunId = state.RunId.Value,
                StepInstanceId = material.Scope.StepInstanceId?.Value, ContractId = material.ExecutionContract.ContractId,
                ContractVersion = material.ExecutionContract.Version, Executor = material.ExecutionContract.Executor
            });
        } else if (Scope(scopeRow) != material.Scope || Contract(scopeRow) != material.ExecutionContract) {
            throw new InvalidDataException("The original Process material scope or executor contract changed.");
        }
        var retained = await dbContext.DecisionMaterials.AsNoTracking().SingleOrDefaultAsync(item => item.MaterialId == material.Reference.MaterialId.Value,
            cancellationToken).ConfigureAwait(false);
        if (retained is not null) {
            if (scopeRow is null || retained.ScopeId != scopeRow.ScopeId) {
                throw new InvalidDataException("The material identity is already owned by another scope.");
            }
            material.RequireOriginal(Read(scopeRow, retained));
            return;
        }
        dbContext.DecisionMaterials.Add(new() {
            MaterialId = material.Reference.MaterialId.Value, ScopeId = material.Scope.Id.Value, Generation = material.Reference.Generation,
            CapturedAtUtcTicks = material.CapturedAtUtc.UtcTicks, PayloadJson = material.PayloadJson,
            PayloadUtf8Bytes = material.PayloadUtf8Bytes, Fingerprint = material.Fingerprint
        });
    }

    internal static void RequireCommandMaterial(ProcessDecisionCommand? command, ProcessDecisionMaterial? material) {
        command?.Validate();
        if (command?.Material != material?.Stamp) {
            throw new InvalidDataException("A material-bearing Process command requires the exact separately supplied original payload.");
        }
    }

    private void RequireProfile(ProcessDecisionMaterialScope scope) {
        var current = runtimeDatabase ?? throw new InvalidOperationException("Canonical material requires the selected runtime database profile.");
        if (current.Profile.Profile.Id != scope.DatabaseProfileId) {
            throw new InvalidDataException("The original material belongs to another database profile.");
        }
    }

    private static ProcessDecisionMaterialScope Scope(ProcessDecisionMaterialScopeEntity row)
        => new(new(row.ScopeId), row.DatabaseProfileId, row.WorkspaceNamespaceId, new(row.RootRunId), new(row.OwnerRunId),
            row.StepInstanceId is { } step ? new(step) : null);

    private static ProcessDecisionExecutionContract Contract(ProcessDecisionMaterialScopeEntity row) => new(row.ContractId, row.ContractVersion, row.Executor);

    private static ProcessDecisionMaterial Read(ProcessDecisionMaterialScopeEntity scope, ProcessDecisionMaterialEntity row) {
        var material = new ProcessDecisionMaterial(Scope(scope), new(new(row.ScopeId), row.Generation, new(row.MaterialId)), Contract(scope),
            new(row.CapturedAtUtcTicks, TimeSpan.Zero), row.PayloadJson);
        if (row.Fingerprint != material.Fingerprint || row.PayloadUtf8Bytes != material.PayloadUtf8Bytes) {
            throw new InvalidDataException("The retained Process material failed its original content fingerprint or byte bound.");
        }
        return material;
    }
}
