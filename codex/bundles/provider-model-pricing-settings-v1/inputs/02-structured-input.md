# Structured Input

## Core Objective

- Repair provider settings so model inference pricing can be loaded from provider APIs when supported and manually configured per model, including local LLM models.

## Success Criteria

- Provider settings keep editable model, input, cached-input, and output price rows.
- A refresh/load action asks the provider adapter for model pricing data.
- Exact API pricing metadata becomes exact typed price rows.
- Model-name-only APIs create or preserve editable manual/default rows without claiming exact API prices.
- Saved provider profiles rehydrate pricing rows through existing metadata.

## Hard Constraints

- Use strongly typed `ProviderModelTokenPrice` and `ProviderModelTokenPriceEditorModel`.
- Do not silently overwrite manual pricing.
- Do not claim exact pricing for APIs that only return model names.
- Keep both `/settings?tab=providers` and `/agents?tab=providers` provider surfaces working.

## Allowed Side Effects

- Changes may touch provider pricing models, workspace provider adapters, workspace service editor operations, provider pricing editor UI, and targeted tests.
- No database migration is expected.

## Source Artifacts

- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderModelPricingEditor.razor`
- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs`

## Input Coverage Signals

- `N001`: repair providers settings.
- `N002`: missing settings for provider model inference prices.
- `N003`: load prices from API if supported.
- `N004`: add own prices for each model.
- `N005`: local LLMs must be supported.

## Dependency And Sequencing Signals

- The refresh merge contract must be correct before UI closure because runtime cost accounting uses persisted `ModelPrices`.

## Validation Expectations

- Targeted tests for explicit API pricing, model-name-only discovery, manual row preservation, and persistence/normalization.
- Build or targeted test transcript.
- Source assertions for both provider settings hosts.

## Evidence Contract

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- Command transcripts under `proof/SB01/transcripts/`
- Source assertions under `proof/SB01/source-assertions.md`
- Changed-file hashes under `proof/SB01/changed-file-hashes.txt`

## UI Validation Strategy

- Prefer a large-screen browser pass on `/agents?tab=providers` showing the provider pricing refresh control and editable rows.
- If host setup blocks browser proof, record the gap explicitly and rely on source/build/test proof.

## Browser Validation Analytics

- Log route, viewport, actions, assertions, screenshots, and result in `reviews/01-execution-report.md`.

## Working Assumptions

- Provider APIs vary; exact API pricing is available only when the provider response includes explicit price fields.
- Ollama exposes installed model names, not token prices.

## Primary Risks

- Incorrect refresh merge could erase manual local LLM pricing.
- Exact-pricing and model-name-only discovery could be conflated unless the result message/status is explicit.
