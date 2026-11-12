# P02c typed recovery adapter — implementation and proof plan

Status: adapter and native integration implemented, source frozen on 2026-10-16.
Completed subset proof is recorded below; the full native/browser lanes are not
closed. Parent owns serialized compilation, discovery/execution, static gates,
DI/host integration and commit. The original adapter-only slice has since been
wired into ProjectsPage; see [native editor record](P02c-native-editor.md).

## Dependency direction and ownership

`Projects.Contracts` adds `ProjectDefinitionRecoveryContracts.cs`: closed immutable
Create/Replace/Scalars submissions wrapping the existing typed request envelopes,
closed full/scalar accepted receipts, frozen payload wrappers and the narrow
`IProjectDefinitionRecoveryCodec` boundary.

`Projects/ProjectDefinitionRecoveryCodec.cs` implements that boundary. It preserves
raw submitted values in a bounded, versioned pending envelope. On reading storage
it checks byte/child-count bounds before constructing command collections, rejects
unknown interpretations and requires the original frozen representation. Accepted
bytes come from the existing owner `ProjectDefinitionOperationCodec`; no second
canonical accepted-result writer or normalization policy was introduced. Validation
binds operation and kind, original project/lifetime and revision advancement,
every phase slot, retained child IDs, and option omissions only for blank rows.

`Projects.UI/ProjectPendingSubmissionContracts.cs` owns browser-port types and its
interface. UI has no dependency on Web, Infrastructure or Projects implementation.
`ProjectPendingOperationSession.cs` owns only a captured operation's in-memory
orchestration and explicit one-shot reconciliation. Owner dispatch is a typed
delegate; storage remains a real injected boundary. There is no database access,
JavaScript, automatic restart dispatch, token replacement or starter creation in
the session.

`Web/Infrastructure/BrowserProjectPendingSubmissionStore.cs` implements the store
with private JavaScript wire DTOs and the contract-safe owner codec. JavaScript
stages/statuses map through closed switches; epochs cross JS as canonical decimal
strings while frozen owner JSON stays opaque. Returned records are checked against
the requested operation and decoded typed command. Accepted payloads and closed
absence evidence are checked before becoming typed recovery state. Scoped pages
reject foreign metadata or continuations. The host still obtains current server
authorization before listing/reading a scope: browser keys are not authority.

Projects registers its native adapter and owner codec; Web registers the scoped
browser store, its submission/reference interface aliases and explicit capacity
limits (64 records, 16MiB aggregate logical payload, 16 records/page). The existing
browser-state behavior is unchanged. The production store has no package, CSS,
memory fallback or localStorage path.

Browser schema one explicitly requires `ProjectDefinitionLimits.VersionOneMaximum`
for retained request/receipt decoding. Tightening current deployment admission
limits must not make older valid pending operations unreadable or their accepted
receipts unrecoverable. The host freezes genuinely new submissions with the
current write-context limits before preparation, and the owner enforces current
limits for new admission. Store count/aggregate/page bounds remain separately
configured browser capacity controls.

For authoritative ClosedAbsent, `RetainedUntilUtc` is the minimum guaranteed
retention promise. A lookup after that timestamp can still prove absence while
the epoch is above the owner's retired watermark and therefore fully retained.
The adapter checks ordered closure/observation and closure/retention timestamps;
it does not manufacture Expired from the browser clock or minimum promise.

## Outcome and lifetime behavior

Only an Applied durable dispatch claim containing the exact Dispatching stage and
new attempt identity reaches the owner delegate. Cancellation,
retirement, failed storage and Unchanged/Changed/Missing claims cannot authorize a
call. The exact frozen submission is the delegate input. Disposal does not clear
the pending record or assert that an in-flight owner operation was canceled.

Known owner acceptance is retained before browser persistence and reconciliation.
If storage fails, that in-memory acceptance remains and further dispatch is
blocked. A late accepted result may update its own operation evidence after the
session retires, but cannot reconcile that retired editor. Restarted local accepted
bytes remain historical until an authorized lookup verifies the owner result.

Reconciliation is explicit and attempted at most once per session, including when
its callback throws. Replayed receipts never trigger a second callback or starter
effect. Clearing accepted recovery data requires successful reconciliation and an
exact persisted revision. A failed owner dispatch plus failed uncertainty storage
preserves both exceptions. Earlier uncertainty, inconclusive lookup, expiry and
caller/dataset changes do not become a known rejection or new operation identity.

## Focused source cases

Counts are source-derived; run discovery against the final build before recording
results.

| Assembly / filter | Expected cases | Scope |
| --- | ---: | --- |
| Unit: `FullyQualifiedName~ProjectDefinitionRecoveryCodecTests` | 8 | All three request kinds, Int64 identity/raw whitespace, exact existing canonical receipt bytes, blank-option omission, child mapping/identity/coverage, original expectation advancement, corrupt/unsupported content, pre-materialization child counts |
| Unit: `FullyQualifiedName~BrowserProjectPendingSubmissionStoreTests` | 9 | Opaque request/string epoch, accepted preflight before JS, corrupt stored acceptance, disposal/late read, unsupported schema fail-closed, scoped metadata/continuation refusal, authoritative absence beyond its minimum retention promise, metadata-only inventory/disposal without body decoding |
| Lightweight Projects.UI: `FullyQualifiedName~ProjectPendingOperationSessionTests` | 13 | Failed prepare, three nonapplied claim outcomes, malformed applied claim, retirement during claim, acceptance-before-storage failure, late owner acceptance, authorized restart recovery and one-shot replay, uncertainty after rejected retry, failed reconciliation, caller mismatch, combined owner/storage errors |
| Playwright: `FullyQualifiedName~ProjectPendingSubmissionBrowserTests` | 13 | Real IndexedDB transaction/race/restart/quota, cross-binding evidence-disposal and canonical key-terminator proof described in the separate browser-store record |

Recorded parent execution, verified from retained TRX results:

- `p02b-foundation-unit.trx`: all **8 RecoveryCodec cases passed**. This is subset
  evidence only; that historical combined lane had unrelated failures and must
  not be represented as a wholly passing lane.
- `p02bc-repairs-unit.trx`: all **9 BrowserProjectPendingSubmissionStore cases
  passed**, within the passing 38-case repair lane. The earlier seven-case store
  result predates the two reference-inventory/disposal cases.
- `p02c-reference-ui.trx`: **13 PendingOperationSession, 7 EditorOperationSession
  and 1 capture-policy cases passed**. Its three new reference renderer failures
  were fixed and passed separately in `p02c-reference-ui-fixed.trx`.
- `p02bc-browser-repair.trx`: 10 of the 13 standalone IndexedDB cases and the
  native recovery case passed; three standalone cases failed. The native test
  proves actual owner acceptance despite a browser accepted-record write failure,
  restart with the original operation, authorized historical receipt reconciliation
  and a separate current-definition read. The original failed browser lane remains
  recorded; it did not exercise the intended native persistence fault.
- `p02bc-browser-final-four.trx`: all four selected cases passed, zero skipped,
  in 30 seconds: the three remaining store repairs and the native case after its
  comparison heading became `Your draft`. Together these retained selections
  cover all 13 store cases and the native case without presenting the initial
  mixed-result lane as passing. The refreshed 1920×1080 screenshot was visually
  reviewed, including historical/current separation and the corrected heading;
  exact evidence and verified host/lease cleanup are in
  [the native editor record](P02c-native-editor.md).
- After the last copy correction the parent reports UI/Web direct Release builds
  passing in 3.23/29.96 seconds, no-write portability enforcement passing
  (36,268 findings / 15,439 allowances) and rendering validation passing 1,195 inputs.

No fixture, host, browser profile, database or external effect was created while
preparing these sources. The executed native case verifies protected writer dispatch,
exact receipt recovery and historical/current separation with the explicit restart
limitation for later typing and starter drafts. Component/owner proof remains separately
recorded for context admission, lifecycle and other transitions; the adapter unit
tests alone do not certify the whole application or unfinished owner/client cutovers.
