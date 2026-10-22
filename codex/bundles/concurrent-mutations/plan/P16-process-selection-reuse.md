# P16 — Reuse unchanged Process selection publication

Status: design checkpoint. Implement only after the repeated cost proof and the scoped
architecture record are sealed. Recovery remains disabled by default. SPA stays on its
separate branch.

## Evidence and decision

Three alternating native pairs on signed pre-index production `4a35d5311` and current
`cbff4ac4f` preserve all 600 accepted writes and 600 page reads. Median throughput falls
from 9.8798 to 8.6237 writes/second, a 12.714% loss. Median write p95 changes from
136.5832 to 150.0260 ms, +13.4428 ms / 9.842%. The original throughput investigation
threshold remains exceeded; this is not accepted capacity. Full samples and failed
diagnostic attempts belong in the controlled-cost validation record.

Existing ActivitySource diagnostics, including fixture and warmup, record 740 versus
863 JSON publications: exactly 123 additional compact publications. Their aggregate
inclusive write duration changes from 13812.04 to 15337.48 ms. Compact serialization
itself totals 7.25 ms. Selected-key and workspace-reacquisition totals are similar.
This attributes a useful target to repeated publication, not a precise disk-flush or
initial workspace-wait breakdown. Nested durations are not additive; the listener run
is separate from the six capacity samples.

Retain a compact complete selection projection, but stop physically replacing it when
an existing run changes only status, progress, accounting or other non-membership data.
Keep every business journal, durable flush, sorted authority key and publication guard.
Do not introduce a count-only witness, process-wide cache, reader repair or bypass switch.

## Boundary ownership and dependency direction

`FileProcessExecutionIndex` already owns selection construction, completeness validation
and publication. Keep that responsibility there. The existing execution slice owner
assigns the selection revision as part of its original immutable target index. The chat
projection owner carries that target stamp; it does not decide mutation authority.
`FileSandboxWorkspaceJsonStore` retains generic exact-content write suppression, with an
explicit compact-serialization option. No application, UI, Process, EF or provider
dependency enters this Persistence implementation. No project reference, registration,
interface, service locator, new partial or nested runtime owner is justified.

This is a local protocol change within existing owners, not a class extraction. Existing
large store classes receive only field propagation at their current publication points;
selection policy must not move into the facade. Use direct tests of the selection owner
and its pure revision decision, plus native owner integration. No unrelated monolith
refactor is bundled with the performance repair.

Scoped CodeAnalytics baseline: `snap-20261022215400-198deb42`, 148 types, 1530 members,
55 findings and the existing HistoricalReadStore/HistoricalReadPass containment cycle.
Ten existing NuGet-advisory MSBuild load diagnostics are marked errors. The separate
production build succeeds; this snapshot is not a clean whole-repository certificate.
Require unchanged dependency direction and no new cycle after the edit.

## Canonical selection revision

Add an optional `ProcessSelectionRevision` to the internal canonical execution index
and its chat projection. Omit absent values when serializing so legacy prepared payloads
keep their original shape. It is a nonnegative canonical revision no greater than the
ordinary execution revision. It is selection completeness evidence, never authority,
an execution outcome, a business CAS token or a replacement for original receipts.

For newly prepared existing-run updates, initialize an absent stamp from the new ordinary
revision. Preserve a present stamp only when the original and target ProcessRunId are
identical; an identity change advances it even when all counts stay equal. Creation and
deletion advance an existing stamp. A full-state publication uses its new ordinary
revision. Other legacy publication paths may retain the original exact-global-revision
binding until a subsequent prepared write initializes the optional stamp. Session-only
changes preserve membership; the existing session-count completeness check still applies.

The chat target carries the exact stamp from the canonical target. Every constructor,
rebuild, session update, deletion and retained-journal publisher must be audited. A source
projection/canonical stamp mismatch is unavailable evidence. Do not infer a stamp from
counts or a file timestamp. Unknown historical Process lineage stays an overinclusive
candidate; refinement of that derived metadata may change the compact bytes even when
the original run's Process identity did not change.

Compact schema 2 binds its revision to the canonical selection revision when present,
otherwise to the ordinary canonical revision. Schema 1 keeps its original strict global
revision rule. Reject unsupported schemas, invalid stamps, unsupported projection versions,
missing counts, duplicate identities and invalid selected original records as before.
Always read selected original run records afresh. Canonical runtime revision changes do
not make an unchanged membership projection stale.

Sort compact entries by execution identity so display-order changes cannot cause an
unnecessary write. Serialize the complete target compactly and compare actual persisted
content before skipping publication. Missing or different bytes require the usual atomic
durable write. Corrupt derived JSON is replaced from the already validated original target,
as the existing publisher does; comparison must not turn it into a new authority source.
This still incurs O(history) construction/comparison and adds sorting cost. Measure the
allocation and large-history tradeoff rather than claiming a history-independent index.

## Original intents, crashes and compatibility

Retained source and target indices include the optional stamp when originally prepared.
Replay uses that exact target, never the current revision. Legacy source/target pairs with
both stamps absent may validate against the historical unextended target; a new source
with a present stamp may not downgrade its target to the legacy form. All other existing
target validation stays exact. Add explicit before/after-canonical-write recovery proof.

The canonical index and chat target remain published under the current original journal
and workspace exclusion. Pending journals continue to make page reads pending. A crash
between canonical/chat/compact writes cannot authorize exhaustion from mismatched evidence.
Stable membership leaves a valid compact projection, but does not bypass the pending
journal check. Membership changes invalidate the old compact stamp even with unchanged
counts. A missing/corrupt compact file must still be recovered, and failure to publish it
must retain the original journal. Reads never repair.

Older writers that do not understand the optional fields retain the legacy global-revision
protocol; they cannot reuse a newer compact projection without matching its version rule.
Test legacy prepared targets explicitly. This change does not claim protection against
manual partial filesystem restores outside the governed restore protocol.

## Testability and acceptance

- Pure selection revision tests: stable identity, changed identity at equal counts,
  absent legacy stamp, malformed revision, and no downgrade from a present source stamp.
- Native stable update: compact bytes and a deliberately distinct last-write timestamp
  remain unchanged, while a page hydrates the newly accepted original run revision.
- Native identity change: old Process membership disappears and new membership appears
  without changing the run count. A count-only shortcut must fail this case.
- Native new run, deletion, session and bulk publication; missing/corrupt compact bytes;
  current and legacy original-journal recovery before/after canonical publication.
- Existing 23 compact-index, 18 page, 12 query and nine publication-recovery cases remain
  the bounded regression selection. Add exact discovery counts for new cases before runs.
- Repeat the same three alternating tiny-history pairs and the 2000-history 1/8/32-writer
  workload. Preserve the 10% throughput and 15% p95 investigation thresholds, every failure
  and accepted-revision assertion. Listener attribution must show fewer physical writes.
- Check 20k/100k metadata cost separately. Do not run a load generator during measurements,
  invent a cold-cache claim, or infer WAL/server CPU from client time.
- Build the changed production owner and consumers in standard Release, freeze owning and
  copied outputs, run full portability with no-write final enforcement, and reconcile all
  owned fixtures. Broad Stable remains the final integrated gate.

Entry permits only this measured local repair. HTTP/status cutover, contended cancellation
progress, policy refresh and the rest of the original bundle remain separate open gates.
