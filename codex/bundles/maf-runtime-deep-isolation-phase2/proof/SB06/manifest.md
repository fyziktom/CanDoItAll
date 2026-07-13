# SB06 Proof Manifest

## Status

- Planned.

## Required Evidence

- Changed-file hashes.
- Build transcript.
- Finalizer/recovery/session/guard unit tests.
- Source scan proving large helper blocks moved from `MafAgentRuntime.cs`.
- Anti-stub audit output.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Proof |
| --- | --- | --- | --- | --- |
| Finalizer recovery result | Recovery service | Execution coordinator | Produced after finalizer/provider failure paths | Missing/invalid artifact tests |
| Session persistence decision | Session persistence service | Execution coordinator | Produced per run/session | Request-scoped attachment tests |
| Tool invocation guard decision | Tool invocation guard | Execution coordinator | Applied per tool call | Repeated mutation/validation tool tests |
