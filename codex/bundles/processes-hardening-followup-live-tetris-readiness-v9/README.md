# processes-hardening-followup-live-tetris-readiness-v9

## Status

Prepared for Codex execution.

## Branch context

- Repository: `fyziktom/CanDoItAll`
- Reviewed branch visible through GitHub connector: `processes-hardening`
- User branch name: `process-hardening`
- Reviewed head: `phase8` / `4bd0e822a4bef0c0b37187f9810f7e5eb3226061`
- PostgreSQL-only runtime requirement remains active.

## Purpose

This bundle is the last hardening pass before a real UI-driven test where the Processes module should orchestrate creation of a simple **Blazor WebAssembly PWA Tetris** application.

The goal is not to hardcode Tetris into the process runtime. The runtime must stay generic. Tetris belongs in a template scenario, live-run profile, acceptance criteria, skills, and UI test harness.

## Summary of phase8 verification

Phase8 made real improvements:

- `ProcessStepRecoveryOption.None` exists, so the earlier read-model compile concern appears resolved.
- `project_structure_*` tools are now registered and classified, and project-structure mutation requires `ExecuteExternalAction`.
- The process API skill is much richer and documents governance fields.
- Blazor revalidation and writeback steps were corrected away from product mutation.
- A `baseline-blazor-wasm-pwa-tetris` scenario now exists.

## Remaining concern

The Tetris scenario in `baseline-scenarios.json` includes pre-authored transitions and artifacts. That is good for regression, but a real UI test must not use a pre-completed seeded run as proof that agents can execute the process. This bundle therefore asks Codex to create a clear distinction:

- **Baseline scenario**: regression/demo data with transitions/artifacts.
- **Live-run profile**: starts the process with acceptance criteria and assignments, but no pre-completed transitions/artifacts.

After this bundle, the next activity should be a real UI run using the live profile.
