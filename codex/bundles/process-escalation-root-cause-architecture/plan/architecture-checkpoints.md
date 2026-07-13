# Architecture Checkpoints

## Checkpoint After SB01

- Dependency graph review: no new runtime dependency on modules, MAF, .NET delivery, templates, or UI.
- Partial-class policy review: no new partial file unless temporary with removal note.
- Testability review: blocked diagnostics can be tested without live provider or full dispatch loop.
- Old-class shrink proof: any new diagnostic logic is outside `ProcessRuntimeDispatchApplicationService` unless it is a small call site.
- Next-phase unlock decision: SB02 may start only when blocked diagnostics are persisted and projected.

## Checkpoint After SB02

- Dependency graph review: readiness contracts live in contracts/application boundaries without cycles.
- Partial-class policy review: no new partial class for readiness.
- Testability review: fake tool/MCP/skill catalogs cover missing/denied/suppressed cases.
- Old-class shrink proof: launch/matching code delegates readiness classification instead of embedding checks.
- Next-phase unlock decision: SB03 may start only when readiness diagnostics are visible at launch/dispatch boundaries.

## Checkpoint After SB03

- Dependency graph review: driver recovery strategies depend on abstractions, not application internals.
- Partial-class policy review: no generic dispatcher partial added for domain recovery.
- Testability review: generic and domain fake drivers cover recovery/no-recovery paths.
- Old-class shrink proof: manager fallback decisions move out of large dispatcher branches where feasible.
- Next-phase unlock decision: SB04 may start only when recovery decisions are typed and logged.

## Checkpoint After SB04

- Dependency graph review: .NET delivery behavior remains out of generic runtime/application.
- Partial-class policy review: no new partials hiding .NET behavior in generic adapter.
- Testability review: .NET driver policy is unit-tested with at least two app topics and one non-UI shape.
- Old-class shrink proof: product completion adapter did not gain .NET-specific checks.
- Next-phase unlock decision: SB05 may start only when .NET behavior has a clear isolated home.

## Checkpoint After SB05

- Dependency graph review: template policy extraction does not introduce runtime-template cycles.
- Partial-class policy review: no partials introduced for prompt/template policy.
- Testability review: templates parse and validate required capability/readiness declarations.
- Old-class shrink proof: repeated long prompt rules are reduced or generated from testable policy where appropriate.
- Next-phase unlock decision: SB06 may start only when template fixtures cover UI, non-UI, and management-only paths.

## Checkpoint Before Closure

- Dependency graph review: CodeAnalytics or project-reference audit shows no new cycles.
- Partial-class policy review: no unplanned partial files remain.
- Testability review: characterization, unit, integration, and E2E replay proof is recorded.
- Domain leak review: generic layers are free of .NET/Blazor/Calculator/Tetris/screenshot/Playwright-specific rules.
- Closure decision: if any blocked run lacks typed diagnostics, reopen SB01.
