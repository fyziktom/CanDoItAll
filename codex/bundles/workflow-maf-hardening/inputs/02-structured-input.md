# Structured Input

## Primary problem

The repository has a CanDoItAll workflow domain model, a template pack, managed seeding, and plugin projects. After updating MAF, the system must be hardened so these repository concepts are not merely stored or previewed but are aligned with native MAF workflow semantics: typed messages, executors, edges, builder validation, streaming/non-streaming runs, checkpoint-friendly event boundaries, and human/tool approval patterns.

## Workstream split

1. Baseline and audit the current code.
2. Harden the repository workflow model and template loader before changing runtime execution.
3. Add or verify a native MAF compiler/adapter and typed executor foundation.
4. Harden plugin executor contracts and sandbox/permission behavior.
5. Align runtime events, artifacts, state, checkpoint, retry, and telemetry behavior.
6. Migrate UI/seeded definitions safely after contracts stabilize.
7. Complete tests, documentation, browser proof, and architecture review.

## Non-negotiable boundaries

- User-managed data must not be overwritten.
- Example workflows remain file-backed under `Templates/Workflows`.
- Plugins must not bypass workflow permission, approval, timeout, cancellation, artifact, or telemetry policies.
- Preview/in-process runs must be visibly distinct from durable/production runs.
- Any package upgrade must be deliberate and tested; do not mechanically bump all packages without reviewing API changes.

## Success definition

A developer can define a workflow in repository templates or UI, validate it, compile/adapt it into a native MAF execution graph, execute it in preview or durable mode, use plugin executors safely, observe typed events and artifacts, recover from failures where policy allows, and prove the behavior with deterministic tests.
