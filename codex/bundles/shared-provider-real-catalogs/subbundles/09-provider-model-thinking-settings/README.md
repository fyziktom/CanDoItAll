# SB09: Provider-owned model thinking settings

## Status

- Status: Completed
- Proof tier: Governed

## Objective

R13/N013 recover stale imported thinking metadata explicitly; R14/N014 expose
automatic and manual per-model capabilities, with one authoritative policy.

## Covered Inputs

- inputs/07-provider-model-thinking-settings-feedback.md

## Prerequisites

- SB07 code remains valid but SB08 acceptance is reopened for 5214. Reproduced old
128-model snapshot and absent Sol capability through Playwright, without saving
the user's agent. Source defaults already define Sol correctly. Entry: Pass.

## Exact Source References

- repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/AgentThinkingEffortPolicy.cs
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderThinkingCapabilityMapper.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor
- repo://src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentThinkingEffortSettings.razor

## Deliverables

- Typed, strictly validated manual overrides in provider configuration, separate
from discovery snapshots. Precedence: explicit administrator configuration,
provider discovery, built-in definition. Reset removes configuration, not discovery.
Shared imports use only published source capabilities and stay read-only.
Provider Thinking tab shows model, provenance, support, control mode and allowed
efforts; edit dialog modifies the provider draft, then normal Save persists it.
Explicit shared refresh action preserves unsaved agent selections and reports errors.

## Dependency Impact

- Models owns configuration and pure policy; Core validates saves; existing source
mapper publishes resolved policy; module UI orchestrates existing administration.
No new project references, runtime partials or generic manager/service layer.

## Validation Depth

- Governed failing-first policy/configuration regressions, focused xUnit/VSTest
Unit/Components/Integration discovery. Filters: ThinkingEffort, ThinkingCapability,
SharedThinking, ProviderProfile, SharedProviderPublicationAndCatalog,
SharedProviderRuntimeProjection and exact new editor/refresh classes.
Freeze actual list-tests results; zero fails. Invalidation keys: manual JSON schema,
policy precedence, save validation, source mapping, UI refresh. CodeAnalytics selects
affected scope after final diff; a broad gate requires a named unresolved impact.

## Acceptance Checklist

- Known Sol choices include None/Low/Medium/High/ExtraHigh/Max without configuration.
- Unknown custom model can be configured explicitly; unsupported stays distinct from unknown.
- Discovery cannot erase manual configuration; reset restores automatic policy.
- Invalid/duplicate/contradictory options fail; unrelated provider JSON survives.
- Source catalog and client dropdown match; per-agent values are not silently changed.
- Shared refresh keeps the draft and makes old snapshot capabilities usable.

## UI Composition

1920x1080 desktop. Existing provider editor gains one Thinking tab. Compact filter
and model table; no duplicate stats cards. Per-row edit opens a focused dialog,
with support/control and effort checkboxes visible together. Dialog body owns
vertical overflow; table fits provider detail width. First viewport shows search,
provenance, several model rows and save access. Inspect normal/edit overlay and
agent runtime dropdown in its existing two-column dialog layout.

## Boundary Ownership

Pure typed Models helper; module-owned editor; existing runtime/catalog adapters.

## Dependency Direction

Unchanged. Scoped CodeAnalytics snap-20260828012250-0196ed5a: healthy, no cycles.
Components MCP Transport closed; local controls/source are the explicit fallback.

## Pattern Decision

Explicit precedence and immutable configuration records; no new interface for a
single trivial implementation. Reuse the existing sharing synchronization service.

## Testability Contract

Pure configuration round-trip/negative tests, production save/discovery tests,
component interactions and actual source/client materialization. SB10 owns real UI.

## Partial Class Policy

Only existing Razor code-behind boundaries; no runtime partials.

## Proof Required

- proof/SB09/manifest.md, semantic invariants, before/after hashes, red/green
transcripts, exact source audit, CodeAnalytics impact and downstream SB10 evidence.

## Architecture Proof Required

Dependency review, isolated policy/editor tests, exact diff review.

## Progression Gate

- SB10 proceeds after focused tests/build. Reopen for lost manual intent, guessed
shared capability, stale draft, cross-agent state or invalid options accepted.
