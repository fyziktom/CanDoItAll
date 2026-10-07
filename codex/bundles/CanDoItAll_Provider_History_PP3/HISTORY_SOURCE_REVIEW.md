# Current History source and behavioral map

This is a reviewed current-source map, not a generated inventory of every file in the repository.
Use the current checkout and CodeAnalytics/caller search to finish the dependency/test census.
Sources S14–S26 identify the actual files and inspected ranges; quoted behavior is not a claim
that new runtime reproductions were executed by this reviewer.

| Current file/family | What it actually does | PP3 decision |
|---|---|---|
| `Pages/Components/History/ProviderRequestHistoryPanel.razor` | Search form, filters, existing results surface and creation of the actual metadata dialog | Move the reusable complete workspace markup; leave effect composition in the host |
| `ProviderRequestHistoryPanel.razor.cs` | Scope/auth task changes, database notification, draft/context, Search and result intents | Preserve explicit lifecycle; separate product subscriptions from portable UI state |
| `ProviderHistoryFilterDraft.cs` | DataAnnotations/custom validation, fixed UTC range conversion, exact optional IDs and query construction | Reuse/move pure state; do not duplicate backend authority or silently normalize exact tokens |
| `ProviderHistoryFilters.razor` | Ordinary/advanced filter fields, raw date/page size controls and changed callback | Move actual controls; preserve no-read-on-edit and before-submit input semantics |
| `ProviderHistorySearchState.cs` | Applied query, opaque cursor stack (32), replacement/cancel state, result/coverage publication | Reuse correct cancellation/query origin behavior; verify retries and stale intents |
| `ProviderHistoryDetailsDialog.razor[.cs]` | Explicit metadata, separately requested owner/captured content, cancellation and error sanitization | Extract true detail rendering and owned read lifetime; no automatic content access |
| `AgentFramework.UI/History/*` | Existing neutral results, metadata, content and safe formatting | Reuse the actual family, do not redraw it in a new duplicate library |
| `ProviderRequestHistoryService` | Permission pipeline, cursor binding, canonical owner version checks before/after content | Preserve existing native owner; not a renderer responsibility |

## Important preserved contracts

- Global scope is `AllAuthorized`, not all data. A fixed `SingleProvider` scope wins over an entered
  provider field. Backend requires the scope independently. [S19,S24,S25]
- Relative intervals are frozen at Search using TimeProvider. Custom fields are UTC despite the
  datetime-local browser input; interval is from-inclusive/to-exclusive. Preserve the existing
  explicit timezone labels and maximum interval. [S18,S19]
- Applied query remains unchanged after later typing. Next/Previous use that query and its opaque
  cursor, not the live draft. Previous-page history is bounded. Partial/unavailable coverage is not
  equivalent to an empty successful result. [S20]
- Detail rows enumerate up to 16 canonical owner links; each has its exact kind/owner/evidence and
  permissions. Captured standalone content and canonical content are different paths. [S16,S25]
- Content is encoded read-only text with original/captured byte counts, truncation/redaction flags,
  expiry and section labels. It is not an executable script/Markdown action or a complete request
  payload merely because a conversation-context section is shown. [S23]

## Edge cases to prove, not speculative backend defects

The current search state clears Page at the start of pagination. A failed Next therefore has no
accepted page available for navigation. Preserve honest failure and the applied query; if adding
an explicit retry, replay only that READ and bind it to the same query/cursor. Do not silently
recompute a relative interval or apply later draft filters. A deliberate new Search remains a new
request. Do not invent a generalized paging framework. [S20]

Results intents currently name simple actions or an entry ID. During extraction bind them to the
rendered page/search activation too: an old Previous/Cancel/Clear/Details event must not operate on
a successor just because a row ID reappears. Preserve native permission checking as the final gate.
This is targeted lifetime validation for the new seam, not a claim of a observed data leak. [S15]

Current metadata/content reads validate entry IDs and suppress late results. Close explicitly
clears values; Dispose cancels but does not itself clear every retained content reference. Ensure
the extracted owner retires content, callbacks and subscriptions on all closure paths, including
auth/profile change and a parent removed while the content subdialog is open. Do not add a timer
that repeatedly refetches content. [S17,S23]

Render-only numeric/date validation must survive toggling advanced filters. Invalid raw text can
exist while the bound model still contains its previous valid value; validate the actual EditContext
before forming the query. Do not replace InputNumber with lossy string-to-int fallback. [S18,S26]

## Existing tests to retain and extend

`ProviderRequestHistoryPanelTests` already covers lazy reads, applied filters, page-size refusal,
profile/auth/scope changes, exact canonical owner request, encoded malicious text and close during
content. Several old cases use synchronous Click/Submit helpers; migrate only the touched tests to
the repository's awaited renderer-dispatch convention rather than relying on ignored event tasks.
`ProviderHistorySearchStateTests`, `ProviderHistorySeamTests`, History detail/governance browser
and native query/owner/cursor tests remain owners of their behavior. Discover current names rather
than guessing counts. [S26,S29]
