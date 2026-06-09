# Process Runtime Restoration Ledger

## Status

As of 2026-06-09, the process-owned runtime restoration release candidate is source-backed for current UI, API, project-structure, scheduler, workflow-origin, operator readback, and deterministic execution paths.

## Validated Runtime Paths

- Global process workspace start and launch-plan execution through `ProcessesService`.
- Project-scoped process workspace launch with preserved project context.
- Project-structure process node start through the project-structure bridge.
- Scheduler and workflow-origin starts through `ProcessesService.StartRunFromTriggerAsync`.
- Durable outbox dispatch, claim, finalization, artifact projection, and recovery.
- Workflow-backed and direct-agent process execution using process-owned finalizers.
- Deterministic software and business-analysis process scenarios.
- Run detail recovery UI and API readback.
- Manager-visible read-only diagnostics with no process mutation.
- Failure taxonomy, operator health, invariant diagnostics, escalation, outbox, and attempt timeline readback.

## Release-Candidate Proof

- Solution build passed with 0 warnings and 0 errors.
- Full unit tests passed with 1,134 tests.
- Focused process integration matrix passed with 199 tests.
- Focused large-desktop Playwright matrix passed with 3 tests at 1900x1200 and 11 screenshot artifacts.
- Source scans found no transient bundle paths and no process driver runtime host, registry, selector, manager command, endpoint mapping, scheduler hook, workflow hook, or driver mutation surface.

## Current Migration Position

Use process-owned services, HTTP APIs, project-structure bridge routes, and typed read-only verification adapters. Do not migrate runtime launch, dispatch, finalization, recovery, manager operations, scheduler starts, workflow starts, or operator actions to a process-driver runtime host.

Read-only verification migration may use `ProcessReadOnlyVerificationBatchOrchestrator` with caller-supplied facts. It must stay diagnostic-only and must not mutate transitions, claims, finalizers, retries, artifacts, workspace files, storage, or external systems.

## Open Blockers

- Generic process-driver runtime host is not approved.
- Driver registry, runtime selector, dependency-injection registration, manager command, scheduler hook, workflow hook, and endpoint mapping are blocked.
- Execution-capable drivers are blocked until runtime ownership, cancellation, retry ownership, failure handoff, observability, audit persistence, sandbox policy, allow-list policy, authorization, approval, revocation, emergency stop, dry-run behavior, compatibility, versioning, tests, source scans, and red-team proof are approved together.
- Live OpenAI smoke remains opt-in. Deterministic fake-provider and process-runtime tests must not be reported as live OpenAI proof.

## Reopen Triggers

Reopen runtime restoration before release if any of these occur:

- A new start path bypasses `ProcessesService` or the project-structure bridge.
- A driver package gains process mutation, runtime hosting, external I/O, storage/workspace writes, or DI registration.
- Run health, block reason, recovery options, invariant diagnostics, outbox health, or attempt timeline disappears from operator readback.
- Playwright release smoke fails for global process start, run-detail recovery, or project-structure output navigation.
- Source scans find active bundle-path references or forbidden runtime-host terms in production source.
