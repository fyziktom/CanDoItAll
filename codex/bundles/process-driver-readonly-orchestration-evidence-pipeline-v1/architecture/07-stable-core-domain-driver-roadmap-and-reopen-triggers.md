# Stable Core And Domain Driver Roadmap

## Stable Core Roadmap
- Keep `CanDoItAll.Processes.Core` deterministic and driver-free.
- Keep Core descriptor types source-only and free of runtime host, DI, package discovery, scheduler, workflow, file, network, storage, workspace, or process mutation behavior.
- Continue exact Core consumer allow-list tests when process-module descriptor usage changes.
- Do not move process lifecycle, transition, finalizer, retry, provider repair, or dispatch behavior into Core to make driver integration easier.

## Domain Driver Roadmap
- Keep domain driver packages as read-only alpha libraries over supplied payloads.
- Add new lanes only with strongly typed request records, supplied-content hash binding, audit facts, redaction checks, and no-mutation assertions.
- Route multi-lane callers through the explicit gateway or the process read-only batch orchestrator.
- Keep observation aggregation over already-produced verification responses; it must not invoke verifiers or discover drivers.
- Preserve source-backed README samples for every package-level sample.

## Reopen Triggers
- Any `CanDoItAll.Processes.Core` reference to `CanDoItAll.Processes.Drivers`.
- Any `Verify(object)`, lane-name dispatch, dynamic/object payload dispatch, reflection selector, registry, provider, pack, runtime host, or service-registration API in the driver/gateway path.
- Any manager command, scheduler hook, workflow hook, hosted service, or dependency-injection extension that invokes drivers.
- Any file/network/storage/workspace read or write from verification packages, gateway, process read-only adapters, payload builders, or orchestration.
- Any process mutation, claim, transition, finalizer, retry, provider repair, connector call, shell execution, Office/Graph runtime call, or business-record mutation from verification paths.
- Any completed validator failure, prepared validator failure, full unit failure, focused driver unit failure, focused process adapter integration failure, source scan failure, missing critical manifest, missing semantic invariant, or stale source-backed README sample.

## Handoff Rule
The next bundle must start by reopening the current source, not by trusting this report. Use the P18 manifest, completed validator transcript, bundle zip, and the source-backed tests as entry proof, then rerun build, focused tests, source scans, and validators before changing behavior.
