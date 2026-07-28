# 01 Current-State Baseline and Architecture Contracts

## Status

- `Completed`

## Objective

- Lock a reproducible current-state architecture and latency baseline, characterize existing defects, and finalize implementation contracts before product code changes.

## Success Criteria

- Targeted CodeAnalytics/dependency snapshot is captured with no blocking load error.
- Cold/warm no-provider-call baseline records startup stages and store/query operation counts.
- Existing UI/event/cancellation/profile-relay behavior has failing-first characterization tests or explicit test plans.
- Architecture gate resolves type placement, DI lifetime, stream identity, preparation ownership, and snapshot invariants.

## Covered Inputs

- R09 baseline half and R11 architecture preservation.
- Deep exploration, root causes, threading/source-of-truth/bottleneck risks.
- Backend-first phase boundary.

## Prerequisites

- Prepared bundle validator and preparation architecture gate pass.
- Clean or fully inventoried worktree.

## Exact Source References

- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core\Execution`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Persistence\Storage`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceShell.razor`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs`

## UI Composition Contract

- N/A; this subbundle makes no browser-visible change.

## Deliverables

- Baseline instrumentation/tests and artifacts.
- Constructor/producer/consumer/query inventory.
- Final architecture documents and A1 gate record.

## Dependency Impact

- All later work depends on the baseline and exact ownership. Missing a manual workspace construction or startup read would invalidate stream injection and performance claims.

## Validation Depth

- Proof tier: `Governed`.
- Critical foundation for agent execution Core/Module/Persistence/MAF and both Blazor consumers.

## Implementation Steps

1. Capture targeted architecture/dependency and constructor inventories.
2. Add failing-first characterization and timing/operation-count probes without changing production behavior.
3. Run cold/warm/new/existing-session baseline matrix with fake runtime.
4. Finalize stream/project/DI placement and update architecture gate.
5. Capture governed proof and A1 progression decision.

## Scope Exceptions

- No optimization, UI work, SSE endpoint, or real provider call.

## Do Not Do

- Do not change production execution semantics.
- Do not claim improvement from generic local UI text.
- Do not run Terra.

## Acceptance Checklist

- [x] Baseline is reproducible and separates cold/warm paths.
- [x] Operation counts expose duplicate catalog/session/run-detail work.
- [x] All production constructors and DI lifetimes are inventoried.
- [x] Architecture gate is `Pass`.

## Proof Required

- `proof/SB01/manifest.md`
- failing/passing characterization transcripts, baseline metrics, architecture snapshot, constructor/query inventory, semantic invariants, and anti-stub audit.

## Browser Validation Logging

- N/A.

## Progression Gate

- A1 passes: baseline, constructor inventory, failing-first behaviors, and final project/DI placement are complete.

## Reopen Triggers

- A missed production construction path, unmeasured startup stage, incorrect persistence domain, or non-reproducible baseline reopens SB01 and all performance comparisons.

## C# Architecture Contract

- No product dependency change in this phase.
- Metrics/probes must be testable and bounded.
- Baseline instrumentation cannot add paid calls or alter canonical persistence ordering.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Preserve behavior, capture the no-cost baseline and architecture inventory, update governed proof, and stop if A1 cannot honestly pass.
```
