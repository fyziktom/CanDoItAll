# Target Architecture

## Target end state for this bundle
- `CanDoItAll.Processes.Core` remains deterministic and driver-free.
- `CanDoItAll.Processes.Drivers.*` packages remain read-only alpha packages over supplied payloads.
- `CanDoItAll.Processes.Drivers.VerificationGateway` exposes explicit typed single-lane and batch methods.
- Process module uses narrow read-only adapters and an explicit orchestration path for already-resolved supplied evidence payloads.
- No runtime host, registry, selector, DI registration, manager command, scheduler hook, workflow hook, shell/Graph/file/storage/workspace/process mutation exists.

## Explicitly allowed
- Splitting adapter files.
- Adding typed batch envelopes.
- Adding internal process-module read-only orchestration.
- Adding tests, docs, architecture maps, source scans, package README examples.
- Adding more explicit gateway methods if they are strongly typed and lane-specific.

## Explicitly denied
- Generic `Verify(object request)`.
- `IProcessDriverRegistry`, `ProcessDriverRuntimeSelector`, runtime host, provider, pack, DI extension, manager command.
- Any runtime execution or mutation side effect.
- Any storage/workspace/file/network access in the verification path.
