# 03 Revisioned Runtime Preparation Snapshots

## Status

- `Completed`
- Gate: `A3 Pass with follow-up`

## Objective

- Replace misleading metadata-only preparation with defensively immutable, revisioned, single-flight execution blueprints while keeping all live/secret/request-specific runtime state per execution.

## Success Criteria

- Blueprint acquisition reuses matching warm data in a bounded per-agent map and explicitly refreshes/rejects stale data.
- Canonical catalog data revision, database-profile generation, and provider fingerprint invalidate in-flight and current per-agent snapshots.
- Shared loads have service-owned cancellation; each waiter can cancel independently.
- No mutable list, secret, client, `DbContext`, live tool/agent/session, authorization result, approval, or context-contributor output is retained.
- Duplicate catalog/session reads are removed or passed through a prepared startup aggregate.
- The startup aggregate is transient per invocation: cached blueprint plus current session/context; use-time revision validation precedes capability/policy materialization.

## Covered Inputs

- R07, preparation part of R08, prepared-DI intent, no string-key cache, snapshot lifetime policy.

## Prerequisites

- SB02 A2 gate passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Services\AgentChatPreparationPool.cs`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core\ReferenceData`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core\Execution`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Maf\Runtime`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Providers`

## UI Composition Contract

- N/A; backend preparation only.

## Deliverables

- Immutable blueprint contracts/service, revision source, invalidation and DI.
- Reference-data defensive copies and cancellation repair.
- Startup aggregate passed through run creation to eliminate duplicate reads.
- Proof that session/context and current authorization never enter the cached blueprint map.
- Preparation activity and correlated timing measurements.

## Dependency Impact

- Module adapters and performance proof depend on correct invalidation. Retaining mutable/live data creates cross-run security and correctness risk.

## Validation Depth

- Proof tier: `Governed`.
- Critical foundation for Agent Framework reference data, execution, MAF construction, and DI.

## Implementation Steps

1. Write failing immutability, invalidation, first/later-waiter cancellation, and resource-exclusion tests.
2. Add one store-owned catalog data revision advanced by every relevant catalog mutation; define profile generation/provider fingerprint and blueprint contracts.
3. Build service-owned single-flight loading and atomic generation commit.
4. Build a transient per-invocation startup aggregate from blueprint plus current session/context and remove duplicate reads.
5. Revalidate catalog revision/profile generation/provider fingerprint immediately before capability/policy materialization; preserve per-operation configuration snapshot isolation while current security/tool enforcement remains live.
6. Overlap only independent provider/session reads with separate safe dependencies and explicit failure ordering.
7. Capture cold/warm timing, resource audit, architecture snapshot, and A3 decision.

## Scope Exceptions

- Skill file parsing/tool creation/runtime session restore remain per run unless measurement supports a separate immutable descriptor in scope.
- Cross-host atomicity between the final provider-revision probe and external provider
  use requires a distributed lease/transaction and is not claimed by this subbundle.
- Physical WAL/directory durability is owned by SB05 and is not a preparation-snapshot
  guarantee.

## Do Not Do

- Do not pool live MAF agents, provider clients, credentials, tools, MCP sessions, approvals, or DbContexts.
- Do not silently serve expired/stale data.
- Do not use string cache keys or `Task.WhenAll` over unsafe shared state.

## Acceptance Checklist

- [x] Collections cannot be cast and mutated.
- [x] Old in-flight result cannot overwrite a newer revision.
- [x] One waiter cancellation does not poison others.
- [x] Warm acquisition reduces measured reads/work.
- [x] Per-run resources are created/disposed per run.
- [x] Session/context/current authorization never enter the cached map and stale use-time identity rejects/reprepares.
- [x] Preparation phases identify reused versus refreshed work truthfully.

## Proof Required

- `proof/SB03/manifest.md`, `proof/SB03/a3-decision.md`, preserved baseline/red
  contracts, passing tests, lifecycle/resource matrix, cold/warm operation evidence,
  architecture snapshot, semantic/adversarial invariants, and anti-stub audit.

## Browser Validation Logging

- N/A.

## Progression Gate

- A3 passed with two explicit A3 P2 follow-ups: synchronous database-switch
  notification can be delayed by a blocked subscriber, and final provider validation
  cannot be globally atomic without a distributed boundary. Immutable bounded
  blueprints, invalidation/cancellation/resource lifecycle, and truthful reuse are
  proven. A5 subsequently returned `GO with three P2 follow-ups`, and final
  CodeAnalytics snapshot `snap-20260728014834-63e19a8b` kept the affected project
  graph acyclic.

## Reopen Triggers

- Any stale answer, retained live/secret state, duplicate cold-path regression, shared cancellation leak, or unbalanced disposal reopens SB03-SB07.

## C# Architecture Contract

- Preparation is a read-only scoped application service with a lifetime CTS, not canonical storage.
- Blueprint identity is typed and based on profile/workspace/agent, catalog data revision, profile generation, and provider fingerprint.
- Atomic replacement occurs only after full immutable construction.
- Runtime materialization remains in MAF per execution.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Implement immutable revisioned preparation only, prove resource/cancellation safety and real reuse, update governed proof, and stop if A3 cannot pass.
```
