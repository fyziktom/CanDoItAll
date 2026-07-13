# SB05 Storage Foundation Architecture Cleanup Gate

## Status

- `Completed — unqualified architecture gate Pass 2026-07-12`

## Objective

- Review, refactor, and validate SB02-SB04 as one trustworthy Storage foundation before any FileTools integration project/reference work.

## Covered Inputs

- N002-N004, N013-N015; R002-R007, R009, R026-R036, R040.

## Prerequisites

- SB02, SB03, and SB04 Completed with trusted proof.

## Exact Source References

- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage`
- `repo://src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `bundle://architecture/00-csharp-current-state-inventory.md`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/02-csharp-dependency-direction.md`
- `bundle://architecture/03-csharp-pattern-selection-records.md`
- `bundle://architecture/04-csharp-testability-plan.md`
- `bundle://plan/architecture-checkpoints.md`

## Deliverables

- Strict C# architecture gate result and exact blocker/repair list.
- Cleanup of duplicated mapping/path/transport/settings logic and weak naming/typing/tests found by review.
- Before/after responsibility, line/member, project-reference, capability, testability, and partial-policy tables.
- Final affected build/tests/format, CodeAnalytics snapshot/dashboard/findings/dependencies/cycles, and dependent fake/outer-adapter smoke.
- Performance gate over accepted large-directory/remote-transport baselines, structural counters, and a fresh scoped anti-pattern scan; O(total-source) page one or unbounded buffering is a blocker.

## Dependency Impact

- SB06-SB18 are blocked until Pass. Any “Pass with follow-up” affecting required boundaries is Fail.

## Validation Depth

- Proof tier: `Standard`.
- Critical architecture progression gate; behavior proof remains owned by SB02-SB04.

## Implementation Steps

1. Run architecture review on actual diffs/source/tests/snapshots.
2. Repair fake separation, duplicated policy, broad types, reference/cycle, service-location, capability/test gaps.
3. Rerun all affected checks, large-source/transport envelopes, performance scan, and downstream contract smoke.
4. Record Pass/Fail and reopen owners if needed.

## C# Architecture Impact

- Review/cleanup only; no new feature scope.

## Boundary Ownership

- Confirms native Storage ownership and FileTools independence.

## Dependency Direction

- No new project/package edges allowed in this gate.

## Pattern Decision

- Validate PSR-01/PSR-02 against implementation; remove cargo-cult abstractions.

## Testability Contract

- Direct tests must exercise extracted drivers/collaborators without Web/full storage orchestration.

## Partial Class Policy

- Zero new partials.

## Architecture Proof Required

- Full Checkpoint A result in `bundle://reviews/csharp-architecture-gate.md`.

## Scope Exceptions

- Does not introduce FileTools packages or UI.

## Do Not Do

- Do not waive required findings as residual risk or refactor unrelated Infrastructure.

## Acceptance Checklist

- [x] Architecture gate Pass.
- [x] No fake separation/duplicate policy/false capability.
- [x] Dependency/cycle and no-FileTools proof pass.
- [x] All affected checks and dependent smoke pass.
- [x] Large-source bounds and remote streaming/connection reuse pass from measurements and structural counters.
- [x] SB06 unlock decision is explicit.

## Proof Required

- Review result, commands/results, snapshot ID, before/after tables, and execution-report gate row.

## Browser Validation Logging

- N/A.

## Progression Gate

- Only an unqualified Pass unlocks SB06.

## Reopen Triggers

- Any later provider/adapter finding contradicting capability, dependency, testability, or confinement reopens the owning SB and SB05; all dependent phases revalidate.

## Suggested Agent Prompt

```text
Act as the strict Storage architecture reviewer. Inspect actual SB02-SB04 code/tests/references, repair only concrete blockers, rerun proof, and issue an evidence-backed Pass or reopen the exact owner. Do not add feature scope.
```
