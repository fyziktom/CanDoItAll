# Structured Input

## User Objective
Review the completed driver-contract API / verification alpha boundary work on `maf-processes-refactor`, identify repairs/improvements, and prepare a next implementation-ready bundle that moves toward a stable Process Core with domain drivers.

## Normalized Direction
The next work may move beyond docs-only prerequisites. It may create a first verification-only alpha driver implementation only if all hard boundaries remain enforced.

## Hard Constraints
- Preserve existing runtime behavior.
- Keep Process Core deterministic and dependency-clean.
- No broad Core runtime extraction.
- No runtime driver registry, selector, dependency-injection registration, manager command, workflow executor hook, or process mutation.
- First alpha driver must be verification-only and read existing evidence/transcripts only.
- No shell execution, package restore, Office/Graph calls, workspace/storage writes, claim/transition/finalizer/retry mutation.
- No UI/browser/mobile/small/medium proof unless UI files are unexpectedly changed, which should fail the bundle rather than broaden proof scope.
