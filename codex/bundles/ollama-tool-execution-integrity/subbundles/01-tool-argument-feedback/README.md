# SB01 — Safe Tool Argument Feedback

## Status

- `Completed`

## Objective

Make malformed tool calls correctable without executing them or exposing protected exception details, and replace implicit mutation success with typed evidence.

## Covered Inputs

- N03, N05, N07; R01, R02, R10; F01 and F05.

## Prerequisites

- SB00 MAF 1.20 upgrade and characterization gate passed; generated schema/result assumptions refreshed.
- Prepared bundle, captured tool schema and SDK probes reviewed.
- Read engineering/testing/CI guidance and current IAgentToolFailure/middleware contracts.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentToolFailureMapper.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeToolInvocationResultClassifier.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolFailureContract.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Runtime.Abstractions/RuntimeResponseContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentContracts.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolInvocationResultTests.cs`

## Deliverables

- A cohesive MAF invocation adapter using the actual AIFunction/schema contract, with safe field-path/error-code feedback and explicit invocation status.
- Asset tool description and schema use exact projectId/request paths and a concise nested example for smaller models; descriptions never replace runtime validation.
- Typed success/failure/unknown and effect-state values in current neutral contract owners, reusing existing side-effect metadata.
- Explicit adapters for supported typed domain/workspace/MCP results; unknown mutation evidence cannot become success.
- Captured invalid and corrected payload fixtures without secret data, plus production-path tests.

## Dependency Impact

- Critical foundation for SB02/SB03/SB04/SB05/SB06. Error shape, outcome state, call correlation or authorization changes invalidate downstream evidence.

## Validation Depth

- Proof tier: `Governed`.
- Test project/filter/expected exact cases: V01 in [validation-plan.md](../../plan/validation-plan.md). Planned new cases are explicitly identified there; no test has been implemented or passed during preparation.
- Selection reason: Real AIFunction and middleware with counting domain delegate and captured feedback; test the public runtime caller rather than only a helper.
- Invalidation keys: Changes to schema serialization, SDK version, authorization ordering, outcome contract, supported result adapters or correlation reopen this phase and all downstream tests.
- Broad-gate decision: Not required in this phase; shared receipt/persistence contract trigger is consolidated at the final frozen SB06 checkpoint.
- Critical foundation for SB02/SB03/SB04/SB05/SB06. Error shape, outcome state, call correlation or authorization changes invalidate downstream evidence.
- Every protected source/test change requires the portability procedure and final enforcement without --write-baseline.

## Implementation Steps

1. Lock the captured project_id/wrong-root payload and corrected nested request in a failing-first test through the real MAF middleware.
2. Choose neutral enum/record additions and expose only safe known validation codes/paths. Bound diagnostic size; never include raw input values or exception dumps.
3. Extract touched invocation adaptation from the large factory, keeping existing authorization/approval/isolation ordering intact.
4. Normalize supported success/error contracts explicitly; preserve valid nonmutating read behavior and record Unknown for ambiguous mutations.
5. Return a correlated tool result the model can read while the trace remains failed. Preserve cancellation semantics and call identity.
6. Run V01 and required static enforcement; produce Governed invariants and source/test evidence before SB02.

## C# Architecture Impact

MAF owns SDK validation/result adaptation; Models/Runtime.Abstractions own neutral values. Core consumes the values later. Follow [architecture checkpoints](../../plan/architecture-checkpoints.md).

## Boundary Ownership

MAF owns SDK validation/result adaptation; Models/Runtime.Abstractions own neutral values. Core consumes the values later.

## Dependency Direction

Preserve [the approved project directions](../../architecture/02-csharp-dependency-direction.md). No new project reference is planned; no Core-to-Maf/Workbench/Web or neutral-contract-to-SDK dependency.

## Pattern Decision

Adapter plus concrete normalization policy; no new generic manager/interface hierarchy.

## Testability Contract

Real AIFunction and middleware with counting domain delegate and captured feedback; test the public runtime caller rather than only a helper. Expected discovery must match V01; test-created success artifacts cannot substitute for production producer/consumer proof.

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

- No catalog-wide parameter flattening, silent aliasing, global IncludeDetailedErrors, SDK upgrade, or automatic retry of unknown side effects.

## Acceptance Checklist

- Captured malformed call executes the tool zero times and explains missing projectId/request.
- Corrected nested call reaches the same authorized delegate exactly once with parentNodeKey and sourceWorkspacePath intact.
- Domain-safe errors stay useful; secrets/stack traces/raw argument content never enter model feedback, public receipts or portable proof.
- Generic SDK error text and unknown shapes cannot certify a mutation; supported reads remain compatible.
- Authorization and pending approvals cannot be bypassed by a correction path.

## Proof Required

- V01 discovery/execution plus existing invocation-result regressions, build of each changed owner, final portability enforcement.
- Governed manifest with failing-first/passing transcripts and source/test hashes; invariants INV01 (safe pre-execution feedback) and INV02 (trusted outcomes only).
- Negative fixtures: secret-bearing exception/input, invalid enum/type, spoofed receipt and unknown result; positive corrected payload uses real AIFunction binding.
- Record exact commands, expected/actual discovery and exit codes. Zero or unexpected discovery is a failed proof.
- Use [semantic evidence rules](../../plan/validation-plan.md#semantic-evidence) and preserve both positive and adversarial negative results.

## Browser Validation Logging

- N/A — no browser-visible markup change in this phase. SB05/SB06 own browser proof.

## Progression Gate

- Proceed only when safe diagnostics, zero-call rejection, corrected one-call execution and trusted outcome normalization are proven at the production boundary.
- Record architecture checkpoint and downstream contracts; no downstream implementation based on a disconnected helper test.

## Reopen Triggers

- Changes to schema serialization, SDK version, authorization ordering, outcome contract, supported result adapters or correlation reopen this phase and all downstream tests.

## Suggested Agent Prompt

```text
Execute this subbundle only after the user authorizes implementation. Verify prerequisites and current source. Preserve the outcome, ownership and scope contracts. Capture the required production-path proof, update the execution report, and stop progression if its gate fails. Do not infer implementation permission from this prepared bundle.
```
