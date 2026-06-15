# Runtime Host Approval Matrix

## Status
- Subbundle: `SB046`
- Current decision: all runtime-host surfaces are `Not approved`.
- Applies to: driver registry, runtime selector, DI registration, manager commands, scheduler hooks, workflow hooks, and execution-capable drivers.
- Contract line: `v1.x verification-only alpha`.

## Approval Matrix
| Surface | Current status | Required future gate before status can change | Explicit non-goal in this bundle |
| --- | --- | --- | --- |
| Runtime host | `Not approved` | Dedicated runtime-host bundle with lifecycle owner, sandbox boundary, audit persistence contract, failure model, and operator approval path | No host that executes, schedules, persists, discovers, or mutates driver work. |
| Driver registry | `Not approved` | Typed registration model with package ownership, compatibility versioning, duplicate-key rejection, and source-backed red-team proof | No dynamic discovery, plugin scan, assembly scan, string-key registry, or fallback registration. |
| Runtime selector | `Not approved` | Deterministic selector contract with allow-listed lanes, no string dispatch, traceable denial reasons, and downgrade/unknown-lane tests | No selector that chooses providers, drivers, tools, or execution modes at runtime. |
| DI registration | `Not approved` | Explicit service lifetime design, testable ownership, package-boundary review, and no hidden startup side effects | No `IServiceCollection` extension, `AddScoped`, `AddSingleton`, hosted service, or startup hook for drivers. |
| Manager command | `Not approved` | Manager command contract with authorization, idempotency, audit persistence, dry-run behavior, and rollback story | No manager command may invoke drivers, mutate processes, apply transitions, or write artifacts. |
| Scheduler hook | `Not approved` | Scheduler ownership, replay rules, backoff policy, cancellation model, and durable audit trail | No timer, queue, background service, retry scheduler, or workflow-triggered scheduling for drivers. |
| Workflow hook | `Not approved` | Workflow lifecycle contract, input/output schema, approval boundary, failure semantics, and cross-module ownership review | No workflow node, workflow executor, subprocess hook, finalizer hook, or transition hook for drivers. |
| Execution-capable drivers | `Not approved` | Separate execution-capable contract line with sandbox, command allow-list, workspace/storage policy, external-call policy, and red-team proof | `ExecutionCapableFuture` remains a denied marker, not permission to execute. |

## Required Future Approval Gates
- Runtime lifecycle ownership must identify the owning module, startup/shutdown behavior, cancellation path, failure mode, and observability contract.
- Audit persistence must define immutable audit records for every request, denial, execution decision, output hash, redaction descriptor, and operator approval.
- Sandbox and allow-list policy must explicitly govern command execution, package restore, workspace reads/writes, storage writes, file access, network/HTTP calls, Office/Graph calls, CRM calls, and provider repair.
- Approval and authorization must specify who can enable a runtime surface, how approval is recorded, how it is revoked, and how unsafe requests fail predictably.
- Compatibility review must update `ProcessDriverContractVersion.Current`, public API snapshots, migration docs, source scans, and focused tests before any runtime surface is consumed.
- Red-team proof must reject report-only approval, non-empty diagnostics as approval, implicit DI registration, fallback runtime selection, and undocumented manager/scheduler/workflow entry points.
- Exact future prerequisite evidence is defined in `architecture/11-future-production-runtime-prerequisites.md`; every prerequisite remains `Not satisfied` in this bundle.

## Non-Goals For This Bundle
- No generic driver host.
- No registry, selector, provider abstraction, runtime pack, service collection extension, hosted service, manager command, scheduler hook, workflow hook, endpoint mapping, or execution-capable driver.
- No shell execution, package restore, arbitrary file read, workspace write, storage write, HTTP call, Office/Graph call, CRM call, provider repair, retry scheduling, finalizer application, transition application, claim mutation, or process state mutation.
- No UI, browser, mobile, screenshot, or media proof is part of runtime-host approval.

## Consumer Rules
- Treat current alpha verifiers as direct, explicit, verification-only components.
- Construct lane-specific typed requests with supplied evidence content; do not introduce generic `object` payload dispatch or stringly typed lane selection.
- Treat `ExecutionCapableFuture` as a rejected capability marker unless a future bundle changes this matrix and the public contract together.
- If a consumer needs host, registry, selector, DI, manager, scheduler, or workflow behavior, it must open a new approval bundle instead of extending the current alpha lane implicitly.

## Reopen Triggers
- Reopen SB046 if any documentation says or implies that runtime host, registry, selector, DI, manager command, scheduler hook, workflow hook, or execution-capable driver surfaces gain approval in this bundle.
- Reopen SB046 if production driver packages gain runtime host, registry, selector, provider, DI, manager-command, scheduler/workflow, process/HTTP/file/storage, or mutation behavior.
- Reopen SB046 if future docs add runtime prerequisites without naming lifecycle ownership, audit persistence, sandbox/allow-list policy, approval/authorization, compatibility review, and red-team proof gates.
