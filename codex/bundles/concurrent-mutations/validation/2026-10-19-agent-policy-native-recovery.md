# Native Agent policy retained-save checkpoint

The existing native Agent policy editor now retains its original command before
dispatch and recovers historical acceptance after a browser reload without resending.
All **39 selected behaviors have passing evidence**: 38 passed on the first frozen
candidate, and the remaining native journey passed after two test-only repairs.
Exact source comparison confirms the production code and all other tests are unchanged.
The failed attempts remain retained. This closes the existing-Agent native browser
portion of [the recovery design](../plan/P07-agent-policy-recovery.md), not all R5/E2E work.

## Behavior and boundaries

AgentFramework.UI owns typed pending records and pure editor state. The module's new
editor session owns preparation, one dispatch, known-result retention and read-only
receipt lookup. The Razor dialog delegates those operations and owns its read lifetime.
Web implements the storage port with IndexedDB and the existing interactive identity
provider, including its explicitly trusted local identity when API authentication is
disabled. Anonymous untrusted callers fail before storage access. Browser awaits recheck
caller identity; the record itself never grants backend permission.

The key is caller/profile/scope/Agent. The original generation and exact decimal revision
remain inside the immutable payload. Strict transactions complete before acknowledgement.
There are at most 128 records, 8 KiB per request and 4 KiB per result. Prepared records
may be discarded explicitly; only the atomic Prepared-to-Dispatching transition grants
one live dispatch. Dispatching records cannot be discarded or resent. Missing receipts
remain unresolved, even if current values happen to match the submission.

Known acceptance/rejection is recorded before conditional removal. Every transition
compares the full original command. Late acknowledgements cannot clear a newer operation,
and known follow-up warnings are retained. Storage failure blocks further saving without
a memory-only fallback. A fresh editor restores submitted values; later typing survives
acknowledgement, recovery and current-state reads. Historical acceptance requires an
explicit current read and Use current/Keep draft before the next save.

Browser record work is bounded and transactions contain no backend/network await.
The store serializes only the selected bounded record and a count for capacity; it does
not enumerate or serialize every retained operation per save. No new backend lock or
receipt retention rule was introduced. Sustained capacity remains a separate gate.

Native entry for a deleted Agent and production-host restart at the same browser origin
remain unproven. The backend lookup's deletion/fresh-owner coverage is supporting proof,
not a substitute for those native entry/lifecycle variants. Process/Workflow policy
activation and wider owner coverage remain open.

## Validation

Discovery matched 12 UI-state, 23 component/adapter and four browser cases. The exact
filters are recorded in each candidate's `c4-resource-families-v5.json`. Four changed
production targets (UI, module, Web, sandbox) and three owning test projects built in
standard Release using local libraries. The sandbox retains its existing Parity asset
mode. Existing compiler/NuGet warnings remain.

Component proof includes storage failure before dispatch, lost dispatch acknowledgement,
restored generation, unknown outcomes, explicit cancellation of unsent submissions,
terminal-result cleanup failures, preserved follow-up warnings, later typing and a
newer operation surviving an old acknowledgement. Codec/adapter proof includes exact
large revisions, malformed/foreign records, caller changes and local identity selection.
The browser protocol proves cross-tab atomic transitions, reload, bounds, isolation and
conditional removal. Existing native conflict/rebase and sandbox journeys also passed.

The new native journey uses the real policy owner and receipt lookup. One tab is held
after durable Dispatching but before the owner call; another tab cannot dispatch and
NotObserved does not unlock it. A browser result-persistence fault follows acceptance.
Another writer then changes current policy. Reload restores the original submission;
lookup displays its historical three-minute value while preserving a six-minute live
draft and comparing the current two-minute value. Current revision stays unchanged
through recovery, proving no second write. The strict browser oracle passes.

The first candidate was 38/39: its fault route missed the fingerprinted static asset.
The next attempt reached all behavior assertions but failed the strict browser oracle
because two raw navigations had not registered their disconnect beacons as owned
teardown. The final test uses the existing navigation helper for one full document
replacement and records its single expected beacon. No error filters, production code,
asserted mutation outcomes or race deadlines were weakened. Only this one test changed;
the two repair candidates each discovered and executed that exact case.

The final 1920x1080 screenshot was inspected: historical acceptance, editable draft and
current comparison are distinct, all fields/actions are readable, the footer is visible
and no horizontal overflow occurs. The existing wide dense Dialog, Alert, Button and
Cluster composition remains; no custom layout/CSS or small-screen work was added.

Portability rescanned each final candidate including new files and enforced **15,615
reviewed findings unchanged**, without writing the baseline. Rendering-register review,
documentation and diff checks pass. No unfiltered suite was run; broad Stable remains
historically incomplete and is not claimed green.

## Architecture gate

Status: **Pass for the bounded native recovery increment**. No project references,
production partials, global coordinators or Projects-specific pending contracts were
added. Pure state, orchestration, wire decoding and browser persistence have separate
owners. The same module session is exercised through bUnit without constructing the
original runtime and through the real native browser producer.

CodeAnalytics `snap-20261019153057-0f8dc3b2` to `snap-20261019155634-0f8dc3b2` reports
three projects, 92 to 104 types, 1,356 to 1,453 members and 75 to 76 DI registrations.
The dependency query confirms Web to module to UI, with zero cycles. The only new finding
is informational: the browser adapter has 23 members. The existing pure editor-state
finding now counts 39 members; these remain one editor lifetime, not a new runtime
manager. Diagnostics remain 219 (26 DI0001, four DI0002, 189 WS0002); one pre-existing
Program diagnostic moved a line. Existing workspace-load blocking status remains,
while the actual affected builds pass.

## Proof identity and resources

All candidates inventory 9,410 source and 5,075 runtime inputs, verify unchanged inputs
after execution, and release their freezes before edits. The final candidate is
`c4-agent-policy-native-navigation`:

- Source SHA-256: `7FB28B4FB11585081720BD6F8383DCA3A8E750D9D412205C73C4072562C50903`.
- Runtime SHA-256: `F1981994BCE56DE5A7A207498DC33343BA04AF863C6A5C87341660D1C2B83504`.
- Compact proof: `70434BFB0946BDF4478C6ADA155633569776C5E10335A3A74DB5A1F2BD0C32A2`.

Earlier compact proofs are `c4-agent-policy-native.json`
(`2E527143B86E2D18F65D505E9E3233315222CCC57406EA12CEA5055835B48E5A`) and
`c4-agent-policy-native-ready.json`
(`94FF5BC55CC278256CF113A968E2CE7B8DD259A619A5655DFA7C9A5660412994`), retained under
`proof/2026-10-19-planning-authority`. The final proof includes the exact source bridge,
architecture delta, screenshot hash and browser teardown evidence.

All observers cover their complete test commands. Each candidate has zero remaining
observed processes and zero new database delta; 12, six and four unattributed TEMP
entries respectively were preserved. The existing isolated PostgreSQL fixture is reused
for continuing work. Older blocked cleanup remains in [resources](../resources.md) and
was not retried or bypassed. Later documentation/commit metadata is outside the executed
identities. SPA remains untouched on `codex/concurrent-mutations`; no merge or push.
