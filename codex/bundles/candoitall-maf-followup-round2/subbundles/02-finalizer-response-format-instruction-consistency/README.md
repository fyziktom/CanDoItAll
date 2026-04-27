# Subbundle 02 — Finalizer instruction and response-format consistency

## Goal

Make finalizer instructions compatible with `ChatResponseFormat.ForJsonSchema(...)`.

## Problem

The current required-finalizer instruction says normal assistant text is display-only. But structured-output runs also configure a schema response format. The final assistant response should still be valid JSON matching the schema.

## Required implementation

1. Replace the single `AppendFinalizerInstructions(...)` method with mode-aware instruction rendering.

Suggested signatures:

```csharp
private static string AppendFinalizerInstructions(
    string instructions,
    AgentFinalizerPolicy? finalizerPolicy,
    AgentFinalizerMode finalizerMode,
    bool hasStructuredResponseFormat)
```

2. Required-mode instruction template:

```text
Finalizer tool policy:
- Call `<tool>` exactly once before finishing.
- The `<tool>` arguments are the authoritative machine output for `<contract>`.
- After the finalizer call, return exactly one JSON object matching the same structured-output schema.
- Do not use Markdown, prose, code fences, or any extra text in the final assistant response.
- Do not call any other `submit_*` finalizer tool for this contract.
```

3. Shadow-mode instruction template:

```text
Shadow finalizer telemetry:
- The final assistant response JSON remains the source of truth for `<contract>`.
- You may call `<tool>` at most once as telemetry.
- If you call `<tool>`, its arguments must match the same meaning as the final JSON response.
- Do not use Markdown, prose, code fences, or extra text in the final assistant response.
```

4. Disabled mode: no finalizer instruction.

5. If `hasStructuredResponseFormat` is false, do not imply schema response format exists. Keep the instruction accurate.

## Tests to add

- Required-mode instructions include “return exactly one JSON object”.
- Required-mode instructions do not contain “assistant text is display-only”.
- Shadow-mode instructions do not contain “exactly once”.
- Disabled mode appends no finalizer text.

## Acceptance criteria

- Instructions never conflict with the configured response format.
- The model receives a single coherent output protocol.

## Status

Completed.

## Requirements Owned

R02, F02.

## Prerequisites

Subbundle 01 must be completed or already proven in current source.

## Dependency Impact

Critical foundation for final hardening verification and required-finalizer reliability.

## Validation Depth

Behavioral or reflection tests for rendered required, shadow, and disabled instruction text.

## Progression Gate

Downstream verification may continue only after required-mode instructions demand JSON-only final assistant output and shadow mode does not require exact-once finalizer usage.

## Closure Proof

Required-mode instructions demand exactly one finalizer call after significant tool work and exactly one JSON object through the structured response format. Shadow mode now says at most one comparison call and keeps final assistant response JSON authoritative. Focused proof: `MafAgentRuntimeTests` passed.
