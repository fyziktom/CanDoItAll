# Structured Input

## Current decision
The latest branch now has a multi-domain verification gateway and read-only process adapters. The next bundle should not add a generic runtime host. It should consolidate the explicit gateway and process read-only orchestration so all current domain drivers can be exercised through a controlled, supplied-evidence pipeline.

## Hard constraints
- Preserve all current process runtime behavior.
- No generic `IProcessDriverRegistry`, runtime selector, DI registration, manager command, scheduler hook, workflow hook, or execution-capable driver.
- No shell command execution, package restore, Graph/Office runtime calls, file/network access, workspace/storage writes, process mutation, claim mutation, transition mutation, finalizer application, or retry scheduling.
- Core must not reference driver abstractions or driver packages.
- Drivers must remain read-only over supplied payloads / already-produced descriptors.
- Browser/mobile/small/medium proof remains N/A unless UI/media drift occurs; unexpected UI/media drift fails the bundle.
