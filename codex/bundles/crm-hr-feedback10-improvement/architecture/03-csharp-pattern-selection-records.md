# C# Pattern Selection Records

## PSR-01: Typed Async Loader Strategy

- Problem force: one reusable browser must load different record domains with server paging, search, tags, typed filters, cancellation, and errors.
- Selected pattern: Strategy expressed as a strongly typed async delegate supplied to the component.
- Rejected alternatives:
  - A service-locator lookup by string record kind: stringly typed and hides dependencies.
  - A broad `IRecordRepository`: invents a domain repository in the UI layer.
  - An interface with one trivial implementation per consumer: unnecessary boilerplate when a delegate is the real seam.
- New types/projects: generic request/page/option records and loader delegate in AppComponents; module query methods/adapters.
- Testability improvement: component tests inject deterministic fake loaders; query services test independently.
- Proof required: page/search/tag/filter values reach the loader; cancellation/stale result behavior; compile-time typed selection/filter; no full-list fallback.

## PSR-02: Domain Adapter At The UI Boundary

- Problem force: CRM parties/accounts and Projects have different entities and filters, while the browser needs neutral display records.
- Selected pattern: Adapter from module-owned query/projection to neutral browser option/page records.
- Rejected alternatives:
  - Move domain entities into AppComponents: reverses dependency direction.
  - Reflection/dictionaries for labels and ids: loses type safety.
  - Reuse `CrmHrAgentQueryService`: agent-specific trust/redaction/search contract and no real paging/total.
- New types/projects: cohesive CRM/HR and Projects query adapters in existing modules.
- Testability improvement: adapters run against seeded persistence without rendering UI; presentation mapping is deterministic.
- Proof required: >1,000-record page test, type/tag/search combinations, stable ordering, and no shared-project domain references.

## PSR-03: Explicit Wizard Step State

- Problem force: contact and opportunity creation require ordered steps, back/cancel/finish behavior, isolated drafts, and validation.
- Selected pattern: small enum plus switch/guarded transition methods inside dialog state/components.
- Rejected alternatives:
  - Full State-object hierarchy: disproportionate for a small closed step set.
  - Booleans such as `isStepTwo`: become invalid as steps/validation grow.
  - Immediate mutation of the live model: caused the reported empty-row failure class.
- New types/projects: typed step enums and isolated draft models in CRM/HR.
- Testability improvement: transitions and cancellation can be exercised without persistence or page construction.
- Proof required: invalid transitions blocked, Back preserves draft, Cancel leaves live state unchanged, Finish commits once.

## PSR-04: Read Projection For Financials

- Problem force: Financials aggregates opportunity facts by currency/period and must distinguish absent authoritative sources from zero.
- Selected pattern: dedicated query/read-projection service returning typed availability and chart series.
- Rejected alternatives:
  - Razor-page LINQ aggregation: adds business/query logic to the 1,810-line page.
  - Add methods to `CrmService`: deepens the 6,054-line mixed-responsibility file.
  - Seeded placeholder numbers: misleading and non-production.
- New types/projects: `CrmFinancialSnapshotQueryService` and typed snapshot/availability records in CRM/HR.
- Testability improvement: direct deterministic tests cover currency grouping, periods, empty/unavailable states, and error propagation.
- Proof required: source-of-truth query, mixed-currency negative test, unavailable bought/invoice states, UI renders typed states without fake series.

## PSR-05: Typed CRM Route Catalog

- Problem force: module subroutes need contextual workbench titles without hard-coding CRM domain details into MainLayout.
- Selected pattern: a typed/static CRM route catalog consumed by `CrmHrSecondaryTabs` and Web's workbench descriptor builder.
- Rejected alternatives:
  - `IShellNavigationContributor`: current implementation flattens contributions into actual main navigation and ignores `IsSubItem`.
  - Scattered path strings throughout `MainLayout`: increases composition-root conditionals and duplicates route knowledge.
  - Browser `<PageTitle>` only: does not affect workbench tabs.
  - A new cross-module provider interface: unnecessary for one module unless future callers prove the extension requirement.
- New types/projects: CRM/HR route metadata record/catalog in the CRM/HR module; a narrow catalog lookup in Web before generic fallback.
- Testability improvement: catalog lookup and workbench descriptor behavior test without rendering the full layout.
- Proof required: distinct visible titles/ids for all subroutes, unchanged main navigation item count/routes, no non-CRM regressions, and Playwright tab-bar confirmation despite `9rem` truncation.

## PSR-06: Thin Facade For Compatibility Only

- Problem force: existing CRM/HR consumers use `PartyPicker`, while the reusable mechanics move to AppComponents.
- Selected pattern: optional thin facade/adapter component during migration.
- Rejected alternatives:
  - Keep both unpaged `Options` and async loader paths: a silent scale fallback.
  - Rewrite every consumer with duplicated host markup in SB01: unnecessary churn.
- New types/projects: none beyond a thinner existing component.
- Testability improvement: facade behavior is limited to filter mapping and selection propagation.
- Proof required: source assertion that paging/search/error mechanics live in AppComponents; old dropdown/inline fallback removed; all consumers updated or facade deleted.

## PSR-07: Opt-In Bounded Results Scroll

- Problem force: Directory and Workforce need Agents-like dense catalogues whose filters and pager remain usable, while picker dialogs already own their own scrolling.
- Selected pattern: a strongly typed/default-off browser presentation option that modifies only the results container.
- Rejected alternatives:
  - Global overflow CSS: changes every picker consumer and creates nested scroll traps.
  - Page-level fixed height: scrolls controls and pager away.
  - Copying the Agents in-memory catalogue: discards genuine server paging.
- Testability improvement: component tests can assert default and opt-in modifiers without a CRM dependency.
- Proof required: fixed filter/pager, bounded results at `1800x1100`, default picker unchanged, and real next-page loads.

## PSR-08: Route-Synchronized Controlled Record Dialog

- Problem force: record selection remains addressable by `partyId`, but permanent split editors waste catalogue width and conflict with the requested Agents interaction.
- Selected pattern: keep page-owned generation/load state and render the existing workspace in one controlled full dialog; opening follows successful selection load and closing clears route selection plus invalidates pending work.
- Rejected alternatives:
  - New stateful detail component containing copied services/view models: broadens an already large refactor.
  - Separate detail and edit state machines: duplicates the same tabbed workspace for no behavioral gain.
  - CSS overlay around the permanent editor: leaves route/loading ownership ambiguous.
- Testability improvement: existing page freshness/privacy tests remain the orchestration seam; browser tests exercise deep-link, close/reopen, nested overlays, and stale completion.
- Proof required: no permanent detail pane, list context remains usable, and a closed dialog cannot be reopened by an old request.

## PSR-09: Thin HTTP Transport Adapter

- Problem force: realistic persistent CRM-HR scenarios must be operable through the product API, while application services already own validation, audit, search, and persistence.
- Selected pattern: cohesive Minimal API route group with typed request models and direct delegation to existing application services.
- Rejected alternatives:
  - Direct `DbContext` writes in Web or a seed-only endpoint: bypasses canonical behavior.
  - Generic command-name dispatcher: stringly typed and weakens OpenAPI.
  - New repository/facade interfaces with one trivial implementation: boilerplate without a new lifecycle or boundary.
- Testability improvement: route handlers are covered through the HTTP host and services retain their existing direct tests.
- Proof required: positive linked scenario, bounded list output, structured invalid-reference/validation responses, no direct persistence, and normal API authorization composition.

## PSR-10: External Search-Before-Create Scenario Operator

- Problem force: demo records must persist and be repeatable without product startup code.
- Selected pattern: an external HTTP workflow uses deterministic external codes, lists/searches before writes, and carries returned typed ids across commands.
- Rejected alternatives: migrations with data, application startup seeding, direct SQL/EF scripts, destructive reset endpoints, and trusting row counts as identity.
- Testability improvement: running the same operator flow twice proves stable business identities against the real API.
- Proof required: two runs, identical external-code identities, no duplicates, and visible UI records.
