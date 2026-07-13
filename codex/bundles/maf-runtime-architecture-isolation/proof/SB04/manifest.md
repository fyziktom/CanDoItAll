# SB04 Manifest

## Status

- Result: `Partial pass`
- Scope: provider construction, streaming dispatch, finalizer/trace execution contracts.

## Evidence

- Added `IMafProviderCredentialService` and `MafProviderCredentialService`.
- Added `IMafProviderAgentFactory` and `MafProviderAgentFactory` for OpenAI, Azure OpenAI, and Ollama MAF agent construction.
- Added `IMafProviderStreamingRunner` and `MafProviderStreamingRunner` for dispatch lease, timeout, and streaming overload selection.
- Moved `RuntimeBuildResult`, `HostedRuntimeAgent`, `FinalizerCapture`, `ToolInvocationTraceRecorder`, and `ScriptContentInspection` out of private nested runtime types.
- Deleted `MafAgentRuntime.Session.cs`; streaming now runs through the provider streaming runner.

## Production Behavior Artifact Matrix

| Artifact | Production Path | Status |
| --- | --- | --- |
| Credential resolution | `IMafProviderCredentialService` | Used by runtime and provider factory |
| Provider client construction | `IMafProviderAgentFactory` | Used by main runtime and finalizer repair agents |
| Streaming dispatch | `IMafProviderStreamingRunner` | Used by primary run and finalizer repair paths |
| Finalizer capture | `FinalizerCapture` | Used by runtime and direct tests |
| Tool invocation traces | `ToolInvocationTraceRecorder` | Used by runtime and direct tests |

## Residual

- The full runtime build coordinator still lives in `MafAgentRuntime.AgentFactory`.
- Session construction remains in the pre-existing `MafRuntimeSessionBuilder`, which is acceptable for this pass but not a full SB04 closure.
