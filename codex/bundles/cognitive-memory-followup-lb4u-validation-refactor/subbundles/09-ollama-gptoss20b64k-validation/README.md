# 09 Ollama GPTOSS20B64K Validation

## Status

- Status: `Ready`

## Objective

Validate local cognitive-memory behavior with Ollama `gptoss20b64k`, including explicit output token budget and truncation proof.

## Covered Inputs

- OpenAI validation evidence from subbundle 08.
- Model settings from subbundle 03.
- Core LB4U probe set.
- Ollama provider profile.

## Prerequisites

- Subbundle 08 must pass.
- Ollama must be available locally with `gptoss20b64k`.
- Token budget and timeout settings must be visible.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs
- C:\Users\lucys\.codex\skills\candoitall-api-cognitive-memory\SKILL.md

## Deliverables

- Local Ollama validation transcript summaries.
- Token/output-length/truncation evidence.
- Comparison against OpenAI validation results.
- Any fixes for local model prompt size or response budget.

## Dependency Impact

- Unblocks closure.
- May require returning to subbundle 03 if token metadata is incomplete.
- May require prompt/context packing changes if local model behavior is poor.

## Validation Depth

- Runtime provider check.
- Core LB4U probe set.
- Token budget and truncation observations.
- Secret exclusion recheck.
- Failure mode proof if local model cannot pass.

## Implementation Steps

1. Select Ollama `gptoss20b64k` profile explicitly.
2. Confirm max output tokens and context behavior.
3. Run core LB4U probes.
4. Compare source-backed answer quality with OpenAI evidence.
5. Fix prompt/context/token issues if needed.
6. Record evidence and blockers.

## Do Not Do

- Do not call Ollama validation complete without proving token budget.
- Do not silently fall back to OpenAI.
- Do not accept truncated answers as pass.
- Do not loosen memory-quality criteria only because the model is local.

## Acceptance Checklist

- Local model id is explicit.
- Output token budget is explicit.
- Truncation state is visible.
- Core probes pass or fail with actionable evidence.
- Secret exclusion remains clean.

## Proof Required

- Provider/model status.
- Probe summaries.
- Token/truncation metadata.
- Workbook evidence.
- Execution report update.

## Browser Validation Logging

- Browser validation is not required unless UI is used.
- If UI is used, capture route and evidence.

## Progression Gate

- Proceed to closure only after Ollama validation passes or an actionable blocker is recorded.

## Suggested Agent Prompt

Validate cognitive memory with local Ollama `gptoss20b64k`. Prove output token budget and truncation behavior, then compare results to the OpenAI validation gate.
