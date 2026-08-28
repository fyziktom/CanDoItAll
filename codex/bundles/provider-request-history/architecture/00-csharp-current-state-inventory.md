# C# Current-State Inventory

## Scope And Method

This is a preparation inventory, not a passed runtime architecture gate. Source anchor,
CodeAnalytics coverage and limitations are in [current state](../analysis/01-current-state.md).
Large-file/constructor numbers below are observed at that anchor; remeasure before editing.
They identify seams to protect, not arbitrary file-splitting targets.

## Responsibility And Growth Map

| Current owner | Observed size / construction | Responsibility and risk | Allowed change |
|---|---|---|---|
| SharedProviderRelayApplicationService | 474 lines; 11 constructor parameters | Resolve route/auth/upstream, begin audit, invoke, shape response. | Call small pricing/caller/projection collaborators. Do not add history search, retention or payload parsing here. |
| SharedProviderInvocationAuditFinalizer | About 173 lines inside SharedProviderAuditedRelayStream.cs; 5 parameters | Terminal persistence and bounded retry independent of caller cancellation. | Extract to its own top-level file, consume frozen pricing; preserve terminal concurrency. |
| SharedProviderInvocationAuditService | 2 constructor parameters | Existing canonical audit write path. | Stage compact outbox intent in its actual DbContext transaction. |
| ProviderAdministrationService | 686 lines; 8 constructor parameters | Provider administration. | Small query gateway delegation only if existing gateway contract requires it; no history manager body. |
| DatabaseProviderProfileRegistry | 542 lines; 9 constructor parameters | Resolve active provider/runtime profiles. | Preserve identity/pricing snapshot access; no history reads or policy state. |
| ProviderRuntimeContracts / handle factory | 544 contract-file lines | Typed driver/queue/lease boundaries; factory creates handles. | Minimal optional typed capture context as needed. No body classifier or new giant contracts file. |
| ProviderBackedLlmInvocationAdapter | 2 constructor parameters | Buffered dispatch and empty-response retries. | Per-dispatch attempt observer inside retry, with explicit input ownership. |
| ProviderBackedLlmStreamingInvocationAdapter | 366 lines; 4 constructor parameters | Stream protocol and callback lifecycle. | Typed terminal observer; do not infer usage from the generic callback result. |
| LlmChatConversationEngine | 393 lines; 6 constructor parameters | Existing conversation ownership and commit. | Pass trusted ownership/correlation; canonical bodies remain here. |
| MAF provider client/factory | Factory + transport/empty-result decorators | IChatClient calls can bypass generic runtime handles. | Small dedicated history decorator in the production chain; no second runtime. |
| ProviderUsageQueryService | One source collection dependency | Existing aggregates over all selected contributions. | Preserve behavior. New history query never calls this API. |
| FileSandboxWorkspaceExecutionSliceStore | 3,372 lines | File evidence read/write within workspace persistence. | Owner-specific journal/projection collaborator. No new history methods bulked into partials. |
| PersistentWorkflowUsageObservationStore | 664 lines | Canonical workflow observation persistence/lineage. | Same-transaction intent and typed multi-owner mapping. |
| AgentsHomePage | 371 Razor + 923 code-behind lines | Dashboard/chat/navigation orchestration. | One history-tab host and load exclusion; controller in a small separate class. |
| AgentProviderProfilesPanel | 280 Razor + 451 code-behind lines | Editing forms, model/pricing and Sharing. | Hoist tab/form boundary; extract reusable editor-form wrapper; share History panel. |
| Manager Summary | About 798 lines | Existing lazy date/filter report. | Reuse interaction conventions, not report-fetch implementation. |
| SettingsPage (Workspace) | Existing application settings owner | Separate module from AgentFramework UI. | Add a Workspace-owned policy panel consuming neutral ports. No Workspace-to-AgentFramework reference. |
| RuntimeHostServiceCollectionExtensions | Existing large composition root | Registration of implementations and EF configuration assemblies. | One cohesive history registration call; do not inline all registrations/worker policy. |

Exact source paths and line anchors are in the focused
[pricing](06-sharing-pricing-analysis.md), [history](07-history-performance-analysis.md)
and [UI](08-ui-search-analysis.md) inventories. The literal declared graph is stored in
[project-reference inventory](../inventories/03-project-reference-inventory.json).

## Construction, Coupling And Internal Instantiation

Provider runtime handles and relay finalizers are currently constructed inside existing
factories/application services. Adding a new interface without updating those actual
construction sites would produce a test-only path. SB04 must exercise production factory
composition, including MAF's decorator ordering and callback-based streaming.

Infrastructure discovers EF configurations through AppDbContextModelRegistry and outer
composition. New history mappings must be registered there and in the migration model;
Infrastructure must not reference History.Persistence. ProviderManagement must remain
independent of Workspace/Web/AgentFramework UI. SharedProviders.Abstractions stays neutral;
its small caller protocol shape maps to history contracts in an outer adapter.

## Partial-Class And Size Policy

No new runtime partial classes. Existing Razor code-behind is acceptable as the framework
component companion, but another partial is not responsibility extraction. A moved runtime
behavior must leave its old owner and gain an explicit collaborator and tests.

New cohesive classes normally stay at or below 250 lines. Crossing 250 triggers review;
crossing 400 requires a written redesign or approved exception. Constructor growth is
reviewed with responsibility changes, not hidden inside parameter-object service bags.
Generated files/migrations are reported separately from runtime hand-maintained code.

## Performance Review Coverage

Two passes inspected 14 named files / 6,362 lines. A second wider declaration scan observed
2,231 sealed sites among 2,288 concrete declaration sites; these are declaration sites,
not unique compiled types. It is context, not a mandate to seal every remaining class.

No benchmark or profiler result was produced. The planned improvement is bounded
server-side scalar querying and incremental metadata publication; validate those invariants
with SQL, file-read counts, allocation/latency observations and adversarial scale fixtures
before making any performance claim.
