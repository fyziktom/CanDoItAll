# Target Solution

## Approved Production Work
- Refactor read-only adapters and builders into smaller source files.
- Add typed read-only batch/gateway improvements if they preserve explicit methods.
- Add manager-visible projection planning DTOs over existing observations.
- Add release-candidate tests and source scans.
- Add docs/samples/migration notes for v1.x read-only drivers.

## Explicitly Forbidden
- Generic runtime host.
- Driver registry, selector, provider, pack, DI extension, service registration, manager command.
- Scheduler/workflow invocation.
- `Verify(object)`, lane string dispatch, reflection-based dispatch, late-bound payloads.
- Shell execution, package restore, Office/Graph calls, file/network/storage/workspace access.
- Process mutation, claim mutation, transition mutation, finalizer application, provider repair, retry scheduling.
- Any Core reference to driver packages or abstractions.

## Package Topology Intent
- Core remains pure and driver-free.
- Driver abstractions stay contract-only.
- Domain driver packages stay read-only.
- VerificationGateway may compose domain drivers explicitly.
- Process module may consume gateway/adapters only through allow-listed files.
