# SB03 Branch-Aware Gate Enforcement And Dedup

## Status

- `Completed`

## Objective

Apply branch-aware receipt rule enforcement and eliminate duplicate product/process receipt diagnostics for the same semantic requirement.

## Covered Inputs

- GPTPro RC1, RC3, and RC4.
- Requirement R02 and R05.
- Task 04 from GPTPro.

## Prerequisites

- SB02 structured rule parser and launch preservation pass.
- SB00 repair/accepted branch characterization exists.
- SB01 evaluator seam exists.

## Exact Source References

- `bundle://02-root-causes.md`
- `bundle://codex-tasks/04-branch-aware-gates-and-dedup.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionReceiptGate.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateFactory.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`

## Deliverables

- Receipt evaluator filters rules by branch outcome and skip/enforce metadata.
- Trace includes applicable, skipped-by-branch, observed, failed, and missing receipt facts.
- `quality-accepted` still requires acceptance proof when applicable.
- `repair-required` and repair-escalation do not require acceptance-only browser proof when concrete defect evidence exists.
- Duplicate product/capability receipt diagnostics are removed or normalized.

## Dependency Impact

- SB04 branch routing depends on accurate issue classification and skipped-rule facts.
- SB09 .NET lifecycle can build on correct receipt state classification.

## Validation Depth

- Critical foundation.
- Requires accepted/repair positive and negative tests.

## Implementation Steps

1. Add branch outcome to the completion gate context.
2. Filter product completion receipt rules by branch applicability before matching receipts.
3. Extend or wrap capability-scope receipt gate so it can avoid acceptance-only enforcement for repair branch or remove duplicated capability receipt requirements from migrated templates.
4. Classify missing receipts as absent, failed, wrong execution run, skipped by branch, or not applicable.
5. Deduplicate diagnostics by normalized selector and purpose.
6. Add tests for QA/recheck accepted and repair branches.
7. Keep no-route legacy behavior stable.

## C# Architecture Impact

This phase turns branch-aware rule metadata into generic evaluator behavior without adding domain constants.

## Boundary Ownership

- Generic evaluator filters by branch outcome values as data.
- Workbench/templates decide which branches enforce which receipt purposes.

## Dependency Direction

- No Workbench reference from generic evaluator.
- No template-specific branch constants in generic production code.

## Pattern Decision

- Rule evaluator and diagnostic normalizer.
- Rejected: removing all capability scope receipts without a replacement exposure model.

## Testability Contract

- Tests must pass arbitrary branch names to prove generic filtering.
- Tests must include software-delivery branch names only in template/domain fixtures.

## Partial Class Policy

- Do not hide branch filtering in a new adapter partial.
- Gate behavior must remain in extracted evaluator/services.

## Architecture Proof Required

- Source assertion that branch comparison uses rule metadata.
- Negative test proving missing QA proof without defect evidence is not accepted as repair.
- Diagnostic dedup proof.

## Do Not Do

- Do not route content failures to repair yet; that is SB04.
- Do not weaken `quality-accepted` proof obligations.
- Do not silently ignore required receipts without trace.

## Acceptance Checklist

- Accepted branch enforces validation and browser proof when configured.
- Repair branch skips acceptance-only proof only with concrete defect evidence.
- Missing proof caused by QA omission remains retry/blocker, not repair.
- Duplicate diagnostics for same receipt requirement are gone.
- Skipped rules are visible in trace/evaluation result.

## Proof Required

- `bundle://proof/SB03/manifest.md` after execution.
- `bundle://proof/SB03/semantic-invariants.md` after execution.
- Failing-first and passing branch enforcement transcripts.
- Source assertions and anti-stub audit.
- Production Behavior Artifact Matrix if a new trace record/state is introduced.

## Browser Validation Logging

- N/A for SB03 unit-level proof.

## Progression Gate

- SB04 may start only after branch filtering and dedup behavior is proved for accepted and repair branches.

## Suggested Agent Prompt

Implement SB03 by enforcing structured receipt rules by branch outcome and deduplicating diagnostics. Preserve accepted-branch proof requirements and do not add branch routing yet.
