# Shared Provider Model Choice Foundation

## Status

- Status: `Completed`

## Objective

Create reusable provider model selection semantics and UI so provider default, suggested models, and custom override are handled consistently.

## Covered Inputs

- Raw request for a generic component.
- Requirement R003 for suggested provider models.
- Requirement R004 for override text field.
- Requirement R005 for reuse beyond one agent dialog.

## Prerequisites

- Bundle readiness gate passed.
- Provider `DefaultModel` and `SuggestedModels` fields confirmed in existing models.
- BaseLib form components reviewed through Components MCP.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\CanDoItAll.AgentFramework.Components.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\_Imports.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\DropDown.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\CheckBox.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\TextBox.razor`

## Deliverables

- Shared provider model selector component in `CanDoItAll.AgentFramework.Components`.
- Selector supports provider profile input and generic default/options input.
- Component tests for default, suggested model, and override paths.

## Dependency Impact

- Subbundle 02 cannot proceed until the selector exposes clean value semantics for default-linked and custom override modes.
- A broken selector invalidates agent runtime proof and any workflow adoption.

## Validation Depth

- Component-level bUnit tests are required.
- Compile/build proof is required because the component will be used from another Razor project.

## Implementation Steps

- Add the shared component and any minimal helper records/classes.
- Use BaseLib `FormField`, `DropDown`, `CheckBox`, and `TextBox` rather than a local raw-field layout.
- Ensure provider default maps to empty value by default.
- Ensure unknown current values activate override mode.
- Add focused tests in `tests\CanDoItAll.Tests.Components`.

## Do Not Do

- Do not hard-code OpenAI or Ollama model names in the component.
- Do not remove custom model entry.
- Do not change runtime provider resolution in this subbundle.

## Acceptance Checklist

- Selector renders provider default choice.
- Selector renders distinct suggested model choices.
- Selecting provider default emits empty value.
- Selecting suggested model emits that model.
- Checking override shows text field and emits trimmed custom value.
- Unknown current model starts in override mode.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release --no-restore --filter ProviderModelSelector`
- Build/test output recorded in execution report.

## Browser Validation Logging

- N/A for standalone component. Browser proof is required when subbundle 02 places it in the agent dialog.

## Progression Gate

- Passed on 2026-05-20. `ProviderModelSelector` builds in `CanDoItAll.AgentFramework.Components`, focused selector tests pass, and the API supports agent dialog consumption without duplicate local model logic.

## Suggested Agent Prompt

Implement subbundle 01 only. Add a reusable provider model selector in AgentFramework.Components using BaseLib form controls. It must map provider default to empty value, list suggested models, support explicit override text, and have focused component tests. Do not edit the agent dialog yet except if required to compile a shared dependency.
