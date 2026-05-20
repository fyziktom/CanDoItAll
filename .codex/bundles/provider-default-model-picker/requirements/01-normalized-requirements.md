# Normalized Requirements

| Id | Requirement | Acceptance Signal | Owning Subbundle |
| --- | --- | --- | --- |
| R001 | Agent Runtime provider selection must offer a provider-default model choice. | Selecting a provider shows a model dropdown with provider default as the default choice. | 02 |
| R002 | Provider-default selection must stay linked to provider settings. | Saved agent has empty `Model` when provider default is selected, and runtime fallback resolves provider default. | 02 |
| R003 | Model dropdown must offer provider-supported options. | Dropdown includes `ProviderProfile.SuggestedModels` without duplicating the provider default. | 01, 02 |
| R004 | Free-form custom model entry must remain available. | Checking "Override model name" displays an editable text field and saves that custom model. | 01, 02 |
| R005 | Selector must be reusable beyond the agent dialog. | Provider model selection behavior lives in a shared AgentFramework component with generic provider/default/options parameters. | 01 |
| R006 | Existing workflow or other provider surfaces should have a migration path. | At least one dependent surface is reviewed or adapted, and unadapted memory-specific UI is documented as no direct model picker today. | 02 |
| R007 | Explicit model override is canonical and must survive save/reload. | If the override checkbox is enabled and a concrete model string is saved, reopening the agent details Runtime tab shows override enabled and the text field populated; only empty `AgentDefinition.Model` means provider-default linkage. | 03 |

## Scope Exceptions

- Cognitive Memory settings currently expose provider allow-list policy but not a rendered per-role model picker in `CognitiveMemorySettingsTab.razor`; this bundle must avoid inventing a new memory profile editor unless implementation discovers an existing hidden model surface.
- Workflow LLM components may continue storing concrete model strings if their runtime contract requires concrete component models.
