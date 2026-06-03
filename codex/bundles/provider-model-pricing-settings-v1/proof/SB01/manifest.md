# SB01 Proof Manifest

## Status

- Subbundle: `SB01 provider-model-pricing-settings`
- Closure: `Passed with targeted test and browser proof`

## Changed File Hashes

- `bundle://proof/SB01/changed-file-hashes.txt`

## SHA-256 Changed-File Hashes

- `1b2b20b6ddd57eef21a7dfe8cc50b8ad98c5fcddcfdb42a01145914a1038d425` `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `5b4afdcd9cdad4bb3973c970ae4c43755ec4becea5eb965bf8d69f3bacc23b80` `repo://src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs`
- `3656a22fd45ab91b243e484f2f9c8e5a66dc980b9e5bec9748ba68e720c42e87` `repo://src/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`
- `bec7e7d9579e46d658768f7ee094ce7efa13d3b2c6554b7fcdc4fc98277e980b` `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderModelPricingEditor.razor`
- `e3570a50635c9f9107f866ce5eaed1e5291281720ef4ccec7f590d202b0e6ec4` `repo://tests/CanDoItAll.Tests.Unit/ProviderPricingTests.cs`

## Command Transcripts

- Prepared validator: `bundle://proof/SB01/transcripts/validate-bundle-prepared.txt`
- Completed validator: `bundle://proof/SB01/transcripts/validate-bundle-completed.txt`
- Provider pricing tests: `bundle://proof/SB01/transcripts/provider-pricing-tests.txt`
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Browser proof: `bundle://proof/SB01/transcripts/browser-proof.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/provider-pricing-tests.txt`

## Failing-First Evidence

- Failing-first N/A process/non-production exemption: this was a missing-settings repair where the pre-change behavior was established by source inspection rather than a committed failing test. Adversarial negative proof is captured by `OpenAi_pricing_discovery_requires_secret` and `Ollama_pricing_discovery_returns_model_names_without_prices`.

## Source Assertions

- `bundle://proof/SB01/source-assertions.md`

## Semantic Invariants

- `bundle://proof/SB01/semantic-invariants.md`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `ProviderDiscoveredModelPrice` | `repo://src/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs` | `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs` | Provider API discovery result used during refresh and discarded after merge | `OpenAi_pricing_discovery_requires_secret`, `Ollama_pricing_discovery_returns_model_names_without_prices` |
| `ProviderModelTokenPrice` | `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs` | `ProviderPricingMetadata`, runtime pricing calculators | Persisted in provider metadata and rehydrated into runtime provider profiles | `Discovered_prices_override_same_model_but_preserve_manual_rows` |
| `ProviderModelPricingRefreshResult` | `repo://src/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs` | `repo://src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs`, `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor.cs` | Returned to Blazor hosts after adapter discovery and merge | `OpenAi_pricing_discovery_requires_secret` |
| `Load from provider` button | `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderModelPricingEditor.razor` | Provider settings user | Invokes host callback and leaves manual editing controls intact | Browser proof in `bundle://proof/SB01/transcripts/browser-proof.txt` |

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| `N001` repair provider settings | `Solved` | UI wiring, browser proof, source assertions |
| `N002` missing provider model inference prices | `Solved` | Persisted typed price rows and refresh merge tests |
| `N003` load prices from API if supported | `Solved` | OpenAI-compatible explicit pricing discovery test |
| `N004` add own prices for each model | `Solved` | Manual row preservation merge test and unchanged add/remove row UI |
| `N005` local LLM support | `Solved` | Ollama model-name-only discovery test and editable default rows |

## Browser Evidence

- Route: local port `5045`, route `settings?tab=providers`
- Screenshot: `bundle://proof/SB01/browser/provider-pricing-refresh-desktop.png`
- DOM assertions: provider editor present, model pricing section present, `Load from provider` text present, editable price input present.

## Residual Risks

- Live provider APIs vary. Exact pricing loads only when the model discovery payload includes canonical per-million-token price fields. Model-name-only APIs intentionally remain editable manual rows.
- Default output build/test remains blocked by an existing `CanDoItAll.Web` process locking repo://src/CanDoItAll.Web/bin/Debug/net10.0; validation used alternate OutDir.
