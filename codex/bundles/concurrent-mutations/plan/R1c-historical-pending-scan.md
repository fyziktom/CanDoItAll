# Historical pending-publication scan without a session-count cutoff

Status: parent approved the source-only amendment after the UTF-8 experiment closed.
The managed candidate is authored: four production files, five new Integration
Facts and the existing six-Fact historical suite's obsolete entry-limit expectation.
Runtime peer released the historical store and the exact Core status/map lines.
Independent runtime-peer source review found no blocking issue. Parent review
requested one shared semantic-journal-name array; that amendment is included.
No build, runtime, filesystem-capacity or
activation proof is claimed here.

The current `FileSandboxWorkspaceHistoricalReadStore.HistoricalReadPass` stores all
immediate entry names, then refuses after 4,096 entries. `execution/sessions` is a
flat directory of legitimate chat files, so unrelated retained chats can make an
otherwise readable historical run unavailable. Increasing that array limit merely
moves the compatibility failure and retained-memory cost.

Replace that inventory with complete streaming scans of the exact requested target
set for each parent. Before the first scan, group the four semantic journal paths,
catalog, workspace index, execution index and requested run. Add the selected chat
after its identity is read; its parent is separate. A newly added target rescans the
complete known set in that parent; a prior scan never certifies a later target.
The final pass starts fresh with every target observed by the first pass. Retain
only target paths and completed-parent state, never unrelated entry names.

Use `FileSystemEnumerable<string>` with a span predicate that checks cancellation
and elapsed budget for every visited entry, including nonmatches. Transform only
an exact matching pending name into a path. Preserve no recursion, no inaccessible
entry suppression and no hidden/system attribute filtering. Missing parents retain
the current missing-layout semantics; failures during a started scan refuse.
The existing `DurableFileWriter` naming predicate gets a span overload using the
same grammar: dot + target + dot + 32-character GUID + lowercase ordinal `.pending`.
Target comparison remains the explicit caller comparison; no second parser exists.
The [BCL predicate API](https://learn.microsoft.com/en-us/dotnet/api/system.io.enumeration.filesystemenumerable-1.shouldincludepredicate?view=net-10.0)
and [runtime implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/IO/Enumeration/FileSystemEnumerator.Windows.cs)
support filtering before result-string creation. Native directory reads remain
synchronous: cancellation and deadlines are cooperative, not hard OS timeouts.

Each public store call owns one five-second budget, linked to its caller token and
shared across all parents, payload reads and both scans. The existing Core page's
five-second budget still limits the entire page, including its two store calls and
authority/owner reads; it is never reset by a store. `TimeProvider` is an internal
constructor dependency for deterministic elapsed-time proof, with `System` used by
the public constructor. External cancellation propagates. Store budget exhaustion
returns a distinct `ReadBudgetExceeded` storage status; the run-page mapper returns
`CooperativeBudget` with no observations/cursor. Catalog non-ready handling remains
the existing `ReadUnavailable` path. No absence or successful page can be inferred
from partial enumeration. Missing selected-file checks still follow pending checks.

The exact run-byte witness, index before/after check, safe-path checks, current
authority, four semantic journals and optimistic concurrency limits are unchanged.
There is no directory snapshot guarantee, cross-request cache, restart cursor,
cleanup/recovery action, writer index, new storage layout or filesystem watch.
The independent DatasetActivation directory cap is outside this flat-session slice.

## Authored scope and prospective execution

Production edits are limited to the historical store, its Core storage-status enum
and run-page mapping, and the DurableFileWriter span overload. Tests use the existing
file-backed admission fixture and owned cleanup; no database or new test host.

The six existing historical Facts remain, replacing only the obsolete 4,097-entry
refusal expectation. Five new real-filesystem Facts cover: more than 4,096
valid unrelated chats; selected pending evidence among a large directory plus
unrelated/malformed pending names; pending publication introduced between passes;
external cancellation during nonmatching enumeration; and one shared elapsed budget
across the before/after phases. The new class is
`CanDoItAll.Tests.Integration.Runtime.HistoricalPendingPublicationScanTests`:
five plain Facts, no theory expansion, skip, inheritance or category exclusion.
The existing historical partial contributes six plain Facts. These source-derived
expectations are not execution proof. The pending case populates the large directory
before publishing the selected marker; no deterministic filesystem traversal order
is claimed. The complete positive large-directory scan and selected-marker refusal
are separate assertions.
Assert no file changes from reads, no successful/cursor result on interruption and
an immediate later successful read. The scanner's `using`/`foreach` unwind owns
enumerator disposal; successful fixture cleanup is not falsely described as an
independent native-handle assertion on every OS.

Retained scanner memory is proportional to the small requested target set plus the
BCL enumeration buffer. Scan work remains proportional to visited directory entries,
with no claim of constant latency. Before activation, measure real large directories,
allocated bytes, elapsed time and cancellation response on supported filesystems;
the five-second compatibility refusal remains explicit. A sublinear directory lookup
would require a separately reviewed writer-maintained layout/index change.
