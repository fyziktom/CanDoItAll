# Target Solution

## Architecture

- Add a typed provider pricing discovery contract at the workspace provider-adapter boundary, close to existing health/model discovery code.
- Keep Blazor components focused on rendering and orchestration. They call `WorkspaceService` for refresh and receive a typed result, not raw HTTP or JSON.
- Merge discovered model pricing centrally:
  - explicit API prices override the row for that model
  - model-name-only discovery preserves existing manual prices
  - model-name-only discovery creates editable rows with provider-appropriate defaults for newly discovered models
  - non-discovered manual rows remain intact
- Persist through the existing `ProviderPricingMetadata` JSON path.

## Boundaries

- `AgentFramework.Models` remains the owner of strongly typed pricing rows and normalization.
- `Modules.Workspace` remains the owner of provider settings UI, adapter discovery, secret resolution, and editor operations.
- Runtime pricing consumers continue to use `ProviderProfile.ModelPrices`; they should not know whether a row came from API discovery or manual entry.
