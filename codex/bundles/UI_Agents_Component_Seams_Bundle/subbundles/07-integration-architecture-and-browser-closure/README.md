# SB07 — Integration, architecture, and browser closure

**Status:** Blocked by SB06  
**Outcome:** The complete Agents seam is independently reviewed and proven in focused,
broad, portability, and real-host contexts without expanding feature scope.

## Owned requirements

R-001–R-057 final closure.

## Prerequisites and reopen triggers

All earlier checkpoints accepted. Any failure routes back to the earliest owning
subbundle; do not patch closure-only work around an invalid seam.

## Work

1. Inspect final diff against refreshed baseline and verify only permitted paths.
2. Run final production build, route tests, new seam unit tests, primary component tests,
   rewritten Workflows case, and test-hygiene checks with exact discovery.
3. Run DI/composition smoke and confirm all three seams resolve in Web/test composition.
4. Run the broad stable gate once.
5. Run portability-static, review/refresh any legitimate baseline delta, and finish with
   no-write enforcement.
6. Run the large-desktop real-host smoke and capture five named scenarios/screenshots.
7. Run independent C# architecture review; record CodeAnalytics cycle/dependency evidence
   when available.
8. Complete requirement closure and execution report.
9. Record durable documentation candidates for later pre-merge documentation/SharedInfo
   work; do not convert this temporary bundle into maintained product documentation now.

## C# Architecture Impact

Review only. No new feature abstraction is permitted in closure.

## Boundary Ownership

Confirm final ownership matches architecture docs and no hidden duplicate remains.

## Dependency Direction

Confirm no new project reference/cycle and target Razor forbidden dependencies are absent.

## Pattern Decision

Approve PSR-01–PSR-06 or reopen the owning phase.

## Testability Contract

All focused and broad evidence must be current and attributable to final source SHA.

## Partial Class Policy

No new target partial and no source-shape test.

## Architecture Proof Required

- governed final evidence manifest;
- completed `reviews/csharp-architecture-gate.md`;
- exact command/discovery/results;
- browser screenshots/console report;
- readiness and residual-coupling assessment;
- final no-deviation or approved-deviation record.

## Non-goals

No opportunistic feature fixes, route/project/sandbox work, test-suite expansion, remote
write, or documentation migration.

## Closure gate

Use `reviews/final-closure-checklist.md`; every blocking item must be resolved or the
bundle remains open.
