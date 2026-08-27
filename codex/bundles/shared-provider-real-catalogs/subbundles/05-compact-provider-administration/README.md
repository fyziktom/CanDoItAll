# SB05: Compact provider controls and move shared connections into a dedicated dialog.

## Status

- Status: Completed

Proof tier: Behavioral. Owns N008/R8.

## Objective

Compact provider controls and move shared connections into a dedicated dialog.

## Covered Inputs

- inputs/05-compact-provider-and-token-administration.md.

## Prerequisites

- SB04 must pass. SB04 retained proof and current source inspected; no prior catalog regression observed.

## Exact Source References

- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderManagementPanel.razor.cs
- repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/SharedProviderManagementPanel.razor

## Deliverables

- Icon-only accessible New/Refresh/Connections share one row. Search/reset/count share another. Connections can open without a selected provider; source list is absent from inline Sharing. Add/edit/discover/synchronize and imported/local settings retain existing behavior. Focused SharedProviderSourceAndImportComponentTests, SharedProviderPublicationPanelTests, AgentProviderProfilesPanelPricingTests and new ProviderAdministrationLayoutTests; nonzero discovery before run.

## Dependency Impact

- Existing provider/runtime/API consumers retained; architecture/06-administration-boundaries.md
defines ownership and compatibility. SB06 depends on SB05 UI regression gate.

## Validation Depth

- Proof tier: Behavioral. Bounded xUnit/VSTest filters; freeze discovered names before execution,
zero discovery fails. No full-suite mandate. Expand for actual public-contract or DI impact.

## Acceptance Checklist

- Icon-only accessible New/Refresh/Connections share one row. Search/reset/count share another. Connections can open without a selected provider; source list is absent from inline Sharing. Add/edit/discover/synchronize and imported/local settings retain existing behavior. Focused SharedProviderSourceAndImportComponentTests, SharedProviderPublicationPanelTests, AgentProviderProfilesPanelPricingTests and new ProviderAdministrationLayoutTests; nonzero discovery before run.
- Desktop Playwright MCP normal and open-dialog interaction proof, not screenshot-only.
- Log actual failures; no invented success or fixture model changes.

## UI Composition

Primary provider editor/token form stays visible; supporting connections/tokens are modal.
Provider counts are compact inline text, not a separate card. Provider list/editor stays
split; source list/add flow uses a wide/medium dialog. Scopes use a bounded checkbox list,
tokens a paged table with readable identity, scopes, expiry/status and actions.
1920x1080 only. List and dialog body own scrolling. Inspect header inside 25rem rail
and nested source editor/catalog overlays for clipping, layering and focus.
First viewport must show working controls, not explanatory chrome.

## Proof Required

- Behavioral proof/SB05: focused component results and actual MCP screenshots/DOM assertions for normal and dialog states.
Named tests cover positive/negative behavior; one dependent flow before closure.

## Progression Gate

- Do not close until acceptance and proof pass. Reopen for source actions trapped in inline
settings, eager token loading, ineffective revocation, scope widening or nonempty 5214.

## C# Architecture Impact

See architecture/06-administration-boundaries.md; cohesive source/token UI extraction.

## Boundary Ownership

Existing UI components orchestrate application services; control-plane registry owns persistence.

## Dependency Direction

Existing Web/Modules -> Infrastructure/SharedKernel only; no reverse UI references.

## Pattern Decision

Cohesive components and a persistence seam; no new generic layer or project.

## Testability Contract

Independent registry and component tests plus real authenticated HTTP behavior.

## Partial Class Policy

Only cohesive Razor code-behind; no new runtime partial boundary.

## Architecture Proof Required

Scoped CodeAnalytics, actual source review, old-owner shrink, no project changes, focused tests.
