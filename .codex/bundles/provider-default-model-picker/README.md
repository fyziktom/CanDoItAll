# Provider Default Model Picker

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Ready for validator`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Ready for completed validator`
- Browser validation analytics: `Captured for Agents Runtime tab`

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
- The implementation is available as a reusable provider model selector component, not a one-off agent dialog field.
