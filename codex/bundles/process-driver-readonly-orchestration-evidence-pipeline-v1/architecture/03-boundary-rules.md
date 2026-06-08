# Boundary Rules

## Core
- Must not reference `CanDoItAll.Processes.Drivers.*`.
- Must not reference Modules, Infrastructure, AgentFramework, EF, UI, storage, workspace, file, network, runtime host, or DI.

## Driver packages
- May reference abstractions and, where needed, Core descriptors.
- Must not reference process modules, infrastructure, EF, UI, storage, workspace, external connectors, file IO, network, or runtime service packages.
- Must remain supplied-payload/read-only.

## Verification gateway
- May explicitly compose current alpha verifiers.
- Must not expose generic runtime selection, object payload dispatch, reflection dispatch, registry, DI, manager commands, scheduler/workflow hooks, or execution-capable operations.

## Process module
- May host internal read-only adapters over already-resolved payloads.
- Must not persist verification observations or mutate process state in this bundle.
- Must not resolve arbitrary paths or read files/storage/workspace as part of verification.
