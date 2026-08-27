# SB04: consistent avatar and manual-setup handoff

## Objective

Restore avatar identity and provide a genuinely blank manual-setup client.

## Status

- Status: Completed

Owns N006/R6 and N007/R7. SB01-SB03 remain trusted.
Proof tier: Behavioral. Local UI state repair and development test deployment;
no production orchestration or new authorization boundary.

## Covered Inputs

- inputs/04-avatar-and-fresh-client.md: N006/R6 and N007/R7.

## Prerequisites

- Entry gate: Pass. Clean working tree. Ordinary browser reproduced avatar-06 on
real-openai card and avatar-07 in editor. Catalog seeds by ID; editor/picker by name.
The explicit avatar URL is already loaded correctly.

## Exact Source References

- repo://src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Components/LlmChatDefinitionEditorDialog.razor
- repo://src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Components/LlmChatDefinitionPresentationMapper.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/AvatarPicker.razor
- repo://tests/Components/CanDoItAll.Tests.Components/LlmChatDefinitionUiTests.cs
- repo://codex/bundles/shared-providers/subbundles/SPMETA-source-metadata-mirroring/proof/Restart-TestInstances.ps1

## Deliverables

- Consistent avatar previews and isolated manual-setup client.

1. Failing-first regression for card/editor/picker default identity and rename.
2. Pass stable existing definition ID to both previews; optional picker seed preserves
   other callers. No new service, project, CSS or BaseLib change.
3. Build/redeploy pair after focused checks; preserve volumes, history and 5032.
4. Create isolated third app/database/data volume, no configured providers/imports/keys.
   Add explicit default-preserving initialization opt-out; see architecture/05-blank-client-initialization.md.
5. Verify all URLs, third-to-source DNS/auth boundary and document manual UI connection.

## Dependency Impact

- SB03 local-browser identity remains unchanged. SB04 gates the fresh-client handoff.

## Validation Depth

- Proof tier: Behavioral. The focused checks below own acceptance.

## Acceptance Checklist

- Existing generated default, name edit, selected asset, uploaded image, reset-to-default.
- Components project filter FullyQualifiedName~LlmChatDefinitionUiTests: expected 10
  existing plus five new cases; freeze actual discovery before execution; zero fails.
- Real browser comparison of card/editor/picker URLs, save/reopen/reload at 1920x1080.
- Docker image build; replacement only of 5210/5212; health/UI smoke on all three apps.
- Fresh third: zero persisted providers, source imports or provider credentials.
- Third reaches source protected catalog by Docker DNS without importing providers.
- Handoff names source token UI, exact scopes, client secret UI and source base URL.
- Invalidation: avatar parameters, local UI access, fresh storage and networking.
- Additional focused tests: ProviderInitializationIntegrationTests (2 cases) and
  ProviderCatalogProjectionFailureTests; freeze discovery including theory rows.
- No unfiltered suite: bounded UI/host contract; no shared schema/runtime changes.
- Workspace header/overview counts match the canonical provider list, including zero.
- Closure proof: bundle://proof/SB04/manifest.md; 36 focused passing cases and three
  healthy final-image apps. Operator instructions: HANDOFF.md.

## UI Composition

Definitions cards stay primary; existing wide editor and picker, tabs, compact metadata
and prose fields unchanged. Dialog body owns scroll; avatar/actions readable in first
viewport. Inspect card, editor and nested picker screenshots at 1920x1080.
Components MCP recommendation/library attempts both failed (Transport closed);
use existing source controls, not custom markup or CSS.

## Proof Required

- Proof/SB04 owns red/green tests, browser comparisons/screenshots, image build, deployment,
health, isolation and handoff. Shallow trap: fixing only outer preview or cloning data
and calling it fresh. Negative cases: nested picker, rename, explicit avatar and reset.

## Progression Gate

- Complete only after avatar checks and healthy three-instance handoff. Reopen on avatar
mismatch, copied providers, local access denial or wrong container source address.
No provider catalog/pricing changes, fixture providers, API-auth disablement, secrets
in proof, unrelated changes or volume deletion.

## C# Architecture Impact

Explicit initialization opt-out; architecture/05-blank-client-initialization.md owns the decision.

## Boundary Ownership

ProviderManagement options; Composition binding/bootstrap; existing canonical runtime loader.

## Dependency Direction

Existing Composition -> module -> ProviderManagement references only.

## Pattern Decision

Typed options, no new abstraction or factory.

## Testability Contract

Two real PostgreSQL initialization tests plus existing canonical-registry tests.

## Partial Class Policy

No new partial; redundant registry policy removed.

## Architecture Proof Required

Scoped CodeAnalytics before/after, exact source review, focused tests and blank Docker restart.
