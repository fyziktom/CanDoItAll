# SB07 implementation decisions

Prerequisites: SB03/SB06 passed; SB06 has50unit/69integration/13component cases and its governed source gate. Existing implementation/testing authorization applies.

## Tooling evidence

The built-in Components MCP still returns Transport closed. The configured installed executable was successfully called through the existing ToolHarness, then through one short-lived stdio MCP session for60 component_get/usage_examples/examples calls. All succeeded. These are actual MCP results in proof/SB07, not invented/local replacement APIs. No installed configuration, application, service or sibling source was changed. Playwright works on the existing5210 baseline.

## Ownership

One ProviderRequestHistoryPanel in the existing AgentFramework UI serves fixed SingleProvider and AllAuthorized scopes. A pure ProviderHistorySearchState owns applied queries, bounded previous-cursor history and cancellation ownership. ProviderHistoryFilterDraft validates UI strings and converts them to typed neutral query values; no UI string becomes an authorization grant. A separate controlled detail dialog owns explicit metadata/content reads; no body enters the table.

Workspace owns ProviderHistoryPolicyPanel through the neutral Abstractions project. Add only that already planned project edge; never Workspace -> AgentFramework/ProviderManagement. Its typed draft validates rather than clamps numeric values. Policy Load, Preview and Apply are separate operations; future-only changes do not silently shorten old data.

## Form and lifecycle

Hoist provider Tabs out of the existing EditForm. ProviderProfileEditorForm reuses the existing footer/actions and a parent-owned stable EditContext for each editable pane. Sharing and History are outside that mutation form. History has its own EditForm. Saved provider identity keys the panel; unsaved providers cannot search. Preserve the original provider field markup/validation and API-secret selection behavior.

History construction, mount, tab activation, control opening and draft edits make no history/count/facet/source/provider call. Do not copy Sharing or UsageDialog automatic loading. Suppress the existing automatic aggregate usage load on direct Providers and Request history routes; load overview usage when explicitly entering Overview. Keep existing unrelated shell behavior.

Scope/profile/auth-state changes cancel requests, close detail and clear results. Source/key authorization is rechecked in SB06 per operation; do not poll history or token state. A late canceled completion cannot replace a newer page. Keep at most32 previous cursors and one metadata page; do not retain transcript/result caches by provider.

## Composition

Primary surface: compact filter form, then one bounded results DataGrid (AllowPaging=false). Use shared FormStack/FormRow/FormField, typed DropDown/TextBox plus existing InputDate/InputNumber validation semantics. Advanced fields are disclosed explicitly. Use shared Stack/Cluster, badges and feedback states, not metric cards or custom structural CSS.

Default relative range Last24hours resolves to one UTC interval only on explicit Search; also Last7days and Custom UTC range. Default50/max200rows, maximum31-day interval. Fixed-provider context cannot be edited; all-provider scope can be narrowed by stable provider ID, including deleted providers. Applied range/filters and live-page caveat remain visible when drafts change.

Metadata/detail uses a controlled Wide/DenseChrome Dialog with stable close footer and internally scrolling body. Encoded read-only text only; no HTML/Markdown/media fetching. Price provenance, missing facts, caller kind and managed credential ID are explicit. Owner links are metadata references; only supported authorized content can be opened. General policy editor lives in its own Settings tab and form.

Desktop proof:1920x1080 normal, constrained provider pane and open detail/confirmation overlays. No mobile redesign or BaseLib change. One existing host panel/page scroll owner; no decorative nested scrollers. First viewport must include filters/action and useful results or honest not-requested state.

## Entry gate

Pass: source hosts, contracts, planned neutral edge and mandatory component queries checked. No implementation behavior or browser acceptance is claimed by this entry record. SB07 must pass focused controller/component/route tests and real rendered checks before SB08 closure work.
