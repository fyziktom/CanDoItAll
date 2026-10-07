# PP3 — complete Request History rendering without moving evidence ownership

## Full slice

Both entry points use the same renderer: global Request History and History under one saved provider.
Include every currently supported filter and More filters state, applied-query summary, results,
coverage/price/usage indicators, bounded cursor navigation, explicit cancel/clear, metadata detail,
canonical owner links and separately requested read-only content. No UI-only imitation of the
metadata/content service. The actual tests already prove those separate reads. [S14–S23,S26]

Retain the lazy contract: zero History service reads on mount, tab entry, filter edits or purely
visual state. Search requests a page. A row selection requests metadata. A linked/captured-content
button requests content with its own authorization. A tab change is not a runtime invocation.

## Recommended physical boundary

The existing `AgentFramework.UI/History` already contains results, metadata, content dialog and
presentation helpers, and its project already references ProviderHistory.Abstractions. Prefer to
extend this family with the real filter/workspace/details renderers and appropriate pure state.
Complete representative scenarios in the existing AgentFramework sandbox, or a small history-only
host referencing that same UI library if it materially simplifies targeted watch/testing. [S21–S23]

A distinct `CanDoItAll.AgentFramework.ProviderHistory.UI` can be justified by measured closure and
consumer analysis, not by naming symmetry. In that option move the whole existing History family;
keep the new leaf dependent only on neutral history contracts and actual reusable UI. Do not make
it depend on AgentFramework.UI just to borrow History markup and then introduce a cycle. Preserve
public namespaces/type forwarding only where real consumers need it. Record intentional neutral
edge changes and prove they do not add runtime dependencies to completed roots.

No new History.Contracts copy is needed merely to rename existing neutral types. Runtime-owned
`HistoryAccessContext`, reader registrations and authorized-operation implementation stay out of
rendered authorization decisions, even though some share the same abstraction assembly. [S24,S25]

## Responsibilities

| Owner | Keeps |
|---|---|
| Product host | Route/global or fixed provider scope, authentication/profile notifications, production service resolution, activation lifetime and safe diagnostics |
| Presentation state/read session | Filter draft, validated applied query, cursor history, requests, metadata/content view ownership, bounded cancellation and publication fences |
| Renderer | Actual form, results, dialogs, focus/scroll and typed originating intents |
| Existing history application | Authorization, partition/fence checks, cursor binding, current canonical-owner validation and permission-specific read pipeline |
| Existing canonical owners | Agent conversation/run, Simple Chat, Workflow and standalone evidence retention/content |
| Sandbox | Own synthetic pages/evidence and deterministic completion/failure controls, never actual grants or production state |

Presentation+intents or a cohesive view contract are both acceptable. Keep one authoritative state
per responsibility; no layer just forwarding every method to an identical interface. Shared read
logic can consume the existing narrow IProviderRequestHistory port but must not own business
permission evaluation. Host notifications are adapted explicitly, not through IServiceProvider.

## Exclusions

Do not change schema, history ingestion/outbox/retention workers, global authorization, per-owner
retention, pricing snapshots, shared relay protocol, runtime dispatch or credentials. Do not replace
server cursors with offsets, fetch all rows for local filtering, add exports/deletes, or introduce
content prefetch. Small demonstrated bugs in the selected UI lifetime may be fixed with tests.
A wider owner defect is mapped separately and must not be disguised as UI extraction.

Provider test-chat/model-maintenance/usage/Voice dialogs and capability/team authoring are deferred.
They remain functional. Preserve PP1/PP2, A2, Workspace, Projects, Resources and Workflow integration.
