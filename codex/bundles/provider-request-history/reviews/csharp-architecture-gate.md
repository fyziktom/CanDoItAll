# C# Architecture Gate Review

## Scope And Verdict

Preparation design review at source commit `dec33cb5614b78266a47dfac214401d5c2bb913d`.
Verdict: `Prepared design accepted`. Independent review and prepared-stage validation
passed; no remaining design blocker was found. This is not a passed product/runtime
architecture gate. No production changes, build, tests or performance measurements occurred.

Review used the named governor and two performance skills, dependency/testability/review
guidance, CodeAnalytics plus literal project graph, and independent sharing/pricing,
history/performance and UI analyses. Analyzer scope/limits are recorded in the inventories.

## Resolved Design Findings

| Finding | Resolution and contract | Future proof owner |
|---|---|---|
| Existing relay writes null price in both completion paths | Freeze execution tariff; preserve provider-reported/unknown/free/partial evidence and long counts. No current-rate historical repair. | SB02/SB04; pricing contract and H02/H06. |
| Validated key ID dropped; subject alone insufficient | Web maps managed jti/issuer/subject to trusted snapshot; legacy/auth-disabled kinds explicit. No ProviderManagement-to-Workspace edge. | SB04/SB06; H07/H12. |
| Runtime handle does not observe all real calls | Typed adapters plus MAF IChatClient decorator inside retry, actual stream terminal and batch/media/diagnostic coverage. | SB04; H05–H08. |
| Canonical history can duplicate charges or bodies | One precise attempt + multiple owner links; compact projections only, no PendingCanonical payload fallback. | SB03–SB05; H09/H13. |
| Mutable source version and nullable attempts could duplicate legacy rows | Stable EntryId/Source identity; versions only concurrency; owner link uses nonnullable EntryId in unique key. | SB01/SB03/SB05; H01/H09. |
| Legacy timestamps and runtime epoch could invent/hide history | Immutable SortAtUtc/TimeBasis, nullable actual start/attempt; stable storage partition distinct from transient fence. | SB01/SB03/SB06; H01/H04/H11. |
| Orphan expiry could exclude later retained canonical evidence | Independently index valid late canonical owner lifetime; do not revive expired detail or deleted owner. | SB05; H10. |
| File first commit after expired reservation could be missed on crash | Durable journal covers first canonical creation/attachment and every later update/delete; pending-reference repair is supplementary. | SB05; H10 crash-after-first-late-commit. |
| Outbox described as atomic without a concrete enlistment | Existing owner stages metadata in its actual AppDbContext/SaveChanges transaction; file protocol has prepared/committed recovery. | SB03/SB05; H03/H10. |
| Aggregate owner could overwrite every attempt's price | Matching-granularity authority only; aggregate provides lineage/content, never copied/distributed attempt totals. | SB02/SB05; H02/H09. |
| Cursor implied an unmodeled snapshot sequence | Live keyset with immutable SortAtUtc/EntryId and explicit Refresh/coverage; no insertion watermark promise. | SB06; H11. |
| Authorization could change while a query awaited | Server rechecks profile/authority before publishing; source content has separate resource permission. | SB06/SB07; H12/H14. |
| History inside provider EditForm could Save on Enter | Hoist tabs; editable panes share ProviderProfileEditorForm; Sharing/History have separate form authority. | SB07; H14/H15. |
| Settings component could introduce Workspace-to-AgentFramework cycle | Workspace owns policy panel via neutral ports; source-verified existing components/input validation. | SB01/SB07. |
| Filename-based filters could silently omit actual producers/authority tests | Correct actual runtime/migration class selectors; include source transaction, batch, media, transfer and new history authority cases. | Phase-specific discovery; plan02. |

## Boundary And Pattern Assessment

Three projects separate BCL producer contracts, application decisions and persistence.
No inner feature references concrete canonical owners, SDK/HTTP/UI or token registry.
Existing source adapters and outer composition connect them. No extra event bus,
universal payload visitor, service locator, duplicate transcript store or version upgrade.

Pattern choices explicitly reject larger alternatives. Existing large files receive small
collaborator calls; extracted finalizer/form behavior must leave its old owner. New runtime
classes use the250/400-line responsibility thresholds and no new partial-class workaround.

## Performance Assessment

Source review identifies unbounded aggregate/file reads as the main interactive-search
risk. The proposed query is scalar/indexed/keyset/bounded; source publication and maintenance
are incremental. Two-pass scan records both findings and already-correct async/options/
sealed patterns. It does not prove latency, throughput or allocation improvements.

SB08 must measure the declared SQL/row/byte/latency/capture/worker targets against the
documented isolated fixture. A small passing in-memory test cannot replace PostgreSQL,
streaming or source-file lifecycle proof.

## Required Implementation Rechecks

- Before editing: refresh actual source/graph, public types, factory and schema registration.
- Per phase: named discovery, source/positive/negative behavior proof and downstream validity.
- At frozenSB08: actual DI/model/producer paths, class-size/constructor/dependency audit,
  durable manifests and measured performance/browser evidence.
- At closure: no unproved data producer/consumer, fake empty result, hidden fallback,
  duplicate cost/body, unauthorized content or stale artifact.

No implementation phase can be closed from this preparation review alone.
