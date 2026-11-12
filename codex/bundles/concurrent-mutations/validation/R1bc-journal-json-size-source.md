# R1bc B journal JSON count: authored source checkpoint

Status: four exact source files authored in the managed checkout only; no build,
discovery, tests, static scan or performance execution. Parent reviewed the helper,
owner expression and five new Facts without a blocking source finding. Independent
UI-peer source review also passed at the exact four source hashes in the preserved
pre-review inventory `B9C73E9BB6419C40374FECB9D7566C5CC4CC1814AA3F051FEF66008B0B1CEE77`.
No production/test source changed after that review. Main and the completed A runtime
inputs were not changed.

The [approved design](../plan/R1bc-journal-json-byte-count.md) was released at
SHA256 `D004F77B280B54DBB864B554EABEF82884E5EFC34879753DE0C464102193905C`.
The [source inventory](../reviews/R1bc-journal-json-size-source-inventory.json)
binds exact current lengths/hashes to the two main predecessors and two absent paths;
it also retains the design/source-record identities. Do not transfer unrelated managed
historical, C3 or caller changes as part of this experiment.

## Source behavior

- `AgentToolJournalModels.cs` changes only its final encoded-size expression. Raw
  sum/short circuit, all semantic checks, 24 MiB limit, exception message and
  `ConditionalWeakTable` insertion behavior remain unchanged.
- New `AgentToolJournalJsonSize.cs` is a 42-line Models-local typed helper. It calls
  synchronous `JsonSerializer.Serialize(Stream, AgentToolJournalRecord)` with default
  options and counts the complete output in a checked `long`. No bytes are retained by
  its private stream. `Write(byte[], offset, count)` uses the framework's protected
  `ValidateBufferArguments` before delegating to the span overload. `WriteByte` uses
  the same checked arithmetic; read/seek/resize/length/position are unsupported.
  Flush/dispose have no external effects. The installed net10 reference XML declares
  the reused validation API; compilation is still required.
- There is no early over-limit exception, serializer catch, new options, graph copy,
  generic encoder, public witness, new dependency, cancellation policy or changed
  cache. The serializer still owns token scratch and rented output buffers; a large
  escaped token can allocate substantially before the sink sees it.
- The existing test file changes only its partial-class marker. The new partial
  reuses existing versioned fixtures and adds five Facts without changing old rows.

## Independently derivable focused selection

Owning project: `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`.

Exact class filter:
`FullyQualifiedName~CanDoItAll.Tests.Unit.AgentToolJournalSchemaFiveTests`.
The class has no base test class, category exclusions, skips or dynamic providers.
There are **15 Facts + two fixed four-row InlineData theories = 23 discovery and
runtime rows**, of which **five Facts are new**. This is source accounting, not runner
enumeration or a claim that any row passed.

| New Fact | Observable contract |
| --- | --- |
| `Counted_json_size_matches_default_wire_for_all_supported_journal_versions` | Old default serializer is the byte-count oracle for versions 1–5, restored journals and exact owner receipt/default omissions. |
| `Counted_json_size_preserves_default_escaping_and_large_single_tokens` | Nested controls/quotes/backslashes/HTML-sensitive text/non-ASCII/supplementary Unicode/lone surrogate and an 8 MiB single envelope retain default encoded length. |
| `Whole_journal_encoded_boundary_remains_exact_at_limit_minus_equal_and_plus_one` | Old serializer independently establishes 24 MiB minus/exact/plus one; raw sum is below its bound; public validation keeps exact acceptance/error behavior. |
| `Json_count_preserves_serialization_failures_and_invalid_copies_never_become_valid` | Default-serialization failure type is retained; invalid journals still fail repeatedly, and a cached original does not authorize a malformed new record identity. |
| `Json_count_covers_every_proposal_and_trailing_fields_at_maximum_batch_cardinality` | 512×64 original proposals count fully; a trailing field increment remains visible after many serializer continuation points. Public validation agrees with the independent encoded bound. |

Existing 18 rows retain the previously authored raw-total and escaped-only overflow,
legacy schemas, typed context, owner-result immutability and exact approval rules.
No allocation/time threshold was added to Unit tests. The maximum-cardinality Fact
does not assert a universal capacity pass; it verifies full traversal and the existing
encoded acceptance condition.

Parent owns standard Release build, exact discovery/execution, full static validation
and retained A/persistence/recovery selection. Parent then chooses the unchanged
22-row unprofiled matrix for an isolated comparison. A's completed 112 correctness
passes and 11/22 performance passes do not validate B. The accepted whole-host trace
identified the old allocation site but does not predict per-operation savings or
prove this candidate fits the reference memory profile. No forced GC, relaxed budget,
payload-format change or capacity qualification is part of this checkpoint.
