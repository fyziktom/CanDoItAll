# Shell and Usage completion

## Shell

S10 shows actual header counts and navigation still in AgentsHomePage. Extract those controls, help/stats and narrowly typed commands; compose active tab content from the host. Preserve exact public tab tokens, redirects, query parameters, Back/Forward and context permissions. Header failure, HR readiness and data availability remain independent. A failed optional count does not grant/deny unrelated native access.

Keep default-agent/provider feeding behind its existing explicit confirmation and native owner. Refresh, close/reopen or a result delivery failure must not replay feeding. Move the remaining confirmation renderer only if A1 proves it is not already isolated. Existing diagnostics, governance, catalog, capability, provider and Overview surfaces are not reimplemented. Compose native slots honestly in production; a sandbox cannot claim host execution from dummy slot content.

## Usage

Extract the actual consumer/provider/model charts, tables, totals, states and Close/retry presentation. Inspect ProviderUsageDialog completely before edits (only its current identity was tree-confirmed by this review). Reuse the accepted `ProviderUsageQuery` and `ProviderUsageSnapshot` contracts rather than copying their semantics into UI DTOs with different meanings.

Preserve the exact selected workloads and resolved UTC window from Overview into a dialog. Do not recalculate `now` when paging or reopen a 7-day detail with an unbounded/all-time query. Distinguish executions from usage observations, unknown token usage from zero, known cost from unpriced observations, complete totals from partial sources, and provider/model identity from display text. Provider collisions and source-managed opaque model identifiers must remain distinct even if labels match.

Current AgentUsage read code already cancels the old request, verifies returned Query equality and disposes the owned token on completion (S11). Preserve that rather than introducing a new shared mutable singleton. Add explicit native profile/actor/parent ownership where the real callers require it; query equality alone is not cross-profile authority. Any held error or success must only update its original open view.

Failure should keep a usable close/retry path and clear that data is unavailable rather than empty. Retain previous same-scope accepted values only if visibly stale and allowed; never show a different scope's cached data under the new heading. Chart construction is pure and bounded. Pagination/filter interaction must not repeatedly query full runtime history per render or keypress.

## Tests

Compare chart points, grid rows and totals to independently obtained native Usage snapshots, including empty/partial/unknown/unpriced data and duplicate labels with distinct IDs. Include Agent-only, SimpleChat-only, Both and all supported periods. Two dialogs with different windows; scope changes while loading; error/cancellation; late old completion; close/reopen; actor/profile change. Measure query counts during shell rendering and UI-only paging.

Final native journey: run an owned agent and simple chat through actual services, open each real Usage dialog from Overview, compare source identities, accepted window, totals and masked presentation, then open separately authorized History without conflating aggregates with transcripts. Unrelated periods/providers/second client must remain excluded.
