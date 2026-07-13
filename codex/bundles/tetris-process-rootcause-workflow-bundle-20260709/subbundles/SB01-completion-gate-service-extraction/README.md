# SB01 Completion Gate Service Extraction

## Status

- `Completed`

## Objective

Extract completion gate evaluation from the adapter partial cluster into testable services without changing behavior.

Fresh corrective evidence shows the old closure only extracted aggregation while private gate behavior, routing, artifacts, and result conversion remained in the adapter partial cluster. The original progression gate is therefore invalid. SB12 owns the complete corrective extraction and must close before this subbundle may be considered complete again.

## Covered Inputs

- GPTPro Phase 1 extraction plan.
- C# architecture governor partial-class and testability requirements.
- Source hotspot inventory for adapter partial files.

## Prerequisites

- SB00 entry gate and characterization tests are in place.
- Current behavior tests are runnable or blocker is documented.
- Architecture checkpoint after SB00 shows no production behavior change.

## Exact Source References

- `bundle://03-source-map.md`
- `bundle://04-target-architecture.md`
- `bundle://codex-tasks/02-extract-completion-gate-services.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateFactory.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionReceiptGate.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessProductCompletionPathGate.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`

## Deliverables

- Extracted completion gate evaluator/context/evaluation records.
- Adapter delegates pure completion evaluation to the extracted service.
- Existing behavior is preserved.
- New service-level tests exercise the evaluator without full MAF runtime construction.
- Architecture checkpoint evidence records old adapter responsibility shrink or thin delegation.

## Dependency Impact

- SB02-SB04 depend on extracted evaluator/resolver seams.
- If this phase leaves core behavior in private adapter methods, later branch/routing work will be hard to test and must reopen SB01.

## Validation Depth

- Critical foundation.
- Requires existing adapter tests, new service-level tests, source assertions, and old-class shrink proof.

## Implementation Steps

1. Identify the smallest cohesive extraction boundary around `EvaluateCompletionGates`.
2. Move pure issue aggregation and ordering into a top-level service or internal service consistent with repo style.
3. Keep adapter responsible for MAF execution and result envelope conversion.
4. Do not introduce an interface unless tests or composition need it.
5. Add tests that instantiate the evaluator directly with assignment/output/receipts.
6. Preserve issue codes, summaries, ordering, and retry safety behavior.
7. Update architecture checkpoint with before/after file responsibility summary.

## C# Architecture Impact

This phase separates core gate evaluation from the adapter partial cluster and creates the test seam required for branch-aware behavior.

## Boundary Ownership

- Generic completion evaluation belongs to generic process/module integration services.
- MAF execution stays in the adapter.
- Product-specific/Workbench-specific rules are not introduced here.

## Dependency Direction

- Do not add dependency from process runtime/application to Workbench.
- Do not create cycles between Modules.Processes and Processes.Runtime/Application.

## Pattern Decision

- Service extraction with thin facade.
- Rejected: another permanent partial file containing the same private logic.

## Testability Contract

- New tests must call extracted evaluator without constructing MAF runtime or launching process runs.
- Existing tests must still prove adapter integration.

## Partial Class Policy

- Temporary partial movement is allowed only as an intermediate step.
- Final SB01 state must not add a new permanent partial as the architectural boundary.

## Architecture Proof Required

- Before/after source assertion showing adapter delegates pure gate evaluation.
- Test transcript for extracted evaluator.
- CodeAnalytics or source inventory note proving no new project cycle.

## Do Not Do

- Do not change branch routing semantics in SB01.
- Do not add branch-aware receipt rule model in SB01.
- Do not hardcode QA/software-delivery terms.

## Acceptance Checklist

- Existing behavior tests pass or failures are documented as pre-existing.
- Extracted evaluator has direct unit coverage.
- Adapter remains the MAF integration facade.
- No new domain terms appear in generic evaluator code.

## Proof Required

- `bundle://proof/SB01/manifest.md` after execution.
- `bundle://proof/SB01/semantic-invariants.md` after execution.
- Changed-file hashes.
- Existing behavior test transcript.
- Extracted service test transcript.
- Source assertion transcript for adapter shrink/thin delegation.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A for SB01; this is backend architecture and unit-test work.

## Progression Gate

- SB02 may start only after the evaluator extraction passes behavior-preserving tests and architecture checkpoint review.

## Suggested Agent Prompt

Implement SB01 as a behavior-preserving extraction. Create a real test seam for completion gate evaluation and prove existing behavior did not change.
