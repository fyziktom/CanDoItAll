# Provider Default Model Picker

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Ready for validator`
- Execution status: `Completed`
- Subbundle gate review: `Passed with browser blocker recorded`
- Final closure gate: `Ready for completed validator`
- Browser validation analytics: `Targeted tests passed; browser startup blocked by HealthTimeout`

## Source Input

- User request: Agents settings Runtime tab must choose provider default model by default, offer provider-supported model options, and keep an explicit override text field behind an "override" checkbox.
- Scope signal: The model picker should be generic because provider/model selection also appears in workflow and memory-facing surfaces.

## Bundle Shape

- Profile: `initiative`
- Subbundles:
  - `01-shared-provider-model-choice-foundation`: shared selector semantics and component.
  - `02-agents-runtime-tab-and-dependent-surfaces`: Agents runtime integration, selective dependent surface adoption, tests, and proof.

## Success Definition

- Selecting a provider in the agent Runtime tab leaves the agent model linked to the provider default unless the user explicitly chooses a suggested model or enables override.
- Provider default linkage is stored as an empty agent model so changing the provider default later updates all linked agents at runtime.
- The selector offers provider default plus known or discovered provider `SuggestedModels`, including OpenAI seeded names and Ollama health-check discoveries.
- Custom model names remain possible through an explicit "Override model name" checkbox and text field.
- Explicit model overrides survive the save/reload path even when the chosen text equals the provider default; only an empty model is canonical provider-default linkage.
- The implementation is available as a reusable provider model selector component, not a one-off agent dialog field.

## Follow-Up Closure

- SB03 repaired the canonical model rule: empty model means provider-default linkage; any non-empty model string means explicit override.
- Proof manifest: `proof/SB03/manifest.md`.
- Browser proof for `/agents?tab=agents` was attempted through the managed dotnet-watch app but blocked by a five-minute `HealthTimeout` while the app stayed in `Building`; see `proof/SB03/transcripts/browser-proof-blocker.txt`.
