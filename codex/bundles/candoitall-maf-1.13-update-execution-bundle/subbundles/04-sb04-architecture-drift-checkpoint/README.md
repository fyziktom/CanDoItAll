# SB04 Architecture Drift Checkpoint

## Status

Completed.

## Objective

Review the package-update diff before broader validation and reject architecture drift, product-scope expansion, or weakened governance.

## Covered Inputs

- `bundle://architecture/00-csharp-current-state-inventory.md`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/02-csharp-dependency-direction.md`
- `bundle://architecture/03-csharp-pattern-selection-records.md`
- `bundle://reviews/csharp-architecture-gate.md`

## Prerequisites

- `SB03` build proof or blocker exists.
- The package-update diff is available.
- Any new helper/adapter/test changes are complete enough to review.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter`
- `repo://src/Processes`
- `repo://src/Modules/CanDoItAll.Modules.Processes`
- `repo://src/App/CanDoItAll.Web/Api/ProcessesApi.cs`
- `bundle://reviews/csharp-architecture-gate.md`

## Deliverables

- Diff stat and bounded-change review.
- Source scan for forbidden process/provider/API changes.
- Dependency direction review.
- Partial-class policy review.
- Testability review for any new helper.
- Downstream unlock decision.

## Dependency Impact

- `SB05` cannot start until this checkpoint passes.
- If this checkpoint fails, implementation returns to `SB03` or repairs the bundle.

## Validation Depth

- Source scan proof.
- Diff review proof.
- CodeAnalytics/dependency proof if references changed.
- Semantic Adequacy Gate required because this is a critical foundation.

## Implementation Steps

1. Run `git diff --stat`.
2. Review changed files under MAF and workflow adapter projects.
3. Run source scans for process direct tool provider and route expansion.
4. Run source scans for stale stable MAF 1.8 references.
5. Run `git diff --check`.
6. If project references changed, run CodeAnalytics dependency proof.
7. Update `reviews/csharp-architecture-gate.md`.

## Scope Exceptions

- Historical docs/tests may mention `processes_*`; new production registration must not.
- Existing large files are not fixed here unless implementation made them worse.

## Do Not Do

- Do not accept broad warning suppression.
- Do not accept new process APIs.
- Do not accept new direct process runtime tool provider.
- Do not accept new central package management.
- Do not accept new final runtime partial classes.

## Acceptance Checklist

- Diff is package-update-sized and reviewable.
- No forbidden process/provider/API changes.
- No new dependency cycle or wrong reference direction.
- No broad feature adoption.
- Testability plan updated for new helpers.
- Downstream unlock decision is explicit.

## Proof Required

- `proof/SB04/manifest.md`.
- `proof/SB04/semantic-invariants.md`.
- Diff stat transcript.
- Source scan transcripts.
- `git diff --check` transcript.
- CodeAnalytics dependency transcript if references changed.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A for architecture review unless source changes touched UI-visible routes or components; if so, add browser proof requirements to `SB05`.

## Progression Gate

- `SB05` may start. `reviews/csharp-architecture-gate.md` records pass and proof exists under `bundle://proof/SB04/`.

## C# Architecture Impact

- Dedicated architecture gate for the bundle.

## Boundary Ownership

- Confirms all changed files remain in appropriate owners.

## Dependency Direction

- Confirms no inner project depends on MAF implementation or process module implementation.

## Pattern Decision

- Confirms any new pattern is justified and recorded.

## Testability Contract

- Confirms tests can target extracted behavior directly if extraction happened.

## Partial Class Policy

- Rejects new final partial split.

## Architecture Proof Required

- Diff review.
- Dependency review.
- Source scans.
- Testability review.

## Suggested Agent Prompt

Execute `SB04` only. Review the diff as a senior C# architect. Run source scans and dependency checks. Update the C# architecture gate and decide whether `SB05` can start.
