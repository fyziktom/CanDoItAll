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
        if (command.InheritedScope is not null) {
            throw new InvalidOperationException("Inherited material must pass through its shared run scope owner.");
        }
        if (material is null) {
            if (admission.ExecutionContract is not null) {
                throw new InvalidDataException("The admitted Process execution contract requires its original canonical material.");
            }
            return;
        }
        RequireProfile(material.Scope);
        admission.Validate();
        if (admission.MaterialProtocol != material.Protocol ||
                material.ContentContract is { } content && admission.ContentContract != content) {
            throw new InvalidDataException("The original Process admission does not declare this material protocol and content schema.");
        }
        if (material.ExecutionContract is { } legacyContract) {
            admission.RequireExecutionContract(legacyContract);
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
        await StageCanonicalAsync(material, cancellationToken).ConfigureAwait(false);
    }

    internal void StageRunScope(ProcessDecisionRunScopeAdmission admission) {
        admission.Validate();
        RequireProfile(admission.MaterialScope);
        dbContext.DecisionMaterialScopes.Add(new() {
            ScopeId = admission.ScopeId.Value, DatabaseProfileId = admission.DatabaseProfileId,
            WorkspaceNamespaceId = admission.WorkspaceNamespaceId, RootRunId = admission.Funding.RootRunId.Value,
            OwnerRunId = admission.Funding.RootRunId.Value, ContractId = admission.ContentContract.SchemaId,
            ContractVersion = admission.ContentContract.Version, MaterialProtocol = ProcessDecisionMaterialProtocol.SharedContentV1
        });
    }

    internal async Task StageCanonicalAsync(ProcessDecisionMaterial material, CancellationToken cancellationToken) {
        RequireProfile(material.Scope);
        var scopeRow = await dbContext.DecisionMaterialScopes.AsNoTracking().SingleOrDefaultAsync(item =>
            item.ScopeId == material.Scope.Id.Value, cancellationToken).ConfigureAwait(false);
        if (scopeRow is null) {
            if (material.Reference.Generation != 1) {
                throw new InvalidDataException("The original Process material scope must begin with generation one.");
            }
            dbContext.DecisionMaterialScopes.Add(new() {
                ScopeId = material.Scope.Id.Value, DatabaseProfileId = material.Scope.DatabaseProfileId,
                WorkspaceNamespaceId = material.Scope.WorkspaceNamespaceId, RootRunId = material.Scope.RootRunId.Value, OwnerRunId = material.Scope.OwnerRunId.Value,
                StepInstanceId = material.Scope.StepInstanceId?.Value,
                ContractId = material.ContentContract?.SchemaId ?? material.ExecutionContract!.ContractId,
                ContractVersion = material.ContentContract?.Version ?? material.ExecutionContract!.Version,
                Executor = material.ExecutionContract?.Executor, MaterialProtocol = material.Protocol
            });
        } else if (Scope(scopeRow) != material.Scope || !MatchesContract(scopeRow, material)) {
            throw new InvalidDataException("The original Process material scope or protocol contract changed.");
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

    internal static void RequireCommandMaterial(ProcessDecisionCommand? command, ProcessDecisionMaterial? material,
        ProcessDecisionRunScopeCommand? scope = null) {
        command?.Validate();
        scope?.Validate();
        if (scope is not null && command is not null && !(scope.CompletesRuntimeResult &&
                command is ProcessDecisionClosure { Completion: ProcessDecisionCompletion.Complete or ProcessDecisionCompletion.Cancel })) {
            throw new InvalidDataException("A root scope command cannot also mutate a local decision.");
        }
        var expected = command?.InheritedScope is null ? command?.Material : null;
        expected ??= scope?.Material;
        if (expected != material?.Stamp) {
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

    private static bool MatchesContract(ProcessDecisionMaterialScopeEntity row, ProcessDecisionMaterial material)
        => row.MaterialProtocol == material.Protocol && row.Executor == material.ExecutionContract?.Executor &&
            row.ContractId == (material.ContentContract?.SchemaId ?? material.ExecutionContract!.ContractId) &&
            row.ContractVersion == (material.ContentContract?.Version ?? material.ExecutionContract!.Version);

    private static ProcessDecisionMaterial Read(ProcessDecisionMaterialScopeEntity scope, ProcessDecisionMaterialEntity row) {
        var originalScope = Scope(scope);
        ProcessDecisionMaterialReference reference = new(new(row.ScopeId), row.Generation, new(row.MaterialId));
        DateTimeOffset captured = new(row.CapturedAtUtcTicks, TimeSpan.Zero);
        ProcessDecisionMaterial material = scope.MaterialProtocol switch {
            ProcessDecisionMaterialProtocol.CanonicalSnapshotV1 when scope.Executor is { } executor
                => new(originalScope, reference, new ProcessDecisionExecutionContract(scope.ContractId, scope.ContractVersion, executor), captured, row.PayloadJson),
            ProcessDecisionMaterialProtocol.SharedContentV1 when scope.Executor is null
                => ProcessDecisionMaterial.FromContent(originalScope, reference, new(scope.ContractId, scope.ContractVersion), captured, row.PayloadJson),
            _ => throw new InvalidDataException("The retained Process material has an unsupported protocol or executor/content shape.")
        };
        if (row.Fingerprint != material.Fingerprint || row.PayloadUtf8Bytes != material.PayloadUtf8Bytes) {
            throw new InvalidDataException("The retained Process material failed its original content fingerprint or byte bound.");
        }
        return material;
    }
}
