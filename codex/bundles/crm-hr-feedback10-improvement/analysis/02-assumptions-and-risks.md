# Assumptions And Risks

## Assumptions

- Raw `N001` is only the `CRM:` heading and closes as informational/N/A.
- `N003` asks for contact-method tags, not an implicit mutation of the parent party's tags. A persisted `PartyContactPoint.TagsJson` property and PostgreSQL migration are therefore planned.
- Multi-tag filters use case-insensitive normalized values and require all selected tags, so adding a tag narrows results predictably.
- Server paging is zero-based internally, uses a stable `DisplayName/Title + Id` order, defaults to 24 records, and never loads the full candidate set merely to calculate the visible page.
- Sensitive-record behavior remains at least as restrictive as current CRM/HR behavior. This bundle does not broaden authorization.
- Won opportunity amount is the current sold-data source. Purchase and invoice data are absent; bought and overdue are explicit unavailable states, never numeric zero masquerading as real data.
- `1800x1100` is the named proof viewport; `1600x900` may be used only when the environment cannot provide the preferred size and the exception is recorded.
- AppComponents owns domain-neutral browsing/picker mechanics. CRM/HR and Projects own their records, filters, EF queries, and adapters.
- A typed static CRM route catalog shared by `CrmHrSecondaryTabs` and Web workbench descriptor resolution is the smallest safe contextual-title seam. Do not use `IShellNavigationContributor` for child-title metadata because it flattens contributions into main navigation.

## Critical Path Risks

- SB01 is the critical foundation. A client-only pager, string-based kind filter, UI-owned EF query, or shared component that depends on CRM/HR invalidates SB02-SB06.
- SB02 establishes actual cross-form reuse. If any high-cardinality CRM/HR party selector remains an unpaged dropdown, SB03 and SB04 proof is untrustworthy.
- SB03 establishes safe dialog draft semantics. If cancel/remove mutates persisted collections before Finish, SB04 opportunity wizard state may repeat the same defect.
- SB04 owns CRM opportunity page composition and project selection. SB05 must wait because both edit the same large page and account-detail state.
- SB05 must not fabricate unavailable business data. A misleading zero or seeded-only chart invalidates the Financials outcome and final closure.
- SB06 title resolution must preserve distinct tab identity and routes. If it collapses two CRM/HR artifacts into one restore key, earlier navigation proof must be rechecked.

## Validation Risks

- CodeAnalytics was unavailable and Components transport was intermittent. Manual source inspection cannot claim automated dependency-cycle completeness; the successful component recommendation does not replace missing library/setup retrieval.
- A browser screenshot alone cannot prove paging occurs in SQL. Query interception or SQL-aware integration tests must show bounded `Skip/Take` behavior with a >1,000-record seed.
- bUnit can prove events and state, but Playwright is still required for focus, layering, clipping, scroll ownership, and actual workbench tab labels.
- PostgreSQL migration generation may be environment/tooling-sensitive. Do not silently rely on `EnsureCreated`; the migration and snapshot must be checked into the normal migration project.
- Financial chart libraries may render asynchronously. Browser proof must wait for rendered series/legend state rather than screenshotting a loading shell.
- Long seeded integration suites may be expensive; use targeted deterministic fixtures and reserve full solution regression for SB06.

## Reopen Triggers

- Reopen SB01 if a consumer needs an untyped id/filter, a full-list fallback, page-local query orchestration, a new partial class, or a reversed shared-project dependency.
- Reopen SB02 if a CRM/HR tag editor remains comma-delimited, selected tags are lost/duplicated, a party selector loads all records, or standard lists use a different filtering engine.
- Reopen SB03 if contact/address/relationship callback identity can throw or target the wrong row, add/cancel leaks an unfinished contact, contact tags do not round-trip through migration/import/export/merge paths, or relationship selection bypasses the shared picker.
- Reopen SB04 if filters exceed two rows at the target viewport, owner/project selection uses dropdowns, list context is displaced by a permanent editor, or route selection and dialogs disagree.
- Reopen SB05 if sold totals differ from the typed projection, mixed currencies are silently summed, bought/overdue render as real zeroes, charts use fixture-only data, or the CRM page owns aggregation logic.
- Reopen SB06 if titles remain identical, restore keys collide, other module tabs regress, any raw note lacks Behavioral proof, or a prior architecture checkpoint no longer holds.
