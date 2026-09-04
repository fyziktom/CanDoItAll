# SB02 — Truthful Completion And Receipts

## Status

- `Completed`

## Objective

Make interactive run status and public tool receipts reflect attempted mutations, including failed correction, recovery, unknown results and legacy data.

## Covered Inputs

- N03, N05, N07; R03, R04, R10; F02 and F04.

## Prerequisites

- SB01 closure gate and invariant proof passed.
- Review both terminal completion branches, governed finalizer, pending-approval and background-continuation behavior.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/MafStreamingTurnExecutor.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Runtime.Abstractions/RuntimeResponseContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`
- `repo://src/App/CanDoItAll.Web/Api/AgentApiResponseContracts.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`

## Deliverables

- One provider-neutral application assessment used by both interactive completion branches.
- Durable and public safe typed receipt outcome, diagnostic and effect evidence with legacy Unknown behavior.
- Explicit recovery association for a pre-execution failure and its validated correction; unrelated success does not erase failure.
- Run-level safe explanation when a mutation is unresolved; preserved approval/cancellation/finalizer behavior.

## Dependency Impact

- Critical foundation for SB03/SB05/SB06. Persistence and public contract changes trigger the once-only frozen broad gate in SB06.

## Validation Depth

- Proof tier: `Behavioral`.
- Test project/filter/expected exact cases: V02 in [validation-plan.md](../../plan/validation-plan.md). Planned new cases are explicitly identified there; no test has been implemented or passed during preparation.
- Selection reason: Real execution/persistence/HTTP fixtures with fake external provider responses. Assert state, receipt and effect agreement.
- Invalidation keys: Terminal states, recovery identity, public/persisted receipt fields, approval/resume or finalizer changes reopen this phase and dependent SB03/SB05/SB06.
- Broad-gate decision: Not required in this phase; shared receipt/persistence contract trigger is consolidated at the final frozen SB06 checkpoint.
- Critical foundation for SB03/SB05/SB06. Persistence and public contract changes trigger the once-only frozen broad gate in SB06.
- Every protected source/test change requires the portability procedure and final enforcement without --write-baseline.

## Implementation Steps

1. Add failing behavior tests for the captured failure-followed-by-prose terminal run and both completion branches.
2. Use trusted declared side-effect metadata and normalized outcomes in a small Core assessment policy. Prefer existing Failed/approval/cancel statuses over new terminal enums.
3. Define recovery precisely: same runtime-associated attempted operation and current authorized scope; corrected arguments are validated anew. If operation/target cannot be safely associated, keep unresolved. Do not trust model-supplied recovery IDs.
4. Persist safe outcome/effect data and expose an allowlisted projection through Web. Missing historical fields are Unknown; no bulk rewrite of historic runs.
5. Keep failed/unknown tool evidence visible even if final assistant prose claims success. Publish a deterministic safe run explanation.
6. Run V02, persistence/HTTP round trips and static enforcement; update downstream contracts.

## C# Architecture Impact

Core owns assessment, Models owns persisted values, Web owns safe HTTP projection; Maf supplies evidence and does not decide domain success. Follow [architecture checkpoints](../../plan/architecture-checkpoints.md).

## Boundary Ownership

Core owns assessment, Models owns persisted values, Web owns safe HTTP projection; Maf supplies evidence and does not decide domain success.

## Dependency Direction

Preserve [the approved project directions](../../architecture/02-csharp-dependency-direction.md). No new project reference is planned; no Core-to-Maf/Workbench/Web or neutral-contract-to-SDK dependency.

## Pattern Decision

Small concrete application policy reused at both call sites; additive bounded data mapping instead of another state machine.

## Testability Contract

Real execution/persistence/HTTP fixtures with fake external provider responses. Assert state, receipt and effect agreement. Expected discovery must match V02; test-created success artifacts cannot substitute for production producer/consumer proof.

## Partial Class Policy

No new partial-file architecture. Touched orchestration partials may delegate to cohesive top-level policies; existing facade roles remain. Document the actual responsibility removed from a hotspot.

## Architecture Proof Required

- Record actual changed types, callers, constructor dependencies and before/after project references.
- Run relevant CodeAnalytics or explicit dependency review, affected builds and the C# architecture gate.
- Reject wrapper-only extraction, service-locator wiring, unused abstractions and untyped context bags.

## UI Composition Contract

N/A — backend contract phase; user-visible status/refresh proof is owned by SB05/SB06.

## Scope Exceptions

- The initial investigation proves the captured direct run only. Shared live behavior is pending SB06.
- User requested preparation only; all implementation and product validation in this specification are future work.

## Do Not Do

- Do not equate every failed optional read with total failure, claim arbitrary semantic task completion, add prose keyword heuristics or force all conversations through a structured finalizer.

## Acceptance Checklist

- A failed asset call followed by a future promise cannot finish Succeeded.
- A confirmed corrected call for the same attempted operation can resolve a known pre-execution failure; a different parent/project/operation cannot.
- Unknown commit state is not Succeeded and is not automatically retried.
- Pending approval, cancellation, background continuation and no-tool conversational answers retain correct semantics.
- HTTP and persisted receipts expose safe typed outcomes without RequestSummary, stack traces, credentials or raw arguments.
- Old receipt JSON loads as Unknown without becoming false success.

## Proof Required

- V02 exact tests, builds of Core/Models/runtime contracts and Web as changed, static enforcement.
- Real HTTP/persistence proof for new and legacy receipts; no snapshot-only DTO assertion.
- Capture run/receipt evidence showing actual failure status even when assistant text says success, plus same-operation recovery and unrelated-success negative cases.
- Record exact commands, expected/actual discovery and exit codes. Zero or unexpected discovery is a failed proof.
- Use [semantic evidence rules](../../plan/validation-plan.md#semantic-evidence) and preserve both positive and adversarial negative results.

## Browser Validation Logging

- N/A — no browser-visible markup change in this phase. SB05/SB06 own browser proof.

## Progression Gate

- Proceed only when both completion paths and durable/public outcomes agree, with legacy compatibility and safe projection demonstrated.
- SB03 receives the canonical safe evidence contract; SB05 receives the effect state contract.

## Reopen Triggers

- Terminal states, recovery identity, public/persisted receipt fields, approval/resume or finalizer changes reopen this phase and dependent SB03/SB05/SB06.

## Suggested Agent Prompt

```text
Execute this subbundle only after the user authorizes implementation. Verify prerequisites and current source. Preserve the outcome, ownership and scope contracts. Capture the required production-path proof, update the execution report, and stop progression if its gate fails. Do not infer implementation permission from this prepared bundle.
```
