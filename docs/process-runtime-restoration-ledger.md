# Process Runtime Restoration Ledger

## Status

As of 2026-06-10, the process-owned runtime restoration release candidate is source-backed for current UI, API, project-structure, scheduler, workflow-origin, operator readback, verification audit readback, and deterministic execution paths.

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
- Verification-host beta operator readback through `ProcessManagerReadOnlyVerificationReadbackDto`, including denial category, denial code, audit records, observation hashes, and mutation-denial flags.
- Failure taxonomy, operator health, invariant diagnostics, escalation, outbox, and attempt timeline readback.

## Release-Candidate Proof

- Solution build passed with 0 warnings and 0 errors.
- Full unit tests passed with 1,136 tests.
- Focused verification host/readback/security integration matrix passed with 34 tests.
- Focused operator API smoke passed 2 tests for manager diagnostics readback and process-run detail verification audit readback.
- The existing focused large-desktop Playwright matrix remains the UI proof; the verification-host operator smoke used the API-proof path because no UI route changed.
- Source scans found no transient bundle paths and no process driver runtime host, registry, selector, manager command, endpoint mapping, scheduler hook, workflow hook, or driver mutation surface.

## Current Migration Position

Use process-owned services, HTTP APIs, project-structure bridge routes, and typed read-only verification adapters. Do not migrate runtime launch, dispatch, finalization, recovery, manager operations, scheduler starts, workflow starts, or operator actions to a process-driver runtime host.

Read-only verification migration may use `ProcessReadOnlyVerificationBatchOrchestrator` with caller-supplied facts. It must stay diagnostic-only and must not mutate transitions, claims, finalizers, retries, artifacts, workspace files, storage, or external systems.

Manager/operator projection may use `IProcessManagerReadOnlyVerificationFacade.VerifyForReadbackAsync` to expose diagnostics, `auditRecords`, `observationHash`, `denialCategory`, `denialCode`, and mutation-denial flags. That projection is read-only troubleshooting evidence, not runtime-host approval.

## Open Blockers

- Generic process-driver runtime host is not approved.
- Driver registry, runtime selector, dependency-injection registration, manager command, scheduler hook, workflow hook, and endpoint mapping are blocked.
- Execution-capable drivers are blocked until runtime ownership, cancellation, retry ownership, failure handoff, observability, audit persistence, sandbox policy, allow-list policy, authorization, approval, revocation, emergency stop, dry-run behavior, compatibility, versioning, tests, source scans, and red-team proof are approved together.
- Live OpenAI smoke remains opt-in. Deterministic fake-provider and process-runtime tests must not be reported as live OpenAI proof.

## Execution-Capable Future Gate Guards

Guard status: every execution-capable driver prerequisite is `Not satisfied`.

| Prerequisite | Status | Proof required before approval |
| --- | --- | --- |
| Runtime lifecycle ownership | `Not satisfied` | Source-backed owner for startup, shutdown, cancellation, retry ownership, failure handoff, and observability. |
| Audit persistence | `Not satisfied` | Immutable request, denial, approval, output hash, redaction descriptor, caller, lane, and timestamp records. |
| Sandbox and allow-list policy | `Not satisfied` | Tests proving unknown commands, connectors, paths, lanes, and operations fail predictably before side effects. |
| Authorization and approval | `Not satisfied` | Recorded enablement, revocation, dry-run behavior, emergency stop, and audited approval decisions. |
| Command, network, and storage policy | `Not satisfied` | Explicit allow-list for shell, package restore, file access, HTTP, Office/Graph, CRM, provider repair, workspace writes, and storage writes. |
| Compatibility governance | `Not satisfied` | `ProcessDriverContractVersion.Current`, public API snapshots, migration docs, and source guards updated in the same approval bundle. |
| Red-team negative proof | `Not satisfied` | Tests rejecting report-only approval, non-empty diagnostics as approval, implicit DI registration, fallback runtime selection, fixture-only success, and undocumented manager/scheduler/workflow entry points. |

| Premature surface | Status |
| --- | --- |
| Runtime host | `Blocked` |
| Driver registry | `Blocked` |
| Runtime selector | `Blocked` |
| Dependency-injection registration | `Blocked` |
| Manager command | `Blocked` |
| Scheduler hook | `Blocked` |
| Workflow hook | `Blocked` |
| Endpoint mapping | `Blocked` |
| Workspace or storage write | `Blocked` |
| External command, network, Office/Graph, or CRM call | `Blocked` |
| Transition, claim, finalizer, retry, or process mutation | `Blocked` |
| Execution-capable drivers | `Blocked` |

## Reopen Triggers

Reopen runtime restoration before release if any of these occur:

- A new start path bypasses `ProcessesService` or the project-structure bridge.
- A driver package gains process mutation, runtime hosting, external I/O, storage/workspace writes, or DI registration.
- Run health, block reason, recovery options, invariant diagnostics, outbox health, or attempt timeline disappears from operator readback.
- Playwright release smoke fails for global process start, run-detail recovery, or project-structure output navigation.
- Source scans find active bundle-path references or forbidden runtime-host terms in production source.
