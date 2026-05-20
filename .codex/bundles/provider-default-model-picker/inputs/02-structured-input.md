# Structured Input

## Objectives

- Make provider default model selection explicit in agent runtime settings.
- Store agent default-model linkage in one place by leaving `AgentDefinition.Model` empty when provider default is selected.
- Keep custom model names available behind an explicit override checkbox.
- Build the UI as a reusable provider model selector component that can be adopted by agent, workflow, memory, image, and voice surfaces.

## Hard Constraints

- Do not remove free-form model entry.
- Do not hard-code model names into the agent dialog; use provider `DefaultModel` and `SuggestedModels`.
- Preserve runtime fallback semantics where an empty agent model resolves to provider default.
- Use shared BaseLib form controls for the selector.

## Assumptions

- Provider health checks and seed profiles are already responsible for filling `SuggestedModels`.
- Agents can persist empty `Model` values safely because runtime code already falls back to `provider.DefaultModel`.
- Workflow components may still need to persist concrete model strings; the shared selector can support both default-linked and resolved-value call sites.

## Validation Expectations

- Component tests cover default, suggested model, and override behavior.
- Agent dialog tests prove provider selection clears model override and save persists an empty model for provider default.
- Targeted component test command passes.
- Browser proof captures the Runtime tab selector open enough to validate layout and labels.
