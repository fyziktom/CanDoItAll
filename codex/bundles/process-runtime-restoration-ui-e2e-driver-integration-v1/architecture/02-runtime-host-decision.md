# Runtime Host / Registry / Selector Decision

## Current decision

Do not implement a generic runtime host, driver registry, runtime selector, DI-driven driver discovery, manager command, scheduler hook, workflow hook, or execution-capable driver in this bundle.

## Why

The immediate product risk is that user processes may not be launchable from the app after the long refactor sequence. A generic driver runtime cannot compensate for a broken process runtime.

## Allowed in this bundle

- Explicit read-only adapters.
- Test-only fake providers/executors for process runtime proof.
- Optional process-manager diagnostic observation using current read-only verification results.
- Source-backed architecture tests to keep all side effects outside read-only driver paths.

## Not allowed

- Shell execution through drivers.
- Package restore through drivers.
- Office/Graph calls through drivers.
- Workspace/storage writes through drivers.
- Process state mutation through drivers.
- Claim/transition/finalizer/retry mutation through drivers.
- Scheduler/workflow integration for drivers.
