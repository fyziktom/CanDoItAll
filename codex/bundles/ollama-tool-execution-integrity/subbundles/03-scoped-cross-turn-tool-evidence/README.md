# SB03 — Scoped Cross-Turn Tool Evidence

## Status

- `Completed`

## Objective

Ensure the next turn knows relevant prior tool failures and effects without coupling canonical conversation state to a provider's serialized session.

## Covered Inputs

- N03, N05, N06, N07; R05, R10; F03.

## Prerequisites

- SB01 and SB02 closure gates passed; canonical safe outcome contract settled.
- Review existing transcript ownership, current context authorization and session-runtime compatibility invariants.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/Chat/ChatSessionRuntimeCompatibilityAdapter.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextInvocationFactory.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`

## Deliverables

- A bounded canonical tool-evidence projection used during current turn assembly.
- Current authorization/scope checks for session, agent, owning data profile and attached project/source before any outcome is projected.
- A deterministic budget/order/truncation policy that prioritizes unresolved relevant outcomes and declares omission safely.
- Cross-turn evidence that is independent of the selected provider client and never restores prior approvals or raw provider sessions.

## Dependency Impact

- Critical privacy/context foundation for SB06; SB04 continuation assertions must include the same projection.
- Changing context ownership or projection ordering invalidates provider parity and end-to-end retry proof.

## Validation Depth

- Proof tier: `Governed`.
- Test project/filter/expected exact cases: V03 in [validation-plan.md](../../plan/validation-plan.md). Planned new cases are explicitly identified there; no test has been implemented or passed during preparation.
- Selection reason: Two real canonical turns through production assembly; fake external provider only. Assert both inclusion and isolation.
- Invalidation keys: Changes to receipt schema, current-scope resolution, session serialization, evidence budget/order or provider switching reopen SB03 plus affected SB04/SB06 proofs.
- Broad-gate decision: Not required in this phase; shared receipt/persistence contract trigger is consolidated at the final frozen SB06 checkpoint.
- Critical privacy/context foundation for SB06; SB04 continuation assertions must include the same projection.
- Every protected source/test change requires the portability procedure and final enforcement without --write-baseline.

## Implementation Steps

1. Reproduce the two-turn incident with a canonical previous failure plus contradictory assistant prose; verify current assembly omits failure before the fix.
2. Add a small Core evidence projector over canonical trusted receipt/run data, not provider serialization.
3. Enforce current scope and permission checks before projection. Use typed safe facts, not concatenated raw receipt text or executable instructions.
4. Specify fixed limits for count/text budget and deterministic ordering; include unresolved applicable outcomes and a bounded truncation indication. Record chosen values and tests.
5. Integrate at turn context assembly so both native/shared clients receive equivalent application evidence. Preserve existing runtime session clearing.
6. Run V03 and Governed adversarial scope/replay proofs; recheck SB01 safe data assumptions.

## C# Architecture Impact

Core owns authorized canonical projection; runtime ports carry safe values; Maf turns them into SDK messages. Follow [architecture checkpoints](../../plan/architecture-checkpoints.md).

## Boundary Ownership

Core owns authorized canonical projection; runtime ports carry safe values; Maf turns them into SDK messages.

## Dependency Direction

Preserve [the approved project directions](../../architecture/02-csharp-dependency-direction.md). No new project reference is planned; no Core-to-Maf/Workbench/Web or neutral-contract-to-SDK dependency.

## Pattern Decision

Projection over canonical data, with concrete authorization/budget policy; no parallel history store.

## Testability Contract

Two real canonical turns through production assembly; fake external provider only. Assert both inclusion and isolation. Expected discovery must match V03; test-created success artifacts cannot substitute for production producer/consumer proof.

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

- Do not replay raw serialized SDK sessions, hidden model reasoning, full filesystem content or arbitrary historical records. A previous authorized turn grants no authority in the current turn.

## Acceptance Checklist

- The retry turn receives the prior failed tool name, safe reason and no-commit fact alongside the assistant's prior claim.
- Switching provider endpoint does not remove or expand the evidence's authority.
- Another agent, session, data profile or project cannot receive the prior outcome; current revoked access excludes it.
- Model-authored fake receipts and prior approval tokens never become trusted evidence.
- Limits truncate deterministically and do not accidentally hide the latest unresolved applicable mutation behind successful reads.

## Proof Required

- V03 production context assembly tests and static enforcement.
- Governed INV03 manifest: failing-first/passing output, scope fixture identifiers, redacted captured messages, source/test hashes.
- Negative fixtures for foreign project/session/agent/profile, revoked access, fake receipt and budget flooding; positive two-turn native/shared evidence.
- Record exact commands, expected/actual discovery and exit codes. Zero or unexpected discovery is a failed proof.
- Use [semantic evidence rules](../../plan/validation-plan.md#semantic-evidence) and preserve both positive and adversarial negative results.

## Browser Validation Logging

- N/A — no browser-visible markup change in this phase. SB05/SB06 own browser proof.

## Progression Gate

- Proceed only when the incident's prior failure survives a new turn and adversarial isolation/redaction proofs pass.
- Document the exact canonical source, consumer, limit values and invalidation rules for SB04/SB06.

## Reopen Triggers

- Changes to receipt schema, current-scope resolution, session serialization, evidence budget/order or provider switching reopen SB03 plus affected SB04/SB06 proofs.

## Suggested Agent Prompt

```text
Execute this subbundle only after the user authorizes implementation. Verify prerequisites and current source. Preserve the outcome, ownership and scope contracts. Capture the required production-path proof, update the execution report, and stop progression if its gate fails. Do not infer implementation permission from this prepared bundle.
```
