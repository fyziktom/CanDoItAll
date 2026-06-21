# Stable Process Core Roadmap

## Status
- Subbundle: `SB055`
- Process Core status: stable deterministic domain core.
- Runtime host status: `Not approved`.
- Roadmap decision: remaining runtime side effects stay outside Process Core and require future approval gates.

## Stable Core Boundary
Process Core remains the deterministic domain layer for process definitions, runs, steps, claims, finalizer descriptors, artifact descriptors, validation descriptors, retry descriptors, and pure rules. It must not reference driver packages, modules, infrastructure, storage, workspace services, EF, UI, runtime services, manager commands, scheduler hooks, workflow hooks, or connector clients.

Core may define immutable descriptors that other layers read. Core must not execute commands, call providers, fetch files, write workspace/storage, schedule retries, apply finalizers, mutate claims, or persist verification observations.

## Remaining Non-Core Side Effects
| Side-effect surface | Current owner | Current status | Required future gate |
| --- | --- | --- | --- |
| Verification host registration | none | `Not approved` | Runtime-host approval bundle with lifecycle owner, audit persistence, sandbox/allow-list, authorization, compatibility review, and red-team proof. |
| Driver registry/selector | none | `Not approved` | Typed registration and selection contract with duplicate-key rejection, allow-listed lanes, no dynamic discovery, and no string dispatch. |
| DI registration/startup hook | none | `Not approved` | Explicit service lifetime design and startup ownership review; no hidden service collection extension. |
| Manager command | none | `Not approved` | Authorized command contract with idempotency, dry-run behavior, audit persistence, rollback story, and mutation denial tests. |
| Scheduler/workflow hook | none | `Not approved` | Lifecycle, replay, cancellation, backoff, workflow ownership, and durable audit trail gates. |
| Workspace/storage writes | none | `Not approved` | Sandbox and storage policy with typed allow-list, output hashing, cleanup, retention, and failure-mode tests. |
| File/network/connector calls | none | `Not approved` | Denied-by-default external-call allow-list for file, HTTP, Office/Graph, Gmail, CRM, and provider repair. |
| Finalizer/transition/claim mutation | Process module runtime today, not Core verifier drivers | `Not approved for drivers` | Explicit mutation contract that separates domain rules from runtime side effects and records approval/audit evidence. |
| Provider repair/retry execution | process/runtime services today, not Core verifier drivers | `Not approved for drivers` | Ownership, retry budget, approval, cancellation, provider allow-list, and audit persistence gates. |
| Manager-visible verification results | none | `Future read-only candidate` | Read-only projection contract that consumes verifier responses without scheduling, persisting runtime host state, or mutating processes. |

## Core Evolution Rules
- New Core descriptors must be immutable, serializable, and test-covered by pure rules or compatibility tests.
- New Core descriptor families must update `ProcessDriverContractVersion.Current`, API snapshots, compatibility docs, and migration notes before driver packages consume them.
- Core cannot depend on driver abstractions or alpha verifier packages.
- Runtime side effects must be represented as future module/runtime work, not hidden in Core descriptors.
- Any future runtime approval must update `architecture/10-runtime-host-approval-matrix.md` and `architecture/11-future-production-runtime-prerequisites.md`.

## Reopen Triggers
- Reopen SB055 if Process Core gains a dependency on driver packages, modules, infrastructure, storage, workspace, EF, UI, runtime services, manager commands, scheduler hooks, workflow hooks, or connector clients.
- Reopen SB055 if a roadmap or report implies that runtime host, registry, selector, DI registration, manager command, scheduler/workflow hook, workspace/storage write, file/network call, finalizer application, transition application, claim mutation, provider repair, or retry execution gains driver approval.
- Reopen SB055 if future roadmap work marks any runtime prerequisite satisfied without a dedicated implementation bundle and critical-gate proof.
