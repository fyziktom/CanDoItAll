# Target Solution

## Allowed Production Surface In This Bundle
- A process-module read-only adapter that invokes the existing `.NET/Rust` transcript verifier alpha using supplied evidence/transcript payloads.
- Immutable process-owned verification observation/envelope models that carry diagnostics, evidence references, audit facts, redaction status, and no-mutation proof.
- Adapter-owned mapping between process artifacts/proof transcripts and driver abstraction references.
- Tests and docs for future runtime host, registry, and manager command design.

## Denied Production Surface
- Generic driver registry, selector, host, provider, DI registration, manager command, workflow hook, scheduler hook, connector hook.
- Shell command execution, package restore, cargo/dotnet execution, Office/Graph runtime calls.
- Workspace/storage/process mutation, artifact writes, claim/lease mutation, transition application, finalizer application, retry scheduling.
- Core reference to drivers or driver reference to Modules/Infrastructure/AgentFramework/EF/UI/storage/workspace.

## Dependency Direction
- `CanDoItAll.Processes.Core` remains independent of driver abstractions and transcript verification.
- `CanDoItAll.Processes.Drivers.TranscriptVerification` depends only on `CanDoItAll.Processes.Drivers.Abstractions`.
- `CanDoItAll.Modules.Processes` may consume the verifier only through a narrow read-only adapter and must remain the sole owner of process state and side effects.
