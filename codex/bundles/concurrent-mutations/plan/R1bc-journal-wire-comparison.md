# R1bc C: exact journal wire equality without two retained JSON strings

Status: the approved three-file C candidate and five Facts are authored and independently
source-reviewed, but remain unbuilt and unexecuted. See [the source proof](../validation/R1bc-journal-wire-equality-source.md).
The approved architecture predecessor was `3E2F15B792E81CF9E3B97CBCDE44ABE5C2558157A74392B5640C0F60DBB18459`.
B remains an independent frozen experiment under parent validation; its early results
have not established an allocation improvement. C does not call B's size helper or
change its files, the historical graph, C3, any run/journal limits or the matched
22-row performance workload. Runtime and capacity gates remain open.

## Evidence and actual call path

The accepted whole-host diagnostic is
`r1bc-explicit-gc-diagnostic-v2/analysis3/report.json`, SHA-256
`3E4CDEFC8D5E14193B6A35A74074CFA8EE01DCBF7D5345643324B2C19C86A4BC`.
Its three displayed allocation stacks through `AgentToolJournalTransitions.Equivalent`
contain six allocation ticks, weighted at **248,537,168 bytes**. Every displayed stack
continues through `RequireEffectCheckpoint` into the real owner update callback and a
fresh `LoadRunDetailAsync`. The allocated stack leaf is
`JsonReaderHelper.TranscodeHelper`. These are sampled whole-host allocations from the
pre-A/B diagnostic, including setup, failed workload and cleanup. They are not an
exact measured-operation total, retained heap attribution, a current B profile or a
capacity result. The other displayed allocations remain separate costs.

Current source anchors:

| Source | Required behavior |
| --- | --- |
| `Core/ToolPolicy/AgentToolJournalTransitions.cs:8-32,267` | Validate proposed first, then current; compare complete default serialized JSON only when their journal revisions match; then continue provider, segment and proposal transition checks. |
| `Core/ToolPolicy/AgentToolAdmissionJournal.Effects.cs:337-352` | Compare the pinned admitted journal with the freshly loaded checkpoint journal after the exact run/session/lease/approval/current-read fences. Their references normally differ. |
| `Core/ToolPolicy/AgentToolAdmissionJournal.cs:321-344` | Ordinary owner mutation also validates a journal against an updater result; some no-op results retain the same journal reference. |
| `Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs:1438,2950` | Whole-state replacement and existing-run persistence use the same transition validator. They must receive identical behavior. |
| `Models/Execution/AgentToolJournalModels.cs:209,221-224,381` | The identity-keyed weak table records successful complete validation only. B changes encoded-size counting, not this cache or semantic checks. |

Both main and managed `AgentToolJournalTransitions.cs` currently have SHA-256
`6E0D0A9D5C7E0C05B3E2D5CC4E8E591DE14A234D886FCDEC8CD266B080EDE52F`.
The accepted A witness stays private to one Persistence update. It does not make the
pinned Core journal and a later checkpoint decode the same object.

## Minimal selected shape

Keep `Equivalent` at its existing private call site. After the existing validations,
return true immediately for `ReferenceEquals(left, right)`; otherwise delegate to a
small internal, journal-specific Core helper, `AgentToolJournalWireEquality.AreEqual`.
There is no early return from `ValidatePersistence`: all later transition checks stay.
The helper does not call `Validate`, normalize, cache, inspect files, or accept options.
It does not expose a public or serialized witness.

The helper serializes **left first** with the typed default
`JsonSerializer.SerializeToUtf8Bytes(left)`. It then uses the synchronous typed default
`JsonSerializer.Serialize(comparisonStream, right)`. The private write-only stream
compares each actual output span against the corresponding left slice and retains a
sticky mismatch flag plus a checked `long` total. It returns true only after successful
complete right serialization, no mismatch and equal final lengths. A longer right
output is unequal without slicing beyond the left buffer; a shorter right output is
unequal even if all observed prefix bytes matched. Empty writes do not change state.

A mismatch must **never** throw or terminate serialization early. Keep traversing and
counting the right output so a later serializer failure remains visible, as it does
when both strings are produced before comparison. `Write(byte[], offset, count)` must
validate the actual arguments before delegating to the span overload. `WriteByte` must
share the same comparison/count semantics. `Flush` performs no work. Reading, seeking,
length/position access and resizing are explicitly unsupported; no hidden stream
fallback, asynchronous path, file spill, worker thread or background producer exists.

The root reference shortcut belongs only in the existing post-validation `Equivalent`
path. The lower-level wire helper should serialize its inputs even when their references
are equal, so its pure serialization/error contract is independent of whether a caller
has already validated them. Its internal non-null arguments may be explicitly guarded;
the public transition API's null behavior remains unchanged:

- null proposed/current validates neither and returns;
- null current with non-null proposed validates proposed then returns;
- non-null current with null proposed validates current then rejects replacement;
- two non-null values validate proposed before current, including failures;
- invalid same-reference values still fail validation before any shortcut.

A valid previous journal in the weak table does not validate a new `with` copy. Keep
that boundary, every semantic validation, every exception message/code and current
schema acceptance intact. No change to the CWT's object marker, key, lifetime or
successful-only insertion is needed. No semantic digest, revision, timestamp, file
mtime or hash is accepted as exact equality evidence.

## Exact wire semantics and alternatives

The [pinned .NET 10.0.12 string path](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonSerializer.Write.String.cs#L137-L150)
serializes UTF-8 and transcodes the written bytes into its result string. Comparing the
same serializer's exact valid UTF-8 output therefore preserves the current string
comparison, without introducing semantic JSON equivalence. The
[synchronous Stream implementation](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Metadata/JsonTypeInfoOfT.WriteHelpers.cs#L232-L311)
uses the same typed contract and default options while flushing completed chunks.
Neither call supplies Web options, a custom encoder, a source-generation context,
`object` as the root contract, a comparer or a new JSON version.

Preserve ordering, enum numbers, escaping, timestamps including offsets, computed
serialized properties, conditional null/default omissions and array order. In particular,
`ProviderDispatches=default` versus an empty immutable array and default versus empty
runtime attachments have different wire presence even where separate semantic helpers
normalize them. The journal's computed `HasUnresolvedEffects` and the workspace scope's
computed public properties also belong to the existing wire. Raw string comparison
must not be substituted for the encoder's handling of malformed UTF-16.

| Alternative | Assessment |
| --- | --- |
| Reference shortcut alone | Small and safe after validation; helps retained-reference no-ops but misses the observed pinned-versus-fresh checkpoint. |
| Record or handwritten deep equality | Record equality is not a complete array comparison. A new deep walker duplicates the wire schema and must correctly track every omission, computed property, timestamp and encoder edge; omitted future fields could silently permit stale changes. Do not introduce it in this slice. |
| Two UTF-8 result arrays | Simpler than a comparison stream and removes transcoding, but retains two full results and both aggregate-output allocation paths. The one-sided comparison is a small further local change. |
| Cached JSON bytes in the validation CWT | Retains up to 24 MiB per live journal in addition to its graph, contrary to the memory objective. Do not add it. |
| Cached digest or pair result | Digest-only positive equality changes the exact comparison contract; a pair cache normally misses newly decoded checkpoints. Do not add either. |
| Concurrent serializer pipelines or file spill | Adds scheduling, cleanup and ownership complexity to a pure synchronous guard. Unnecessary. |

The selected helper still serializes both graphs. It removes the two complete UTF-16
result strings and materialization of the right aggregate output; it does **not** remove
the full semantic traversal or every serializer allocation. After ordinary successful
validation, the retained left result is at most the existing **24 MiB** journal limit.
The internal helper does not itself grant that bound to arbitrary invalid direct inputs.
The left byte-array serializer still uses an aggregate pooled buffer and copies its
result. The right serializer can require substantial scratch for an individual large
escaped token; its flush threshold is not a token/allocation ceiling. See the
[existing B source analysis](R1bc-journal-json-byte-count.md). No fixed peak memory,
latency, GC reduction or capacity qualification is claimed.

## Exact prospective file boundary

Only these executable/test files are authored:

1. `src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolJournalTransitions.cs`:
   replace the private string comparison with the post-validation reference shortcut
   and typed helper call; preserve every other branch.
2. New `src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolJournalWireEquality.cs`:
   the internal typed helper and its private comparison stream, with no general utility,
   dependency, registration, logger or cache.
3. New `tests/Unit/CanDoItAll.Tests.Unit/AgentToolJournalSchemaFiveTests.WireEquality.cs`:
   reuse the existing partial class fixtures and Core friend assembly. No need to expose
   a public test seam or edit the B size helper/tests.

The existing main-frozen Unit class currently has 10 Facts plus two four-row theories and B's five
Facts: **23 source rows**. The five additional authored Facts make **28 source rows**; discovery and execution
are still pending:

- Compare independent decoded journals across supported schemas 1-5 with the previous
  default-string oracle, and exercise validated identical-reference and shallow-copy
  transitions without bypassing later checks.
- Cover conditional default/empty and null/non-null wire distinctions, exact timestamp
  offset differences and same-revision changes to late journal fields.
- Cover default escaping, non-ASCII and malformed UTF-16 handling, and an actual large
  protocol token. The expected answer comes from the original two-string expression.
- Exercise differing beginning/middle/end bytes and shorter/longer outputs, including
  a maximum-cardinality journal and a changed trailing field; verify complete output
  equality rather than matching prefixes or caller-provided lengths.
- Exercise helper serializer failure on the right **after an early mismatch**, using
  a later malformed default immutable collection; preserve the baseline exception
  type. Separately exercise null public transitions, proposed-before-current failure
  ordering, invalid identical-reference input and invalid copies of a validated object.

The main-frozen `AgentToolEffectJournalIntegrationTests` comprises five partials and **23
source rows** (19 Facts plus two two-row theories). Keep the complete class in focused
validation, especially `Single_checkpoint_rejects_cancellation_state_approval_and_journal_races_without_dispatch`,
real receipt cursor/read-only lease cases and its two actual store-cost Facts. Those cost
Facts overlap this class and must not be counted again. They retain the four fresh reads,
two actual decodes and unchanged persistence operations from A; C introduces no new read.
The managed checkout also contains six additional Facts in its historical partial; they are outside this isolated performance successor and must not enter its transfer or count accidentally. Keep the existing prepared-write/recovery/UTF-8 compatibility selection when constructing
the final disjoint validation manifest. Do not infer a new total before source recount.

After source/peer review: parent serial Release build, exact source-derived discovery,
focused tests, full required static gate, then the same unprofiled 22-row matrix against
the closed B predecessor. Preserve natural failures and same-result comparison rules.
No forced GC, larger envelope, changed acceptance threshold, historical-graph transfer
or workload change is part of this experiment. Even a correct lower-allocation guard
cannot close the still-open maximum-shape capacity gate without that evidence.
