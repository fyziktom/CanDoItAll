# Target Architecture

## Current restored process runtime
User-facing process execution should continue to flow through:
- UI/API/project-structure launch surfaces;
- `ProcessesService` for run creation;
- durable outbox/claim/lease/finalizer path;
- MAF workflow-backed or direct-agent execution;
- process-owned artifacts, recovery, diagnostics, and readback.

## New target in this bundle
Introduce a **verification-only process driver runtime host alpha**.

Allowed host responsibilities:
- receive typed read-only verification host requests;
- resolve a lane through an explicit registry and selector;
- call a known read-only driver over supplied evidence;
- normalize diagnostics/audit/redaction/no-mutation envelopes;
- optionally persist immutable audit records;
- expose manager-readonly diagnostics.

Denied host responsibilities:
- shell execution;
- package restore;
- file/workspace/storage writes;
- external calls including Office/Graph/CRM/network;
- provider repair;
- process state mutation;
- claim/transition/finalizer/retry mutation;
- scheduler/workflow execution hooks;
- fallback runtime selection;
- unbounded `object` payload dispatch.

## Layering rule
- `CanDoItAll.Processes.Core` must not reference drivers, modules, infrastructure, EF, UI, AgentFramework, workspace, storage, or external calls.
- Process module may consume verification host through explicit, allow-listed read-only manager/process diagnostics paths.
- Domain drivers may stay dependency-clean verification packages.
