# Target Solution

## Shared Selector

- Add a reusable component under `CanDoItAll.AgentFramework.Components` so modules that already reference AgentFramework UI can consume the same provider model picker.
- Parameters should support a full `ProviderProfile` and lighter provider/default/options inputs so workflow option models can reuse the component without data reshaping.
- The selector displays:
  - dropdown option for provider default, labeled with the current default model when available
  - dropdown options for suggested models
  - explicit "Override model name" checkbox
  - text field only when override is active

## Agent Runtime Semantics

- Agent provider changes clear `editorModel.Model` so the new provider starts linked to default.
- Agent saves normalize `editorModel.Model` to empty when it matches the selected provider default and override is not active.
- Explicit suggested model selections persist that model string.
- Override text persists the custom trimmed model string.

## Dependent Surfaces

- Workflow creation UI can adopt the same component with concrete model resolution during save.
- Cognitive Memory provider allow-list remains unchanged unless a direct model picker is discovered.
