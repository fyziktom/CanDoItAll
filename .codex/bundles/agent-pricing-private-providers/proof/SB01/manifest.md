# SB01 Proof Manifest

## Status

- `Completed`

## Semantic Adequacy

- Provider pricing uses typed records: `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`.
- Provider profile and editor models carry private-provider and price-row state: `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`, `repo://src/CanDoItAll.AgentFramework.Models/Editors/EditorModels.cs`, and `repo://src/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`.
- Provider metadata persistence reads and writes `isPrivateProvider` and `modelPrices` without relying on ad hoc caller strings: `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`.
- Provider editor UI exposes editable input, cached-input, and output prices: `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderModelPricingEditor.razor`.
- Manual agent model overrides fail predictably when the provider lacks a matching model price row: `repo://src/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs`.
- Changed-file SHA-256: `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs` `05027F36031BF7756CDC7989D709457F0B7739535C8BD55E7E4DEE6FD313E27A`.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB01/transcripts/passing-tests.md`.
- Anti-stub transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.md`.
- Failing-first: N/A process exemption; the regression was added with the implementation because the missing-price behavior did not have a pre-existing executable negative test in this bundle.

## Validation

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter ProviderPricingTests -v minimal` passed with 4 tests, 0 failed, 0 skipped.
- `dotnet build CanDoItAll.slnx --no-restore -v minimal -clp:Summary` passed with 0 errors.
