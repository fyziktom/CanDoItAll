# SB08 Semantic Invariants

## Template Contract Rules

- Runtime-enforceable template behavior must be represented as typed template metadata, not only as markdown or prompt prose.
- `executionClass` values are centralized in `ProcessTemplateStepExecutionClasses`.
- `AgentWithToolPlanGuard` and `DeterministicToolPlan` steps require typed deterministic tool-plan metadata.
- Deterministic tool plans must declare a resolved script ref or a launch-variable script ref, at least one operation, required receipt metadata, and readback checks.
- Strict validation rejects unresolved template placeholders in deterministic script refs.
- Runtime-owned subprocess steps must declare typed subprocess contracts and child output references that resolve to child steps and artifact expectations.
- Produced artifact slots must reference an artifact expectation declared by the same step.
- Branch outcome keys must remain stable typed identifiers.

## Migration Boundary

- Strict execution-contract validation is opt-in so existing templates remain migratable in SB09.
- Normal compatibility scanning remains available for migration planning.
- SB09 must use the SB08 diagnostics to migrate or explicitly audit every template with deterministic tools, runtime-owned subprocesses, artifact slots, no-go outputs, or branch decisions.

## Production Behavior Artifact Matrix

| Template state | Strict validation result | Runtime implication |
| --- | --- | --- |
| Hard tool gate only in prose | `ProseOnlyHardGate` | Runtime cannot enforce it; template must be migrated before hard runtime execution. |
| Guarded tool plan without deterministic metadata | `MissingDeterministicToolPlan` | Agent dispatch cannot be trusted as deterministic setup proof. |
| Deterministic script ref has `{CurrentProcessRunId}` | `InvalidDeterministicToolPlan` | Placeholder would reproduce the 5032 unresolved-path failure class. |
| Deterministic plan lacks required receipts | `MissingRequiredReceiptMetadata` | Completion cannot prove the required tool actually ran. |
| Deterministic plan lacks readback checks | `MissingReadbackChecks` | File/product state can be falsely accepted. |
| Parent subprocess references unknown child output | `UnknownSubprocessChildOutputStep` | Parent bridge cannot map child completion evidence safely. |
| Produced artifact slot lacks matching expectation | `MissingProducedArtifactSlot` | Artifact materialization cannot be tied to a typed slot. |

## Architecture

- The contract document types stay in `Processes.Templates` because they describe template JSON shape and loader/scanner validation.
- Runtime/application contracts in `Processes.Contracts` were not expanded in SB08 because no runtime consumer requires a shared binary contract yet.
- CodeAnalytics snapshot `snap-20260708195818-85ab0701` reported no scoped dependency cycles.
- The scanner strict-validation methods are isolated in `ProcessTemplateCompatibilityScanner.ExecutionContracts.cs`; they do not change normal compatibility mode behavior before SB09 migration.


## Completed Validator Contract

- Invariant ID: SB08-FINAL-001
- Source raw note: GPTPro Extended escalation root-cause analysis and the user's broader process/template/artifact repair requirement.
- Expected behavior: The completed subbundle behavior remains implemented, tested, and represented by typed proof artifacts.
- Disallowed shallow implementation: Do not close the phase with prose-only proof, build-only proof, or hidden prompt-only gates.
- Failing-first test: N/A process/non-production final proof uses adversarial negative tests or preserved subbundle evidence in proof/SB08/transcripts/00-validator-metadata.txt.
- Passing test: Completed proof metadata is recorded in proof/SB08/transcripts/00-validator-metadata.txt and the subbundle manifest.
- Changed source files: bundle://subbundles/08-sb08-template-schema-execution-contracts/README.md and bundle://proof/SB08/manifest.md.
- Production assertions: Runtime/template/process behavior remains covered by the subbundle proof manifest and final bundle validation.
- Red-team negative case: Shallow final closure without proof metadata, semantic invariant labels, and transcripts is rejected by the completed validator.
- Downstream dependency check: Final bundle validation and recorded CodeAnalytics snapshots verify no unresolved downstream gate remains.

