# process-core-readiness-final-isolation-drivers-prep-v1

Prepared: 2026-06-06

## Purpose

This bundle continues the `maf-processes-refactor` branch toward future Process Core and process helper-driver readiness without creating Process Core yet.

Unlike the earlier micro-subbundle style, this bundle has **27 larger subbundles across 9 phases**. Each subbundle owns a meaningful multi-file isolation slice. Codex should be able to work for several hours without collapsing the work into a small cosmetic pass.

## Scope

- Route service adapter burn-down.
- Candidate hydration decomposition.
- Pre-execution/materialization/start-transition boundary.
- Subprocess runtime and subprocess projection isolation.
- Finalizer/failure closure model boundary.
- Static wrapper/rule burn-down.
- Route/projection/finalizer model readiness.
- Final Core/driver readiness decision.

## Hard Constraints

- Do **not** create `CanDoItAll.Processes.Core`.
- Do **not** introduce production process-driver APIs.
- Do **not** remove existing functionality.
- Do **not** touch UI unless an unexpected compile fix requires it; if so, stop and document before proceeding.
- Do **not** create small/medium/mobile proof artifacts.
- Keep driver work documentation-only.

## Completion Definition

The bundle is complete only when:
1. SB001-SB027 are closed with individual execution-report rows.
2. Critical gates SB003/SB006/SB009/SB012/SB015/SB018/SB021/SB024/SB027 have manifests and semantic invariants.
3. Build and focused tests pass.
4. Source scans prove no Core, no production driver API, no UI/mobile proof drift, no stubs, no route-order drift.
5. The final Core/driver readiness matrix gives a clear next-bundle recommendation.
