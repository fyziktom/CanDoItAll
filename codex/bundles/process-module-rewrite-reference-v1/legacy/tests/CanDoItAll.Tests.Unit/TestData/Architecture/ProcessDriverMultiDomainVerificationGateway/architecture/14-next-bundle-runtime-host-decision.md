# Next Bundle Runtime Host Decision

## Status
- Subbundle: `SB058`
- Production verification host registration decision: `Not ready`.
- Next bundle path: `Continue read-only adapters and projection planning`.
- Runtime host status: `Not approved`.
- Prerequisite status: `Not satisfied`.

## Decision
The next bundle must continue read-only domain-driver adapter and projection planning work. It must not introduce production verification host registration, a runtime host, driver registry, runtime selector, DI registration, manager command, scheduler hook, workflow hook, workspace/storage writes, external calls, process mutation, or execution-capable drivers.

The only acceptable near-term production-facing direction is manager-visible read-only verification projection planning that consumes existing verifier responses without scheduling, persisting runtime-host state, registering drivers, invoking commands, or mutating processes.

## Decision Inputs
- `architecture/10-runtime-host-approval-matrix.md` keeps every runtime-host surface `Not approved`.
- `architecture/11-future-production-runtime-prerequisites.md` keeps every prerequisite `Not satisfied`.
- `architecture/12-stable-process-core-roadmap.md` keeps runtime side effects outside Process Core.
- `architecture/13-domain-driver-roadmap.md` keeps the current driver line as `v1.x verification-only alpha`.
- `proof/SB057/manifest.md` closes Gate S with focused denial tests, source scan, red-team rejection, and proof-index validation.

## Blocking Prerequisites
| Prerequisite | Status | Why it blocks production verification host registration |
| --- | --- | --- |
| Runtime lifecycle ownership | `Not satisfied` | No owning module, startup/shutdown contract, cancellation path, concurrency model, retry ownership, or failure handoff exists. |
| Audit persistence | `Not satisfied` | No immutable audit schema, append-only storage owner, output-hash persistence, redaction persistence, or retention policy exists. |
| Sandbox boundary | `Not satisfied` | No process isolation, workspace/storage policy, secret exposure policy, network policy, timeout policy, or cleanup contract exists. |
| Command and external-call allow-list | `Not satisfied` | No strongly typed allow-list exists for commands, file access, workspace/storage writes, HTTP, Office/Graph, Gmail, CRM, provider repair, retry, finalizer, transition, claim mutation, or process mutation. |
| Approval and authorization | `Not satisfied` | No operator approval model, revocation path, approval audit record, dry-run path, or emergency-stop behavior exists. |
| Compatibility governance | `Not satisfied` | Runtime-host contract versioning, public API snapshots, migration docs, downstream tests, source scans, and critical-gate proof are not defined. |
| Red-team proof | `Not satisfied` | Runtime approval must reject report-only approval, implicit DI registration, fallback runtime selection, fixture-only success, non-empty diagnostics as approval, and undocumented manager/scheduler/workflow entry points. |

## Allowed Next-Bundle Candidates
- Manager-visible read-only verification projection planning over already-produced verifier responses.
- Additional read-only adapter hardening for supplied evidence boundaries, redaction, and no-mutation diagnostics.
- Compatibility and contract guard hardening for new descriptor families.
- Documentation and tests that keep current verifier packages explicitly verification-only.

## Denied Until A Future Approval Bundle
- Production verification host registration.
- Generic runtime host.
- Driver registry or runtime selector.
- DI registration or startup hook.
- Manager command, scheduler hook, or workflow hook that invokes drivers.
- Workspace writes, storage writes, file/network/connector calls, Office/Graph/Gmail/CRM calls, or provider repair.
- Finalizer application, transition application, claim mutation, retry scheduling, or process state mutation.
- Execution-capable driver contract line.

## Future Approval Criteria
A future bundle may revisit this decision only after it implements and proves lifecycle ownership, audit persistence, sandbox boundary, command/external-call allow-list, approval and authorization, compatibility governance, and red-team proof. That future work must update `architecture/10-runtime-host-approval-matrix.md`, `architecture/11-future-production-runtime-prerequisites.md`, the driver contract version, API snapshots, migration guidance, tests, source scans, and critical proof manifests in the same bundle.

## Reopen Triggers
- Reopen SB058 if the next-bundle plan proposes production verification host registration before every blocking prerequisite is satisfied.
- Reopen SB058 if any document describes runtime host, registry, selector, DI registration, manager command, scheduler/workflow hook, workspace/storage write, external call, process mutation, or execution-capable drivers as ready for the next bundle.
- Reopen SB058 if manager-visible projection planning starts persisting runtime-host state, invoking drivers, scheduling work, writing workspace/storage, or mutating processes.
