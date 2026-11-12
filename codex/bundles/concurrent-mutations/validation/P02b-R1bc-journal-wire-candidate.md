# Journal wire equality: executed candidate

Status: all **161 correctness cases passed**, with final portability enforcement
passing. The unchanged performance matrix executed **22 rows: 11 passed and 11
failed**. All 183 execution identities match their discovery, source and binaries;
there were no skips. This is a correctness checkpoint, not capacity or bundle closure.

The [reviewed change](R1bc-journal-wire-equality-source.md) preserves the default
JSON representation as the equality contract. After existing validation, identical
references avoid redundant serialization. Other comparisons serialize the left
journal into UTF-8 bytes and fully serialize the right into an exact comparison
stream. It removes the pair of complete UTF-16 strings and right aggregate result.
The left byte result and serializer scratch remain. A mismatch does not stop right
serialization or suppress a later serializer exception. No hash-only equality,
stored schema, cross-call cache, authority or recovery rule changes.

## Correctness and static validation

Core, Unit and Integration built serially in standard Release with
`UseLocalCanDoItAllLibraries=true`, `--no-restore` and `/m:1`: zero errors, with
0/200/211 warnings respectively. Existing outputs were refreshed.

| Disjoint selection | Executed/passed |
| --- | ---: |
| Complete schema-five class, including five new wire cases | 28/28 |
| Prepared run witness unit | 3/3 |
| Actual owner witness | 11/11 |
| Existing conflict/read checks | 19/19 |
| Existing interrupted-update recovery | 15/15 |
| Complete current effect-journal owner class | 23/23 |
| UTF-8 and durable-write compatibility | 47/47 |
| Usage projection integration | 15/15 |
| Total | 161/161 |

The full effect-journal selection includes the two receipt-cost cases; they were
not counted or executed twice. Historical reader additions were excluded. New
wire tests cover schemas 1–5, conditional fields, DateTimeOffset representation,
Unicode/surrogate replacement, large tokens, chunk/length mismatches, 32,768
proposals and late serializer failures, plus original validation ordering.

Portability tooling passed 6 + 4 tests; rendering tooling passed 7 tests and the
register passed for 1,198 inputs. The full proposed-source scan includes untracked
files and contains 36,585 findings. Its one added finding is the intentional JSON
escaping string in the new test; zero findings became stale. All 15,467 protected
findings are unchanged. No baseline edit was needed; final enforcement without
`--write-baseline` passed. Documentation validation passed for 426 maintained
Markdown files and `git diff --check` passed.

## Performance result and remaining decision

The byte-count predecessor produced 12 passing and 10 failing rows. This candidate
produced 11 passing and 11 failing rows: ten pre-operation idle/headroom refusals
and one cancelled sample. All fresh hosts exited naturally and their owned file
fixtures were removed. No samples were retried and no database/container was created.

The comparison verifies the same 22 workload objects, prepared bytes, runtime,
architecture, OS and GC settings. Eleven rows pass in both candidates. Across those
complete cohorts, median latency changes range from -5.50% to +32.30% and allocation
from -0.85% to +6.67%. These sequential single-machine observations do not establish
a general speedup. The near-maximum-byte FreshDetail row now fails its headroom
gate; its incomplete cohort receives no median.

The serial near-maximum-byte CoreSlice cold sample completes the same four outcomes
in both candidates. Allocation changes from 1,379,452,832 to 1,124,769,624 bytes
(-18.46%); elapsed time changes from 2,305.2457 to 2,155.8445 ms (-6.48%). Both
cohorts fail and have zero measured samples. Maximum-proposal cold outcomes differ,
so their numbers are not an equivalent completed-operation comparison.

Retain this source provisionally with its compatibility proof. Neither this change
nor byte-counting is final performance acceptance. The unchanged previous/target
intent and target publication still rewrite the complete retained journal for each
bounded page. Their cost grows with journal size; processing all fixed-size pages
can accumulate quadratic work in history length. A smaller serializer allocation
does not remove that structural cost. The next owner design must explicitly weigh
publication-buffer changes against a durable-format change, preserve original
snapshot timing and crash recovery, and qualify the supported envelope before
activation. No further optimization or migration is released by this record.

Historical/currentness and actual host-restart proof proceed as separate correctness
work. Complete producer activation, persisted runtime policies and broad closure
remain open.

## Retained evidence

Evidence root: `.artifacts/concurrent-mutations/validation/r1bc-journal-wire-candidate-validation`.

| Evidence | SHA-256 |
| --- | --- |
| Reviewed source inventory | `13281B7CEC073FCBEAB0EDB0C3A66B8593E706EA8621FF396A9478316EFEE989` |
| Complete source inventory, 8,632 inputs | `9A971228B35850FE7B70F6720A1113C2B27C33F9C28CFBD466C6C8F99140F95E` |
| Runner inventory | `4CAD82751C7974EC632739926AA33BA753C5EAC0BBB8F07899FEDF631701261C` |
| Selection | `32ADFE6A4BD97A7643FAD57CCEBF80D219FDB3EBFC1B7E1F02B83E662668EC2A` |
| Completed 183-case summary | `3F9BC9920C9CF056731F935375E36B803D279B9FA51DA2A7D29BFE7FF4B5B616` |
| Matched comparison | `D1790F5C5C2B254444F7931D83A1F8CDEE7F8F3573488E0F0281DB27A376477C` |

All 8,632 source inputs and HEAD were rechecked after reporting; the source freeze
ended at `2026-10-17T04:19:15.108405+00:00`. This executed record and status updates
were written afterward. Earlier blocked cleanup resources and configured GPG signing
remain recorded in [resource ownership](../resources.md).
