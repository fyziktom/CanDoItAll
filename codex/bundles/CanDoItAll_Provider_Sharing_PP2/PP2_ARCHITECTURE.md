# PP2: complete Sharing and source-connection rendering

## In scope

Move actual rendering for SharedProviderManagementPanel, SharedProviderLocalPublicationContent,
SharedProviderImportedProfileContent, SharedProviderSourcesDialog and its list/editor/discovery
and nested confirmation surfaces, and SharedProviderRefreshButton. Include scoped CSS/assets,
validation, unavailable and recovery presentation, all existing controls and independent sandbox.
R12–R18 and R25 describe their current owners and contracts. The local publication child must be
read in full at execution, including all eligibility reasons; this review did not inspect every
child implementation. Do a current descendant/caller/asset census before moving it.

Retain the original module components where they are genuine production adapters: injected owner,
DialogService/Reference, lifetime and exact safe projections. A 300-line original renderer behind
a one-line forwarding component is not an extraction. Conversely a host remains legitimate when
only its product effects remain. Count dependency closure and real state/effects, not Razor files.

## Preferred split

`src/UI/CanDoItAll.AgentFramework.SharedProviders.UI` owns the rendered feature family, pure
presentation state and typed view/intents. `src/Sandboxes/CanDoItAll.AgentFramework.SharedProviders.UiSandbox`
composes it with deterministic in-memory scenario data and the real neutral controls. This is
separate from the PP1 provider sandbox to avoid expanding every completed editor graph.

The production AgentFramework module composes both PP1 and PP2. Existing Providers.UI,
AgentFramework.Editor.UI, Core, ProviderManagement implementation, Infrastructure and completed
Workspace renderers must NOT gain a reference to PP2 UI. A common protocol abstraction can be
used only where it already owns that stable semantic. No infrastructure-to-product-contract edge.
No mandatory new contracts project: safe presentation models can reside in this leaf. If extracting
stable value contracts has concrete benefits, define their rightful owner without moving EF or
service implementations upward. Leave public protocols and persistence models in their assemblies.

## Ownership

| Responsibility | Owner after extraction |
|---|---|
| Markup/layout, input events, raw validation, UI-only list state | New leaf under one explicit host seam |
| Local publication/eligibility and stable public identity | Existing ProviderManagement services |
| Remote discovery, source identity pinning, HTTP/network/credential checks | Existing source owner/client |
| Import/local profile IDs, selection, cache integrity, retirement | Existing reconciliation/materialization owners |
| Admitted writes, typed committed/unknown facts and recovery | Existing native mutation/recovery services |
| Parent catalog/editor refresh and acknowledged delivery | Product host; exact source/target/origin |
| Secret lookup/issuance/editing | Existing Workspace/Security/API integrations, metadata-only projection in PP2 |
| Request History, operational model/test dialogs | Retained production slots, separately scheduled extraction |

## Complete flows

Local persisted provider: show eligibility/last publication and all current reasons, publish,
unpublish via confirmation, pending result/retry delivery. Runtime-only/ineligible provider: show
true refusal and no fake Publish. Imported provider: local alias/enabled edit, remote name/default/
model facts, source availability and explicit retirement; no editing remote model fields.

Sources: list/read/retry, new/edit name/URL/secret-reference/private-network intent, test,
discover and select publications, synchronize existing selections, enable/disable and allowed
delete, nested source editor and import confirmation. Preserve exact existing delete/retirement
limits; do not hard-delete references to make a test pass. Source test can update availability;
read/diagnostic/mutation are not synonyms. Map each actual owner effect before rewriting status.

Refresh button: actual reusable renderer, all callers retain exact provider/source context. A sync
may update multiple selected imports; its parent delivery must reflect those effects without
replaying synchronization or resetting an unrelated dirty PP1/A2 draft.

## Sandbox fidelity

Use the same renderers and real dialogs/tables/checklists as production. Simulated owners have a
separate stored snapshot from the live draft so save/reload/duplicate checks are meaningful.
Scenarios: loading, empty, realistic/large; eligible/ineligible/runtime-only; local and imported;
missing secret reference vs metadata failure; unavailable source; identity mismatch; dirty alias;
late source/discovery/review; known reject/commit-warning/unknown; pending delivery; retired/reappeared;
two independent views; nested confirmations. Simulated network and signing are labeled simulations.
No production DI, PostgreSQL, source network or vault is needed for rendering.

No new responsive design, new runtime features, generic event bus, orchestration framework,
reflection service-bag, or copied BaseLib implementations. Desktop and provider contracts stay
stable. Update exact callers/tests, namespace/assembly compatibility and CSS paths when needed.
