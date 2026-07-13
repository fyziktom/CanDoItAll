# 25 Native Db Context And Persistence Extraction

## Status

- `Completed`

## Objective

- Create native `CognitiveMemoryDbContext`, move native EF records/configurations/migrations, add InMemory/PostgreSQL profiles, and stop new native records from entering AppDbContext.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R14
- R15
- R18

## Prerequisites

- SB24 completed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Foundation/CognitiveMemoryEntities.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Foundation/CognitiveMemoryEntityConfigurations.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Extract native Cognitive Memory entities, EF configurations, DbContext, migrations, and persistence services from the main module into the native service persistence project.
- Create `CognitiveMemoryDbContext` with `IDbContextFactory<CognitiveMemoryDbContext>`, InMemory test profile, PostgreSQL profile, migrations, and no dependency on host `AppDbContext`.
- Replace AppDbContext-native entity coupling in migrated services with native persistence abstractions.
- Use async EF APIs and `AsNoTracking`/projection queries where mutation is not required.
- Add migration tests for native schema creation and old main DB export/read compatibility where needed.

## Dependency Impact

- Engine migration and host dependency removal depend on native persistence ownership.

## Validation Depth

- `Critical native foundation`

## Implementation Steps

1. Inventory native entity/configuration registrations currently in `CognitiveMemoryEntities.cs`, `CognitiveMemoryEntityConfigurations.cs`, `AppDbContext`, and model registry.
2. Create native DbContext and move native EF configurations into the native persistence project.
3. Add native migrations and test-time InMemory factory setup.
4. Refactor migrated persistence services to accept native context factory/repositories instead of host context factory.
5. Add tests for create/read/update paths, no-tracking query behavior, migrations, and absence of host DbContext references.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- Native memory domain records are persisted through the native DbContext, not the main AppDbContext.
- Native persistence supports InMemory tests and PostgreSQL migrations.
- Async/no-tracking patterns are used consistently and verified by tests or source review.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB25/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB25/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB25/manifest.md` and `proof/SB25/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run native-service build/test commands from the `CanDoItAll.CognitiveMemory` repository after confirming real target paths, and capture transcript paths in the manifest.
- Run native persistence tests for InMemory profile and PostgreSQL migration profile where available.
- Run source audit proving native persistence no longer depends on host `AppDbContext` and main AppDbContext no longer registers new native memory models.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB25 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB25 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
