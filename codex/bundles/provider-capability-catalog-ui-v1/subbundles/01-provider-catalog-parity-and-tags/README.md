# Provider catalog parity and tags

## Status

- `Completed`

## Objective

- Make the Agents shell provider tab use AgentFramework provider data, add durable provider tags, seed local Ollama, and render providers through a tag-grouped tree.

## Covered Inputs

- N01, N02, N03.

## Prerequisites

- Prepared-stage bundle validator passes or any validator warnings are repaired.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor`
- `repo://src/CanDoItAll.Modules.Workspace/Pages/Components/ProviderManagementPanel.razor`
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Editors/EditorModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedNormalizer.cs`

## Deliverables

- Provider tag model/editor persistence.
- Local Ollama seed/provider normalization.
- AgentFramework-backed provider panel with `TreeView` and `TagEditor`.
- Targeted tests for provider count parity and tags.

## Dependency Impact

- SB02/SB03 reuse the tag normalization approach for capabilities. Weak provider persistence proof would make capability tag persistence suspect.

## Validation Depth

- Critical UI/data foundation.

## Implementation Steps

1. Add `Tags` to provider model/editor and normalize them.
2. Seed local Ollama with local tags and merge seeded/default provider tags.
3. Add provider tree node builder and AgentFramework provider panel.
4. Swap the Agents shell provider tab to the new panel.
5. Add focused tests and source assertions.

## Scope Exceptions

- Workspace settings provider panel remains DB-scoped and is not removed.

## Do Not Do

- Do not change provider runtime execution semantics.
- Do not remove the existing remote Ollama fallback.

## Acceptance Checklist

- `Local Ollama` appears in seeded providers.
- Provider tags survive save/reload.
- Provider tree renders tag parents and provider children.
- Provider badge/list mismatch is gone.

## Proof Required

- Targeted unit/component tests.
- Build or relevant project compile.
- Browser screenshot of `/agents?tab=providers` at large desktop viewport.
- Source hash/proof manifest entries.
- Closure manifest: `bundle://proof/SB01/manifest.md`.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- Route: `/agents?tab=providers`.
- Viewport: large desktop first.
- Actions: open route, compare provider badge/list count, select a provider tree child, edit tags.
- Screenshot review: no overlap, no hidden tree rows, no dialog or form clipping.

## Progression Gate

- Provider count parity and durable provider tags must be proven before SB02 starts.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
