# Subbundle 01 — Finalizer runtime mode alignment

## Problem

The runtime currently attaches finalizer tools and appends finalizer instructions based only on `structuredOutput`. The execution service later resolves `AgentFinalizerMode` from persisted run metadata. Therefore the runtime can tell the model to call a finalizer exactly once even when the effective enforcement mode is `Disabled` or only `Shadow`.

## Evidence

- `MafAgentRuntime.AgentFactory.cs:67-83`: `CreateFinalizerCapture(structuredOutput)` attaches finalizer tools whenever known structured output exists.
- `MafAgentRuntime.AgentFactory.cs:92`: finalizer instructions are appended whenever capture exists.
- `AgentFinalizerPolicy.cs:88-108`: finalizer mode is resolved later from run metadata and defaults to `Shadow` for process-step runs and `Disabled` otherwise.

## Required change

Introduce a runtime-level execution policy/options object or equivalent parameter that carries the effective finalizer mode into the MAF runtime build.

A possible shape:

```csharp
public sealed record AgentRuntimeExecutionOptions(
    AgentStructuredOutputContract? StructuredOutput,
    AgentFinalizerMode FinalizerMode,
    bool RequireStructuredOutputValidation,
    int MaxStructuredOutputRepairAttempts);
```

Use the actual repository style. The key rule is that `CreateRuntimeBuildAsync(...)` must know the effective finalizer mode before it composes tools and instructions.

## Target behavior

| Finalizer mode | Attach finalizer tool? | Append finalizer instructions? | Validate finalizer? |
|---|---:|---:|---:|
| Required | yes | yes | exact-one required |
| Shadow | yes | yes, clearly marked shadow/comparison | compare if present/expected |
| Disabled | no | no | no |

## Instruction text alignment

Because structured-output runs also configure JSON schema response format, do not instruct the model to return Markdown or prose as the final assistant response.

Recommended required-mode wording:

```text
Finalizer tool policy:
- Call `{toolName}` exactly once before finishing.
- The finalizer arguments are the authoritative machine output for `{contractKey}`.
- After the tool call, return a JSON object matching the same `{contractKey}` schema. Do not use Markdown or prose.
```

Recommended shadow-mode wording:

```text
Finalizer tool shadow policy:
- Call `{toolName}` exactly once before finishing.
- Return the same JSON object through the configured structured response format so the runtime can compare both outputs.
- Do not use Markdown or prose for machine output.
```

## Tests

Add unit/integration tests proving:

- Required mode attaches the finalizer tool and instructions.
- Shadow mode attaches the finalizer tool and shadow instructions.
- Disabled mode attaches no finalizer tool and no finalizer instruction.
- Process automation still passes required mode.
- Manual/non-critical structured output can use disabled mode without receiving finalizer prompt text.

If direct runtime build tests are hard because the method is private, extract a small internal service/helper that can be tested without reflection.
