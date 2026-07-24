# Normalized Requirements

## Requirement Set

| ID | Raw source | Requirement | Observable acceptance |
| --- | --- | --- | --- |
| `R001` | `N001` | Treat `CRM:` as an informational section heading. | Traceability and closure mark it N/A; no invented feature work is attributed to it. |
| `R002` | `N002` | Use BaseLib `TagEditor` for every mutable tag collection and tag-filter collection in CRM/HR. | Repository audit finds no comma-delimited `TagsText` editor or alternative mutable tag control in CRM/HR; add/remove/deduplicate/case-normalize behavior round-trips. |
| `R003` | `N003` | Make the add-empty-then-remove/cancel contact sequence safe and remove the same callback-identity defect from adjacent editors. | The exact contact sequence raises no exception and leaves persisted contacts unchanged; address and relationship remove/direction callbacks target the intended row after re-render/reorder; tests fail for mutable-loop-index capture. |
| `R004` | `N003` | Replace immediate blank contact-row insertion with a two-step add-contact wizard. | Step 1 shows squared, centered icon/title cards for `PartyContactType`; Step 2 captures value, label, `TagEditor` tags, public flag, and notes; Back preserves the draft; Cancel discards; Finish adds one valid contact. Primary-contact semantics remain with the existing dedicated primary email/phone fields. |
| `R005` | `N004` | Provide a reusable strongly typed paged record browser and picker-dialog host. | A typed loader receives search, selected tags, typed domain filter, page index, page size, and cancellation; returns items plus total; explicit loading/empty/error states exist; selection is strongly typed; no full-list fallback exists. |
| `R006` | `N004` | Support CRM/HR record scopes and high-cardinality operation. | Party filtering distinguishes at least people, organizations, and allowed combined scopes; CRM record adapters remain typed; a >1,000-record test proves stable paging, search, tag intersection, and no duplicates/omissions. |
| `R007` | `N004`, `N005` | Reuse the same record-browser core across CRM/HR forms and ordinary searchable lists. | Relationship, ownership, delivery/party assignment, and other audited high-cardinality selectors use the picker; primary lists reuse the same query/filter/paging semantics rather than a second page-local implementation. |
| `R008` | `N006` | Make the opportunity pipeline a reusable server-paged primary surface with compact filtering. | Account-scoped query performs paging/filtering before materialization; pipeline accepts typed results/callbacks; at `1800x1100` controls occupy one or at most two rows; owner filter uses the party picker; loading/count/reset feedback is explicit; mixed currencies are not summed. |
| `R009` | `N007` | Move opportunity create/view/edit into independent dialogs. | CRM Opportunities tab shows list/pipeline plus Add; Add opens a wizard; card click opens read-only detail; Edit opens a wide edit dialog; close/cancel preserves list/filter context; save refreshes and selects the result. |
| `R010` | `N008` | Select and clear a related project through a reusable scalable project picker. | Opportunity create/edit uses a typed project browser based on project-list presentation/query behavior; search/paging work; selection persists `LinkedProjectId`; clear is explicit; nonexistent projects fail predictably. |
| `R011` | `N009` | Add a `Financials` tab immediately after Overview with task-first metrics and charts. | Sold totals are currency-safe and bucketed by the UTC transition to `Won`; missing amount/Won-history is incomplete data; bought and overdue show typed unavailable placeholders; month/year bars render sold truth; sold/bought donut is unavailable while bought is unavailable; no fake values appear. |
| `R012` | `N010` | Give CRM/HR workbench tabs contextual titles and stable identities. | Opening Directory, CRM, Workforce, Recruiting, Agents, and Assignments produces visibly distinguishable concise labels despite `9rem` truncation, distinct routes/ids, unchanged main navigation, and no cross-module title regression. |
| `R013` | Cross-cutting | Preserve architecture boundaries. | Domain-neutral UI lives in AppComponents; domain queries stay in owning modules; no new feature partials/nested services; large pages orchestrate components rather than own reusable query/aggregation logic; project references remain acyclic. |
| `R014` | Cross-cutting | Match the approved large-screen visual and interaction direction. | Calm, dense, professional composition; compact header/nav; one dominant work surface; restrained focus/selection/loading transitions; no ornamental motion or card mosaic; overlays remain unclipped and actions visible at `1800x1100`. |
| `R015` | Cross-cutting | Close every actionable raw note with Behavioral proof. | Each SB records realistic positive and adversarial negative evidence, exact tests/commands, source proof, anti-stub audit, browser findings where visible, progression result, and raw-note closure. |

## Query Semantics

- Page indexes are zero-based in code; user-facing page labels are one-based.
- Default page size is 24; the owning query validates a bounded maximum rather than accepting arbitrary values.
- Search is trimmed, case-insensitive, debounced/cancelable in the UI, and evaluated by the data source.
- Selected tag filters are case-insensitive and conjunctive (all selected tags must match).
- Ordering is stable and deterministic: display label/title, then typed identifier.
- Changing search, tags, or typed scope returns to page zero.
- A stale or cancelled request never replaces a newer result.
- Errors are visible and retryable; the component does not switch to an in-memory dropdown or stale result set.

## Financial Availability Semantics

- Sold is available only from defensible opportunity records and is never combined across currencies without explicit per-currency grouping.
- Sold month/year uses the UTC `OpportunityStageHistory` transition to `Won`, never forecast close date or `UpdatedAtUtc`; missing transition/amount is incomplete data.
- Bought is `Unavailable` until a purchase-side source exists.
- Overdue invoices are `Unavailable` until invoice persistence and due/payment status exist.
- Unavailable is not equivalent to zero. Charts and stats must distinguish `Available`, `Empty`, and `Unavailable` with a typed state.
- The sold/bought donut remains `Unavailable` while bought is unavailable; a 100% sold donut would falsely imply bought equals zero.
- Month/year aggregation uses UTC calendar buckets with deterministic ordering.

## Explicit Non-Goals

- Small, medium, tablet, or mobile tuning for application pages.
- A new BaseLib package component; the neutral reusable browser belongs in AppComponents.
- Radzen adoption, a second CSS framework, ornamental animation, or page-specific design-system forks.
- Invoice management, purchasing workflows, synthetic bought data, currency conversion, forecasting, or accounting exports.
- Rewriting all 6,054 lines of `CrmHrServices.cs` or all CRM/HR pages.
- Replacing every finite enum/status dropdown; only record/entity selectors and tag controls are in scope.
- Governed proof manifests, hashes, and ceremony; all work units use Behavioral proof.
