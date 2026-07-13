# SB09 Semantic Invariants

## Template Migration Rules

- A hard runtime gate is closed only when represented by typed template metadata or an explicit audited exception.
- Deterministic .NET solution setup steps declare `AgentWithToolPlanGuard` plus deterministic tool plans, required receipts, produced artifact slots, script-ref launch variables, side-effect manifest launch variables, and readback checks.
- Runtime-owned subprocess parent steps declare `RuntimeOwnedSubprocess` and preserve typed child output mappings through `SubprocessContract`.
- Branch decision steps declare `BranchDecision`; branch outcome keys must match stable identifier syntax.
- Screenshot/browser/image-analysis writeback steps declare required runtime tool receipts and produced artifact slots instead of relying on prose.
- Business artifact templates declare semantic acceptance rules and explicitly reject file-existence-only proof.

## Anti-Stub Audit

- Strict full-pack validation returned zero `ProcessTemplateContractDiagnostic` records.
- Removing the typed `ExecutionContract` from real `dotnet-solution-setup:create-dotnet-project` produces `MissingExecutionContract`.
- The previous `branching-code-review` sentinel keys `__default__` and `__error__` no longer appear under `Templates/Processes`, so branch decisions use stable identifiers.
- The full audit CSV marks every source-controlled file in SB09 scope as `Migrated`, `Prose-only risk removed or explicit exception`, or `Explicit exception`.

## Production Behavior Artifact Matrix

| Template area | Typed disposition | Runtime implication |
| --- | --- | --- |
| `.NET solution setup` | Deterministic tool plans and required receipts | Empty solution/scaffold-only completion cannot be justified from prose. |
| Subprocess parents | `RuntimeOwnedSubprocess` plus child output mappings | Parent bridges validate child step and artifact expectation references. |
| Runtime validation branches | `BranchDecision` plus required receipt contracts where hard tools are named | Branch completion must cite current-run tool proof instead of upstream claims. |
| Screenshot writeback | Tool-plan receipts and produced slots | Screenshot paths alone are not accepted without browser/image/tool proof. |
| Business artifacts | `SemanticAcceptanceContract` | Markdown/JSON file existence is not enough; semantic review evidence is required. |

## Architecture

- Template data remains under `Templates/Processes`; runtime code does not parse markdown to discover hard gates.
- Scanner generalization stays in `Processes.Templates` because it validates template JSON shape, not runtime execution.
- No new project references were introduced.
- CodeAnalytics snapshot `snap-20260708201501-85ab0701` reported no scoped dependency cycles.


## Completed Validator Contract

- Invariant ID: SB09-FINAL-001
- Source raw note: GPTPro Extended escalation root-cause analysis and the user's broader process/template/artifact repair requirement.
- Expected behavior: The completed subbundle behavior remains implemented, tested, and represented by typed proof artifacts.
- Disallowed shallow implementation: Do not close the phase with prose-only proof, build-only proof, or hidden prompt-only gates.
- Failing-first test: N/A process/non-production final proof uses adversarial negative tests or preserved subbundle evidence in proof/SB09/transcripts/00-validator-metadata.txt.
- Passing test: Completed proof metadata is recorded in proof/SB09/transcripts/00-validator-metadata.txt and the subbundle manifest.
- Changed source files: bundle://subbundles/09-sb09-template-artifact-audit-migration/README.md and bundle://proof/SB09/manifest.md.
- Production assertions: Runtime/template/process behavior remains covered by the subbundle proof manifest and final bundle validation.
- Red-team negative case: Shallow final closure without proof metadata, semantic invariant labels, and transcripts is rejected by the completed validator.
- Downstream dependency check: Final bundle validation and recorded CodeAnalytics snapshots verify no unresolved downstream gate remains.

