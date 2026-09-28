# Plugins implementation review and bounded carry-over

Reviewed branch: `components-decoupling` at
`0e176a3b99270cdc9a86a57d6276d05979d7352e`. Previous reviewed baseline:
`dd050d5a1489537207e073cac0838f40cde4340f`.

## Verdict

Accept the implemented architecture and main fixes; require the two corrections below before
starting Scheduler implementation. They do not justify another architecture replacement or
a fixes-only handoff. This is a source and test-source review, not a reviewer-executed .NET,
PostgreSQL or browser run. Source IDs resolve in [SOURCES.md](SOURCES.md).

The new `Plugins.Presentation` project is useful rather than gratuitous: production and the
scenario owner run the same `PluginsWorkspace`, request lanes, draft registry and operation
policy. The host adapter retains real services/effects. Keep that direction. The renderer
now uses bounded owner-held drafts and typed actions; it does not obtain an EF/runtime owner
through a facade. Source: PL01–PL14; recorded graph/asset evidence: EV02.

| Earlier requirement | Current source observation |
| --- | --- |
| Preserve unblurred input and unrelated drafts | Settings controls capture text through oninput; stable editor instances remain in the registry rather than being replaced on every refresh. PL03, PL07, PL09. |
| Preserve real ID before readback | Save captures a request; accepted ID/token are applied before the secondary settings read. Later field edits survive reconciliation. PL01, PL02, PL07. |
| Distinguish known success from unknown | Typed receipts track committed values and package/OAuth progress; unknown operations block replay and need explicit review. PL02, PL06, PL08, PL13. |
| Correct conflicting grant busy states | One typed full grant target includes plugin/capability/recipe/scope kind/key. Pending operations protect the same target. PL01, PL08. |
| Fence stale reads and effects | Seven read lanes own generations/cancellation; A/B/A selection and browser effects are covered in test source. PL01, PL04, PL05, PL16. |
| Real upload and runtime effect seams | Actual InputFile remains mounted while reading, bounded stream lifetime belongs to host/workspace, packages and restart stay in their implementation. PL01, PL12–PL15. |

Keep the existing tests, docs and source-mode boundaries. Do not reopen previously accepted
TestLab or Collaboration work without a demonstrated regression caused by S0/Scheduler.

## PL-R1 — nonempty catalog loses its navigation when the selection vanishes

**Severity:** functional P2; definite reachable source control flow. Not browser-reproduced by
this reviewer. **Sources:** PL04 (`CatalogAsync`), PL08 (`SelectedPlugin`), PL12 (list/detail
conditional), PL01 (`RefreshAsync`/`SelectAsync`).

The catalog accepts a replacement list but only initializes the selected ID when it is null.
The full `ListDetailShell`, including `PluginList`, is conditional on resolving that selected
ID in the current catalog. For A/B → B, the selected A remains non-null but unresolvable.
The catalog is not empty and the detail cannot resolve: neither branch renders a selectable
list. Repeated Refresh does not clear the non-null A. An uninstalled descriptor disappearing
from a source is enough; do not confuse this with installed-but-unavailable placeholders.

### Required reproduction and correction

Start a controlled owner with A/B; select A, retain a dirty or unresolved A draft, then return
B only on a successful catalog refresh. Render the actual top-level workspace and demonstrate
that B cannot currently be selected from markup. Correct the navigation: keep a useful list
independent of the missing detail and show explicit missing-selection/recovery state.
The user must be able to choose B without a page reload or a synthetic write.

Do not silently attach A's editor, pending/unknown receipt, permissions or effects to B.
An automatic selection policy, if chosen, must be explicit, generation-aware, non-destructive
and tested; the preferred simple outcome is a selectable list and missing detail. Retained
A data must still have an intentional recover/discard path. A later reappearance of A may
reuse only its own valid retained context, not a successor with the same display name.

Assertions: B visible/selectable; current data describes B after explicit choice; A draft
and unresolved operation unchanged; zero replay/owner writes; empty→repopulation usable;
stale earlier catalog success/failure cannot hide newer selection; late A effects cannot
open/populate B. Include at least a top-level bUnit case and a focused real browser journey.

## PL-R2 — a second cleanup failure can destroy an already-known package stage

**Severity:** P2 fault-path correctness with replay risk; conditional on two filesystem
failures. Not reproduced against a real filesystem by this reviewer. **Sources:** PL15
(`ExtractInstalledPackageAsync` and `InstallArchiveAsync`), PL13 (owner receipt mapping),
PL08 (replay policy). Existing integration test source: PL19.

Replacement marks `replacementStarted = true` before removing an existing installed package.
If that deletion or the subsequent move fails, extraction enters its catch. It attempts
recursive temporary-directory deletion **before** wrapping the original failure with
`PluginPackageStageException(ReplacementStarted)`. If temporary cleanup also throws an I/O
or access error, that new plain exception escapes instead. The outer package error handler
can then classify the attempt as invalid/Refused, and the owner adapter returns no stage.
The presentation policy allows a new operation after Refused. This loses the fact that an
irreversible replacement step began, whether or not the old installation was fully removed.

### Required reproduction and correction

Exercise two controlled faults: failure after replacement starts, then failure while cleaning
the task-owned temporary directory. Observe through the real extraction/installer and owner
adapter. A deterministic narrowly scoped fault seam is acceptable; do not rely on arbitrary
sleeps or platform administrator permissions. The expected result is an original-stage-aware
partial/unknown outcome with exact package/plugin identity, preserved primary failure and
safe cleanup diagnostics. It must not claim Installed or clean Refused. Propagate that receipt
through the shared workspace and prove replay stays blocked until explicit review.

A cleanup helper may be factored locally, but not into a generic filesystem or compensation
framework. Preserve best-effort cleanup, byte/path/archive validation and isolation. Never
return success merely because the cleanup error is suppressed. After recording test evidence,
remove only the owned fixture residue; a refused cleanup should not contaminate later tests.

Counterexamples to keep green: invalid ZIP/oversize/read failure before any installation is
Refused; extraction logger failure preserves Extracted; installation persistence and restart
metadata/logger failures preserve their known stages; a normal upload/install succeeds. The
existing tests demonstrate several of these cases but not the double-fault path (PL19).

## Scope and proof economy

Do not bundle unrelated cosmetic changes, database policy or new concurrency protocols into
S0. First get failing-first evidence, then the fixes and focused owning regression evidence.
A later code drift that already fixes one finding may close it with a current test and clear
source evidence; do not introduce a gratuitous edit. Continue with Scheduler after both
findings are resolved or honestly characterize any genuinely external proof blocker while
completing independent work. Do not label an unresolved product failure closed.

EV02 reports 160 distinct Plugins/consumer cases and 21 TestLab entry cases. EV04 reports no
GitHub Actions runs for the reviewed HEAD. Raw implementer TRX/browser/graph evidence lives
in ignored local directories and was not supplied here. Therefore neither the old counts
nor the source of a test is fresh reviewer execution. See [PROOF_STATUS.md](PROOF_STATUS.md).
