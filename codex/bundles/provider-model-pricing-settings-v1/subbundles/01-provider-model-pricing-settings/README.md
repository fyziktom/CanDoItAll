# provider-model-pricing-settings

## Status

- `Completed`

## Objective

- Add a provider-pricing refresh workflow that loads exact prices from provider APIs when available, creates editable rows from model discovery when prices are absent, and preserves user-entered manual prices.

## Success Criteria

- Provider settings expose a refresh/load action for model pricing rows.
- OpenAI-compatible API responses with explicit pricing fields produce exact typed price rows.
- Ollama/local model discovery creates editable rows without claiming API prices.
- Existing manual rows survive refresh unless the same model has explicit API pricing.
- Saved provider profiles continue to persist and rehydrate model prices through existing metadata.

## Covered Inputs

- `N001` repair provider settings.
- `N002` missing provider model inference prices.
- `N003` load prices from API if supported.
- `N004` add own prices for each model.
- `N005` local LLM support.

## Prerequisites

- Prepared-stage bundle validator passed or any failures are documented and repaired.
- Current-state source references still match the repo.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderModelPricingEditor.razor`
- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor.cs`
- `repo://src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProviderPricingTests.cs`

## Deliverables

- Typed pricing refresh result and merge behavior.
- Provider adapter support for OpenAI-compatible `/models` pricing metadata and Ollama `/api/tags` model discovery.
- Workspace service refresh method that resolves provider adapter, settings, and secret values.
- UI button/callback from provider pricing editor to refresh current editor rows.
- Focused tests and proof artifacts.

## Dependency Impact

- Runtime cost graphs, process analytics, and provider usage summaries depend on correct `ProviderProfile.ModelPrices`.
- Weak merge proof could hide user cost configuration loss or produce incorrect billing analytics.

## Validation Depth

- `Critical foundation`.
- Requires semantic positive proof, adversarial negative proof, anti-stub audit, changed-file hashes, and raw-note literal closure.

## Implementation Steps

1. Add typed model-pricing discovery/refresh models without weakening existing `ProviderModelTokenPrice`.
2. Add adapter support for OpenAI-compatible explicit price metadata and Ollama model-name discovery.
3. Add a `WorkspaceService` refresh method that merges discovered rows with current editor rows and resolves secrets explicitly.
4. Wire `ProviderModelPricingEditor` to call the refresh method from both provider settings hosts.
5. Add targeted tests for exact API pricing, model-name-only refresh, manual preservation, and unsupported/missing-secret behavior.
6. Capture proof under `proof/SB01/` and update `reviews/01-execution-report.md`.

## Scope Exceptions

- Live provider billing reconciliation is out of scope.
- Exact pricing can be loaded only when the provider API response includes explicit pricing fields; model-name-only APIs remain manual/default editable rows.

## Do Not Do

- Do not add a database migration for pricing rows unless existing JSON metadata cannot represent the settings.
- Do not replace runtime pricing calculation.
- Do not remove manual row editing.
- Do not silently set zero prices for local LLMs.

## Acceptance Checklist

- `ProviderModelTokenPrice` remains the persisted/runtime type.
- Refresh result distinguishes exact API prices from model-name-only discovery.
- Manual rows are preserved across refresh.
- Local LLM model discovery produces editable rows with explicit messaging that prices are user-owned.
- Both provider settings surfaces expose the same refresh behavior.

## Proof Required

- `proof/SB01/manifest.md` with changed-file hashes, command transcripts, source assertions, and raw-note closure.
- `proof/SB01/semantic-invariants.md` covering:
  - invariant `INV-API-PRICE-EXACT`: explicit API prices become exact rows.
  - invariant `INV-MODEL-NAME-MANUAL`: model-name-only APIs preserve manual/default editable prices.
  - invariant `INV-MANUAL-PRESERVE`: refresh does not drop non-discovered manual rows.
  - invariant `INV-UI-PARITY`: `/settings` and `/agents` provider surfaces share refresh behavior.
- Targeted unit/integration/component test transcript.
- Anti-stub audit transcript showing no fake refresh success path.
- Browser proof for `/agents?tab=providers` or explicit gap if the app host is not available.
- `## Production Behavior Artifact Matrix` in both proof manifest and semantic invariants because this subbundle changes provider-settings state and persisted pricing metadata.

## Browser Validation Logging

- Route: `/agents?tab=providers` preferred; `/settings?tab=providers` acceptable secondary surface.
- Viewports: large desktop `1600x900`; narrow `390x844` only if layout changed materially.
- Actions: open provider editor, verify pricing section has add/reset/refresh controls, verify manual rows remain editable after refresh fixture or source-limited proof.
- Screenshots: `proof/SB01/browser/provider-pricing-refresh-desktop.png` when browser proof is available.
- Review questions: no clipped controls, clear refresh affordance, manual row editing remains visible, settings hierarchy remains compact.

## Progression Gate

- `SB01` closes only when targeted tests or documented equivalent proof pass, artifact-backed proof files exist, raw notes `N001`-`N005` are closed, and any missing browser proof is recorded as an explicit validation gap.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
