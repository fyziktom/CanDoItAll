# Assumptions And Risks

## Assumptions

- Empty agent model is the canonical representation for "use provider default".
- Provider `SuggestedModels` already includes known OpenAI seeded names and Ollama-discovered models after health checks.
- Existing tests that fill `agents-catalog-model` can be updated to enable override first.

## Critical Path Risks

- If the shared selector treats a provider default as a concrete model instead of an empty value for agents, provider-level model changes will not propagate.
- If provider changes do not clear stale agent model values, selecting a new provider may silently keep the old provider's model as an override.
- If the component is too tightly coupled to agents, workflow and memory surfaces will continue to duplicate logic.

## Validation Risks

- Full browser proof may require starting the Blazor app; if startup is blocked, targeted bUnit tests and a documented browser blocker must be recorded.
- Existing Playwright flow tests may need minor updates because free-form model entry now requires checking override first.
- Workflow components may require concrete models; generic selector adoption there must not accidentally persist empty models unless the workflow runtime supports it.

## Reopen Triggers

- Reopen subbundle 01 if agent integration needs selector behaviors not modeled by the component parameters.
- Reopen subbundle 01 if tests show existing explicit defaults are not normalized to provider-default linkage on save.
- Reopen subbundle 02 if a dependent surface loses model selection ability or workflow creation saves an invalid model.
