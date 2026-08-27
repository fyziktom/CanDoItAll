# SB06: Provide scope selection, durable searchable token management and a fresh third client.

## Status

- Status: Completed

Proof tier: Governed. Owns N009/R9 and N010/R10.

## Objective

Provide scope selection, durable searchable token management and a fresh third client.

## Covered Inputs

- inputs/05-compact-provider-and-token-administration.md.

## Prerequisites

- SB05 must pass. SB04 retained proof and current source inspected; no prior catalog regression observed.

## Exact Source References

- repo://src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs
- repo://src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs
- repo://src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs
- repo://src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ControlPlanePaths.cs

## Deliverables

- Scope picker enumerates all declared scopes, confirms/cancels without unintended grants. Tokens dialog fetches only on open, with search and bounded paging. Revoke/delete require confirmation and deny protected HTTP access, including after registry reload. Empty scopes never become api. Metadata contains no bearer tokens. Focused ApiTokenRegistryTests, ApiTokenAdministrationTests, ApiAccessAuthorizationIntegrationTests and SharedProviderAuthorizationIntegrationTests plus impacted issuer callers as required. Rebuild 5210/5212 preserving data; back up/retain old 5214 DB and volume then hand off an empty replacement with zero providers/sources/imports/secrets. Do not change 5032.

## Dependency Impact

- Existing provider/runtime/API consumers retained; architecture/06-administration-boundaries.md
defines ownership and compatibility. SB06 depends on SB05 UI regression gate.

## Validation Depth

- Proof tier: Governed. Bounded xUnit/VSTest filters; freeze discovered names before execution,
zero discovery fails. No full-suite mandate. Expand for actual public-contract or DI impact.

## Acceptance Checklist

- Scope picker enumerates all declared scopes, confirms/cancels without unintended grants. Tokens dialog fetches only on open, with search and bounded paging. Revoke/delete require confirmation and deny protected HTTP access, including after registry reload. Empty scopes never become api. Metadata contains no bearer tokens. Focused ApiTokenRegistryTests, ApiTokenAdministrationTests, ApiAccessAuthorizationIntegrationTests and SharedProviderAuthorizationIntegrationTests plus impacted issuer callers as required. Rebuild 5210/5212 preserving data; back up/retain old 5214 DB and volume then hand off an empty replacement with zero providers/sources/imports/secrets. Do not change 5032.
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

- Governed proof/SB06: semantic-invariants.md, manifest.md, pre/post hashes, failing-first security test, passing transcripts, anti-stub source audit and live HTTP denial.
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
