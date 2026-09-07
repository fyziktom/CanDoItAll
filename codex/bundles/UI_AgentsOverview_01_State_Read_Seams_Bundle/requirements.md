# Requirements and product decisions

| ID | Required outcome | Owner |
|---|---|---|
| OV01 | Preserve `/agents`, typed query/tab codec, selection/context and existing replace-history policy. | Page / O01-O02 |
| OV02 | One accepted Overview supplies dashboard and header counts; HR/avatar/bound resources have distinct header state. | Session/page / O01 |
| OV03 | Separate overview, usage and header reads; scope change performs no unrelated read. | Query/session / O01 |
| OV04 | Independent owned CTS/generations; stale success/failure/finally cannot publish or unlock newer work. | Session / O01 |
| OV05 | Workspace owns desired usage scope; accepted data carries a matching observation stamp. | Session/page / O01 |
| OV06 | Explicit retryable Overview failure; partial HR; visible typed usage partial failures preserving successful data. | Reads / O01 |
| OV07 | Loading, empty, partial, failed and ready remain distinct; failed reads never become fake zero success. | Surface / O01 |
| OV08 | Real stats/charts/lists/teams render without feature services and emit typed intents. | Presentation / O01 |
| OV09 | Dialogs receive accepted current scope and own cancellable reads; unrelated overlays survive cancellation. | Page/dialogs / O02 |
| OV10 | One team intent/navigation; Defaults and HR commands retain page ownership and business semantics. | Page / O02 |
| OV11 | Preserve real composition, charts/assets, CSS/scroll ownership, accessibility and desktop geometry. | O02-O03 |
| OV12 | Physical movement follows valid fresh Overview baseline; application reads/effects stay outside UI. | O03 |
| OV13 | Extend existing sandbox, preserving catalog/capabilities/modes; measure pre/post full app and Parity/Fast. | O03 |
| OV14 | Public RED/GREEN, exact discovery/direct builds, real integration, static/secret gates, honest history. | All |
| OV15 | Preparation only now; implementation requires separate authority. | All |

## Explicit decisions for future authorized execution

- Retain old accepted data internally during scope replacement, but never label/display A totals as requested B. Show loading/error and disable usage-detail actions until matching accepted data exists. A failed B read cannot expose A as B.
- Typed partial B results are accepted B data with visible source warnings. Preserve successful contributions, unpriced/unknown counters, ranking, source metadata validation and deduplication. Contradictory source metadata must not become a plausible partial sum.
- The page owns one session for its lifetime, including other tabs. A conditional renderer cannot own the only summary needed by the persistent header. Fresh Providers/RequestHistory performs no aggregate read and retains unavailable summary presentation. Cached accepted summary may survive those tabs. Fence/cancel aggregate work on history-host entry. Other non-history sections retain current initial demand; no broader lazy-loading redesign.
- Separate header/HR/bound-resource reads from aggregates so independently available header context survives an aggregate failure. Bound-resource failure becomes explicitly unavailable with bounded retry, not zero. This intentionally removes the current combined Task.WhenAll failure coupling. Defaults still performs confirmed warmup then refresh; an Overview retry never repeats warmup.
- Disposal cancels reads and suppresses late UI. For existing dispatched Defaults/HR commands, suppress callbacks without claiming rollback; do not add mutation recovery here.
- A usage overlay captures accepted scope. Scope replacement or leaving Overview cancels only owned usage overlays, and each removed dialog cancels its own query. No global CloseAll. Unrelated overlays remain.
- New error presentation uses safe public text and actionable masked server diagnostics, not raw infrastructure exception messages. Preserve partial-source versus fatal query distinctions.

## Non-goals

No new URL/routed-dialog design, history internals, provider architecture, general capability CRUD, AgentDetails/Chat/Simple Chats/Voice/Floating refactoring, new sandbox project, sibling edits, generic controller/event bus/outbox, durable recovery, history operations or unmeasured performance claims.
