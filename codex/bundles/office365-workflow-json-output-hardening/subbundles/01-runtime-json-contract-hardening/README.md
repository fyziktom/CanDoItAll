# Runtime JSON Contract Hardening

## Status

- `Completed`

## Objective

Harden the workflow LLM runtime so JSON-required components request provider-enforced JSON response format before model execution while retaining strict invalid-output failure.

## Success Criteria

- JSON-required workflow LLM components populate runtime execution options with JSON response-format requirements.
- Component `ResponseFormatJsonSchema` is used when present; generic JSON is requested when only `RequireJsonOutput` or JSON result shape is present.
- Providers without structured/JSON response-format support fail before the model call with an actionable message.
- Malformed JSON returned by the runtime is still rejected by `ValidateJsonPayload`.

## Covered Inputs

- N001, N003.
- Requirements R1, R2, R3, R4.

## Prerequisites

- Bundle prepared-stage gate passed.
- Source files listed in `## Exact Source References` still exist.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`

## Deliverables

- Runtime execution options carry JSON response-format settings.
- MAF run options apply `ChatResponseFormat.Json` or `ChatResponseFormat.ForJsonSchema(...)`.
- Workflow LLM invoker sets those options from component model settings.
- Focused unit tests cover positive and negative behavior.

## Dependency Impact

- SB02 depends on this phase. If SB01 only changes prompts or only tests non-null output, live Office365 validation may still fail on malformed JSON or pass by chance.

## Validation Depth

- Critical foundation with Semantic Adequacy Gate and artifact-backed proof manifest under `proof/SB01/`.

## Implementation Steps

1. Add explicit JSON response-format fields to `AgentRuntimeExecutionOptions`.
2. Apply those fields in `MafAgentRuntime.Session.CreateRunOptions`.
3. Enforce provider capability before provider execution when a JSON response format is required.
4. Populate response-format options from `MafWorkflowLlmComponentInvoker`.
5. Add unit tests proving schema/generic JSON options are passed and invalid JSON still fails.
6. Capture command transcripts, hashes, source assertions, anti-stub audit, semantic invariants, and update the execution report.

## Scope Exceptions

- Does not repair already malformed model output.
- Does not modify Office365 Graph category operations.
- Does not require UI changes.

## Do Not Do

- Do not strip code fences, extract the first JSON object, concatenate fragments, or retry with a hidden repair prompt.
- Do not loosen `ValidateJsonPayload`.
- Do not broaden this into all workflow schema redesign.

## Acceptance Checklist

- [x] JSON response-format options are observable in unit tests.
- [x] Invalid JSON still throws with node and component identifiers.
- [x] Provider capability mismatch is source-asserted before provider call.
- [x] Project scope test still passes.
- [x] `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md` exist and cite artifacts that exist.

## Proof Required

- Failing-first transcript for a targeted test before implementation or a source-level reproduction note showing options are absent.
- Passing transcript for targeted unit tests.
- Changed-file SHA-256 hashes.
- Source assertions for response-format application, provider capability check, and strict JSON validation.
- Anti-stub audit transcript checking changed production files for `TODO`, `NotImplemented`, fallback extraction, and fixture-specific branching.
- `proof/SB01/manifest.md`.
- `proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- N/A. This subbundle changes backend runtime/test behavior and has no browser-visible surface.

## Progression Gate

- SB02 may start only after SB01 proof shows JSON response-format enforcement, malformed JSON rejection, no parser repair fallback, and passing targeted tests.
- Gate decision: `Passed`. See `bundle://proof/SB01/manifest.md`.

## Suggested Agent Prompt

```text
Implement SB01 only. Harden workflow LLM JSON response-format enforcement at runtime, keep strict invalid JSON failures, add focused tests, and record artifact-backed proof under proof/SB01 before allowing SB02 to start.
```
