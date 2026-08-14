# M01 proof manifest

## Scope

- Subbundle: M01
- Proof tier: Governed
- Repository anchor: `386d8beb6038035f89a9a6961ec017d8213879a5` plus accepted M00 changes
- Authoritative dependency mode: package
- Host: Windows x64
- SDK/runtime: `10.0.303` / `10.0.11`
- CodeAnalytics snapshot: `snap-20260812113133-65c5b773`

## Required artifacts

| Artifact | Purpose |
|---|---|
| `transcripts/failing-first-legacy-plan.txt` | Proves the original generic failure |
| `transcripts/validation.txt` | Records focused build/test results |
| `semantic-invariants.md` | Maps requirements to behavioral evidence |
| `architecture-gate.md` | Records governed C# boundary review |

## Changed production scope

- `src/Processes/CanDoItAll.Processes.Builder/ProcessPlanHasher.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/EfProcessInstancePlanStore.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/ProcessInstancePlanPersistenceMapper.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceConfigurations.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceEntities.cs`
- `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260812112732_AddProcessPlanHashVersioning.cs`
- generated migration designer and model snapshot

## Integrity

The final working-tree fingerprint and artifact SHA-256 values are reconciled by M07/M10 after all later subbundles stop invalidating bundle records.
