# R1bc C journal wire equality: source checkpoint

Status: authored in the managed checkout, independently source-reviewed, unbuilt and
unexecuted. Parent owns transfer, Release builds, exact discovery/execution, static
closure and any matched performance comparison. No main source, B input, historical
source, C3 input, package or validation threshold changed in this slice.

The approved architecture predecessor is `3E2F15B792E81CF9E3B97CBCDE44ABE5C2558157A74392B5640C0F60DBB18459`;
the maintained [card](../plan/R1bc-journal-wire-comparison.md) explains the exact call
path and primary .NET 10.0.12 sources. The final source inventory binds the three files,
this proof/card, main predecessor identities and independent review. The main existing
Transitions predecessor is `6E0D0A9D5C7E0C05B3E2D5CC4E8E591DE14A234D886FCDEC8CD266B080EDE52F`.
Both new helper/test paths are absent from main at source freeze.

## Authored behavior

The existing private `Equivalent` now checks reference identity only after both graph
validations have already run. All provider, segment and proposal transition checks
still follow. Apart from removing the unused JSON using, this expression is the entire
existing-file delta against the actual main predecessor; earlier bundle changes in the
repository-wide Git diff are not part of C.

The internal typed Core helper serializes left to default UTF-8 bytes first and fully
serializes right to its private comparison stream. Mismatch is sticky and does not
stop traversal. The checked total and final length are separate from prefix/content
comparison. The range test uses long arithmetic before narrowing a slice offset; the
byte-array overload validates its arguments and the single-byte path shares the same
logic. Unsupported stream capabilities throw. The lower-level helper has no reference
shortcut, authority, cache, file access, options, cancellation fiction or public API.

No new CWT entry or witness is introduced. Null public transitions, proposed-first
validation, invalid copies and invalid identical-reference input retain their existing
classification. The existing 24 MiB limit and every graph check remain owned by the
Models validator, whichever validated B/no-B implementation parent ultimately selects.

## Source-derived test selection

The new `AgentToolJournalSchemaFiveTests.WireEquality.cs` has exactly five Facts:

1. `Wire_equality_preserves_default_json_for_all_supported_journal_versions`
2. `Wire_equality_preserves_conditional_members_and_exact_capture_offsets`
3. `Wire_equality_preserves_encoding_replacement_and_large_single_tokens`
4. `Wire_equality_checks_chunk_content_length_and_maximum_cardinality_trailing_fields`
5. `Wire_comparison_preserves_late_serializer_failures_and_validation_order`

These use the original two-string expression as an oracle. Fixtures include independent
schema 1-5 decodes; default versus empty immutable arrays; exact timestamp offsets;
non-null disclosure/cursor and lease changes; default escaping and malformed UTF-16
replacement; an 8 MiB protocol token; 32,768 distinct proposals and a changed late
field; short/long/content mismatches; and a malformed later collection after a different
first scalar and a 64 KiB earlier token. Error cases retain serializer failure instead
of returning false, plus public null/order and validated-copy boundaries. No tests mock
the serializer or introduce product counters. Test loop arrangements are assertions
inside five Facts, not extra discovery rows.

Current main SchemaFive class is 10 Facts + two four-row theories + B's five size Facts
= 23; C adds five = **28 Unit rows**. There are no dynamic data sources, skips, excluded
traits or inherited tests in those three partials. Core already exposes internals to
the Unit assembly; no reference/friend change is needed.

The actual main effect class is `CanDoItAll.Tests.Integration.Runtime.AgentToolEffectJournalIntegrationTests`:

| Main partial | Facts | Fixed theory rows | Total |
| --- | ---: | ---: | ---: |
| main file | 2 | 2 | 4 |
| Dispositions | 2 | 0 | 2 |
| Origin | 3 | 0 | 3 |
| Receipts | 6 | 2 | 8 |
| Slices | 6 | 0 | 6 |
| Total | 19 | 4 | **23** |

Its only class trait is `FileSystemPortability`, retained by the canonical stable
filter. It has no dynamic or skipped rows. Its two store-cost Facts already belong to
those 23; selecting the whole class replaces, not supplements, B's separate cost2 lane.
The managed-only Historical partial adds six Facts and is explicitly outside this
isolated transfer and main-only count.

Against the frozen B manifest `950060AD4087E0A2926F7BCD999C81F40D014564E4D51D933B024F7D739C573F`,
the planned disjoint focused selection is:

| Lane | Rows |
| --- | ---: |
| SchemaFive, including B and C | 28 |
| Prepared run witness Unit | 3 |
| Prepared run witness owner | 11 |
| Existing conflict reads | 19 |
| Existing run recovery | 15 |
| Whole main effect journal class | 23 |
| UTF-8 Unit compatibility | 47 |
| Usage projection integration | 15 |
| Total | **161 = 78 Unit + 83 Integration** |

This is B135 minus overlapping cost2 plus effect23 plus C5. The unchanged 22 long-running
rows are separate, yielding 183 executions only if parent selects this exact B+C
candidate. Discovery remains a comparison against these source counts, not their origin.
No runner or execution is authored in this slice.

If B is rejected, C remains production-independent. Keep/re-own the base test class's
mechanical partial modifier (B's only base-test change), remove or separately account
for B-only size-helper tests, and recount the selected source. Do not report 161 for a
changed cohort without that reconciliation; a simple omission of B's five size Facts
would make 156 focused rows. C never calls `AgentToolJournalJsonSize`.

## Independent review and limits

The UI architecture peer reviewed exact production hashes `5AD46EEB...` / `B6FED1C...`
and final test hash `AE296D5...`, confirmed the two-line existing-source delta and the
main-only source counts, and reported no source blocker. The final test successor adds
only null-forgiving operators at two already-known non-null fixture context receivers;
no assertion or row count changed after the initial test review. The inventory records
full hashes rather than relying on these prefixes.

Validation performed here is source inspection, exact predecessor comparison and
attribute/provider accounting only. No compilation, discovery, test, static scan,
performance run, database, process or installed-tool action occurred.

The accepted pre-A/B trace identified this site, not C's realized savings. C still
traverses both graphs, retains one complete UTF-8 result (at most 24 MiB only after
normal successful validation), and leaves the left serializer's aggregate pooled
buffer plus large-token scratch behavior. B's early same-outcome cold result is not an
allocation improvement; it is not evidence that C will improve either. Preserve both
experiments and compare exact outcomes after the closed matrix. No capacity, throughput,
peak-memory or GC qualification is claimed.
