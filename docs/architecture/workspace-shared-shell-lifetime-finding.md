# WC-C1: Simple Chat reads outlive disposed scoped services

Status: OPEN. Priority P2; application regression readiness is blocked. Workspace
renderers and the existing S0 shell fix remain unchanged by this finding.

## Observed boundary

The full non-live browser campaign recorded `ObjectDisposedException` for both
`SimpleChatsDbContext` and `SemaphoreSlim` during Simple Chat contributor catalogue
initialisation. TestLab's aggregate journey completed its UI/persistence assertions,
then failed its final shared-host log assertion. The trace names Simple Chats, not
TestLab persistence. That log assertion remains intact; this is not dismissed as noise.

Entry commit: `1080c24163afd3cf65fcc68756913c5dd3262a62`. The observed browser checkpoint
is `d6bdaa1ff6257c1dc541849025baa70212699ebecef63677a635ffe9ba94510b` in configuration
WsCompletionProof, using the unchanged campaign sibling trees and private PostgreSQL 18.
The exact assembly hash, 142-case inventory and original result are retained under
`artifacts/workspace-completion/20260930-1080c2416/frozen-browsers-01`.

Affected groups: APP-01/APP-06 shared shell and GATE-02; APP-11's collection-wide log
check also reports it. No wrong-database mutation, disclosure, replay or lost committed
write was observed. Read-only tests and independent module cases remain safe.

## Reproduction and timeline

Run the complete documented non-quarantined, non-live browser inventory against an owned
host. Its shared Playwright collection navigates the real Agent, CRM and TestLab routes.
`TestLabBrowserTests.Production_all_sections_save_reopen_filters_global_and_historical_references_use_real_owners`
then reads the host log and rejects the retained disposed-context exception.

The TestLab case saved and reopened exact plan/child identities and exercised global and
historical references before that assertion. Its owner read-backs do not excuse the
shared-shell failure. The log has no per-entry UTC timestamps, so it cannot identify the
exact preceding navigation or original profile ID that caused each background error.
An isolated rerun and a controlled disposal reproducer are required to refine attribution.

The retained stack is:

1. `LlmChatConversationShellContributor.InitializeCoreAsync` awaits `ReloadCatalogsAsync`.
2. `LlmChatDefinitionUiGateway.ListPageAsync` enters the scoped application service.
3. `LlmChatProfileScopeRunner.ExecuteAsync` acquires a semaphore and a canonical-profile
   lease, then executes a query against `SimpleChatsDbContext`.
4. EF reports that context as disposed. The runner's `finally` also calls `Release` on
   a disposed semaphore. The contributor catches and logs initialisation failure.

## Ownership and competing causes

`ConversationShellHost` owns contributor startup and its cancellation token. Its existing
S0 implementation cancels on disposal and starts `ReleaseLifetimeAsync`, which awaits
initialisation before releasing the token source. This does not itself retain the
separately scoped database/application services until the read finishes.

`LlmChatConversationShellContributor.DisposeAsync` detaches its invalidation handler and
returns; it does not await its stored initialisation task. `LlmChatProfileScopeRunner`
is scoped, and its synchronous `Dispose` disposes the gate without waiting for admitted
operations. `DatabaseProfileLlmChatRuntimeLease` fences profile identity and links
cancellation; it is not an ownership lease for the DI scope or database context.

This is a cross-owner lifetime hypothesis supported by the exact stack and source.
DI disposal order, prerender/circuit retirement and a gateway scope ending early still
need controlled discrimination. Simply swallowing `ObjectDisposedException`, ignoring
the log, or awaiting arbitrary renderer work during disposal could hide failure or
introduce a deadlock and is not a bounded correction established by this campaign.

The runner's admission/release logic originates in `96f054905e` (August 14, 2026), with
later extraction changes. Source provenance is recorded in `shared-shell-lifetime-origin.txt`.
No equivalent historical browser execution was performed. Attribution is **unknown**;
this is not labelled a Workspace or S0 regression merely because this campaign found it.

## Proposed repair bundle

First add controlled tests that hold the actual catalogue read while retiring the shell,
circuit and owned service scope separately. Record original profile/generation, acquired
scope and completion ordering. Then decide which owner must retain the service scope and
drain/cancel admitted reads before disposing it. Keep canonical-profile fencing and the
S0 nonblocking startup/late-publication protections.

The expected repair boundary is conversation contributor/application-scope lifetime;
no schema migration, new provider capability or permission change is justified. The
runner is also used by non-read operations, so review those callers before changing its
disposal contract. Required negative controls include profile retirement during a read,
closed UI with pending initialisation, cancellation with a non-cooperative read, repeated
disposal and no deadlock while the renderer is retiring. Confirm that admitted mutations
retain their existing commit/receipt semantics and are never replayed automatically.

Acceptance requires the controlled cases, existing S0 tests, Simple Chat owner suites,
rapid real route/circuit navigation and a fresh broad browser checkpoint with no disposed
service errors. Preserve the current complete log assertion. No suppression or quarantine
was added in this run.

The unchanged TestLab production journey passed in `campaign-testlab-isolated`, a new
process with a fresh private host. It completed the same owner assertions and complete
server-log check. This narrows the original detection to the shared-host history; it
does not reproduce or repair the scope-retirement race. WC-C1 remains open.

Original evidence: `shared-shell-lifetime-original.log`, the original full browser log/TRX,
source manifest and blame excerpt under the campaign root. Final bookkeeping includes
their hashes. These private test artifacts are not committed or treated as user data.

The complete 143-case `frozen-browsers-02-closure` run reproduced the disposed context and
semaphore path in the shared Collaboration server-log check. Its source checkpoint is
`15e26ac35fdd97478587e21af118fe66fce53bd6b019af9b11053810499b4a3c`.
`shared-shell-closure-original.log` preserves the recurrence together with WC-C2 and WC-C3;
its SHA-256 is `c36b3169687c3a7be4f9cb8e50784108d7e6709cedbe144861d8f6658a3f1a78`.
No lifetime repair was applied, and the full log assertion remains active.
