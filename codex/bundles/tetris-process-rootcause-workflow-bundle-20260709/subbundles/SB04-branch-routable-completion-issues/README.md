# SB04 Branch-Routable Completion Issues

## Status

- `Completed`

## Objective

Add generic completion issue routing so deterministic acceptance-branch failures can route to configured repair branches instead of manager escalation or same-step retry.

## Covered Inputs

- GPTPro RC2, RC4, and RC9.
- Requirement R03 and R04.
- Task 05 from GPTPro.

## Prerequisites

- SB03 branch-aware receipt enforcement is complete.
- SB00 incident route tests exist and fail before this behavior change.
- Template route metadata shape is agreed.

## Exact Source References

- `bundle://04-target-architecture.md`
- `bundle://codex-tasks/05-branch-routable-completion-issues.md`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessExecutionResultConverter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessProductCompletionPathGate.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessManagedArtifactService.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`

## Deliverables

- `ProcessCompletionIssueRouteKind` or equivalent route kind.
- `ProcessCompletionIssueRouter` that consumes metadata, not hardcoded branch names.
- Completion gate result can return branch route with target branch outcome and evidence refs to add.
- Adapter creates succeeded result with branch signal for branch-routed completion issue.
- Runtime gate findings artifact or appended section is persisted and readable by downstream repair.
- Retry budget is not consumed for branch-routable deterministic defects.

## Dependency Impact

- SB07 template migration depends on route metadata contract.
- SB10 observability depends on route trace facts.
- SB11 final process smoke depends on runtime gate findings.

## Validation Depth

- Critical foundation.
- Requires failing-first and passing proof for exact Tetris incident behavior.

## Implementation Steps

1. Add generic route metadata contract.
2. Extend completion issues/evaluation with route decision fields without domain literals.
3. Add router that maps issue code plus current branch outcome to route kind/target branch from template metadata.
4. Update result conversion so branch-routed completion issues create branch signals before manager conversion.
5. Persist runtime gate findings with safe product-relative refs and current execution run id.
6. Ensure missing route metadata preserves legacy manager/retry behavior.
7. Update incident tests to pass.

## C# Architecture Impact

This is the main runtime behavior change and must remain data-driven.

## Boundary Ownership

- Generic router owns route mechanics.
- Templates own route table values.
- Workbench owns domain-specific route metadata emission where launch variables generate it.

## Dependency Direction

- Router cannot depend on Workbench or template file paths.
- Adapter may consume generic evaluation result and write managed artifacts.

## Pattern Decision

- Strategy/router service.
- Rejected: `switch` or `if` on `qa-validation`/`repair-required`.

## Testability Contract

- Unit tests must use arbitrary branch names to prove generic routing.
- Incident fixture can use software-delivery branch names as data.

## Partial Class Policy

- Do not add permanent adapter partial for routing logic.
- Adapter code should delegate to router/evaluation result.

## Architecture Proof Required

- Source assertion: no hardcoded software-delivery branch names in generic router.
- Source assertion: runtime gate findings producer and downstream consumer path exists.
- Negative proof: same issue without route metadata remains legacy behavior.

## Do Not Do

- Do not treat every product content failure as repair branch without metadata.
- Do not consume retry budget for branch-routed issues.
- Do not hide gate findings only in logs.

## Acceptance Checklist

- `quality-accepted` plus scaffold/content failure routes repair branch when metadata exists.
- `repair-required` plus deterministic defect succeeds as branch signal without acceptance-only browser proof.
- Same issue without route metadata remains manager/retry.
- Runtime gate findings are cited in evidence refs.
- Retry budget test passes.

## Proof Required

- `bundle://proof/SB04/manifest.md` after execution.
- `bundle://proof/SB04/semantic-invariants.md` after execution.
- Failing-first and passing incident regression transcripts.
- Runtime gate findings source assertion.
- Production Behavior Artifact Matrix for the new runtime gate findings record/event.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A for unit-level branch routing proof. Real browser process smoke is SB11.

## Progression Gate

- SB07 and SB10 may start only after route metadata, branch signal emission, runtime gate findings, and retry-budget proof pass.

## Suggested Agent Prompt

Implement SB04 by adding generic metadata-driven completion issue routing. Prove the Tetris accepted-branch defect routes repair without retry budget consumption and without generic domain hardcodes.
