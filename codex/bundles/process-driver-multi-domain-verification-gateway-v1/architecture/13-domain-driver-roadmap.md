# Domain Driver Roadmap

## Status
- Subbundle: `SB056`
- Current driver line: `v1.x verification-only alpha`.
- Runtime host status: `Not approved`.
- Execution-capable driver status: `Not approved`.
- Decision: continue read-only domain drivers and adapters; keep production runtime host and execution-capable drivers as future approval-bundle candidates.

## Current Alpha Lanes
| Lane | Package | Current behavior | Current runtime status | Future candidate |
| --- | --- | --- | --- | --- |
| Transcript | `CanDoItAll.Processes.Drivers.TranscriptVerification` | Verifies supplied .NET/Rust transcript text and returns diagnostics, redaction descriptors, audit facts, evidence references, and no-mutation response metadata. | Verification-only; no command execution, package restore, file read, HTTP call, DI, registry, selector, manager command, scheduler hook, workflow hook, workspace write, storage write, or process mutation. | Broader language/parser coverage only after supplied-content and redaction invariants remain green. |
| RuntimeEvidence | `CanDoItAll.Processes.Drivers.RuntimeEvidence` | Verifies supplied Core execution/finalizer/retry/no-progress/provider/projection descriptors and returns contradiction diagnostics only. | Verification-only; no lifecycle mutation, finalizer application, retry scheduling, provider repair, runtime host, registry, selector, DI, or external calls. | Additional Core descriptor families only through compatibility-gated v1.x additions. |
| Artifact | `CanDoItAll.Processes.Drivers.ArtifactEvidence` | Verifies supplied artifact projection and validation descriptors for drift, lineage, trust/sensitivity, and satisfaction consistency. | Verification-only; no artifact writes, file reads, browser calls, storage writes, workspace writes, runtime host, registry, selector, or provider calls. | Read-only artifact result projection after persistence and lifecycle ownership are designed. |
| Office | `CanDoItAll.Processes.Drivers.OfficeEvidence` | Verifies supplied email/document metadata and text. | Verification-only; no Graph call, Gmail call, connector fetch, attachment fetch, category mutation, task creation, document write, workspace write, or storage write. | Connector-backed evidence collection only after sandbox, allow-list, authorization, and audit persistence gates. |
| BusinessAnalysis | `CanDoItAll.Processes.Drivers.BusinessAnalysis` | Verifies supplied deliverable/evidence text for missing requirements, unsupported assumptions, contradictions, and evidence gaps. | Verification-only; no CRM/business-record mutation, task creation, transition, workspace write, storage write, connector call, or external call. | Read-only business result projection after storage and approval boundaries are explicit. |
| ObservationAggregation | `CanDoItAll.Processes.Drivers.ObservationAggregation` | Aggregates already-produced verification responses from allow-listed lanes. | Read-only; never invokes drivers, discovers packages, registers services, persists observations, schedules work, or triggers commands. | Manager-visible read-only summaries only after projection ownership is defined. |
| VerificationGateway | `CanDoItAll.Processes.Drivers.VerificationGateway` | Explicit transcript/runtime gateway methods only. | Read-only; no dynamic discovery, no generic dispatch, no DI registration, no manager/scheduler/workflow hook, no runtime host. | Additional explicit lane methods only after each lane has package proof and gateway tests. |

## Future Execution-Capable Gate
Execution-capable drivers remain out of scope. A future execution-capable contract line must be separate from the current verification-only alpha line and must satisfy:
- Runtime lifecycle ownership.
- Audit persistence.
- Sandbox boundary.
- Command, file, workspace, storage, network, Office/Graph, Gmail, CRM, provider repair, retry, finalizer, transition, claim mutation, and process mutation allow-lists.
- Approval and authorization.
- Compatibility governance with versioned API snapshots and migration docs.
- Red-team proof rejecting report-only approval, implicit DI registration, fallback runtime selection, fixture-only success, and non-empty diagnostics as approval.

## Next-Bundle Decision
Default next bundle: continue read-only adapters, compatibility guards, and manager-visible read-only projection planning. Production verification host registration is not yet ready because prerequisites in `architecture/11-future-production-runtime-prerequisites.md` remain `Not satisfied`.

## Reopen Triggers
- Reopen SB056 if any alpha lane is described as execution-capable, persisted, scheduled, DI-registered, manager-command-triggered, workflow-triggered, or runtime-hosted.
- Reopen SB056 if `ExecutionCapableFuture` is treated as permission instead of a denied future marker.
- Reopen SB056 if a next-bundle recommendation skips audit persistence, sandbox, allow-list, lifecycle ownership, approval/authorization, compatibility governance, or red-team proof.
