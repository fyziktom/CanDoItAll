# 03 Model Profile Token Settings

## Status

- Status: `Completed`

## Objective

Make cognitive-memory model execution explicit for OpenAI and Ollama, including model id, provider profile, max output tokens, timeout, and truncation state.

## Covered Inputs

- Current cognitive-memory settings contracts.
- Current provider/profile integration.
- User requirement to validate with OpenAI `gpt-5-mini` and Ollama `gptoss20b64k`.

## Prerequisites

- Subbundle 01 audit must identify the existing model/provider path.
- Any provider secrets or credentials must remain outside logs and bundle files.
- Runtime availability may be environment-dependent; tests should isolate configuration behavior.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs

## Deliverables

- Typed cognitive-memory model execution profile if missing.
- Role-specific model selection for consolidation, probing, professor review, and epistemic drive where needed.
- Explicit output token and truncation metadata.
- Tests for OpenAI and Ollama profile selection.
- API/skill update notes for subbundle 10.

## Dependency Impact

- Unblocks model-assisted consolidation and validation subbundles.
- May require API contract changes that subbundle 10 must document.
- Must preserve existing provider governance and access policy.

## Validation Depth

- Unit tests for profile selection and token settings.
- Integration or smoke proof for settings API if route changes.
- Negative tests for missing model, missing token budget, and disallowed provider mode.

## Implementation Steps

1. Locate current provider and model access policy.
2. Add minimal typed settings for cognitive-memory model roles if absent.
3. Surface max output tokens and truncation state in operation results.
4. Add tests for `gpt-5-mini` and `gptoss20b64k` selection.
5. Ensure no silent fallback occurs.
6. Record API/docs changes for subbundle 10.

## Do Not Do

- Do not log API keys or provider credentials.
- Do not silently substitute models.
- Do not treat deterministic fallback as a successful model-assisted run.
- Do not force all memory operations to use one model role.

## Acceptance Checklist

- OpenAI and Ollama model ids can be selected explicitly.
- Output token budget is visible and tested.
- Truncation is detectable.
- Provider access policy remains enforced.
- API changes are tracked.

## Proof Required

- Test output.
- Settings API smoke output if changed.
- Example model execution metadata.
- Workbook and execution report updates.

## Execution Proof

- Added strongly typed `CognitiveMemoryModelExecutionProfile`, role enum, and `CognitiveMemoryExecutionModelId`.
- Persisted model execution profiles through SQLite/PostgreSQL migrations using `ModelExecutionProfilesJson`.
- API settings now read/write profile role, model id, provider profile, max output tokens, timeout, and local-only state.
- Live settings validation read back OpenAI `gpt-5-mini` profiles with 4096 max output tokens and Ollama `gptoss20b64k` profiles with 8192 max output tokens.

## Browser Validation Logging

- Browser validation is not required unless settings UI is changed.
- If UI is changed, capture desktop route evidence.

## Progression Gate

- Proceed to subbundle 04 only after model settings and token metadata are testable.

## Suggested Agent Prompt

Implement explicit cognitive-memory model role settings and token/truncation metadata. Prove `gpt-5-mini` and `gptoss20b64k` can be configured without silent fallback.
