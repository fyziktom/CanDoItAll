# Journal JSON byte count: executed candidate

Status: all **135 correctness cases passed**. The unchanged performance matrix
executed **22 rows: 12 passed and 10 failed**. All 157 results have matching
discovery, source, runtime and definition identities, with zero skips. Final
portability enforcement passed. This is not maximum-workload capacity or bundle
closure.

## Change and decision

The [reviewed change](R1bc-journal-json-size-source.md) replaces the final journal
size-validation byte array with default typed synchronous JSON serialization to
a counting stream. Validation order, the 24 MiB journal limit, complete traversal,
exception behavior and existing immutable-object validation cache remain intact.
The stream retains only its checked count; serializer scratch storage, especially
for a large escaped token, is not constant or bounded by that count.

Retain this change provisionally for the next independent wire-comparison candidate.
Passing large detail-read cohorts show useful allocation reductions, but the core
mutation cold sample regresses and large-case gates remain open. This is not final
performance acceptance. The next comparison holds this source fixed and changes
only journal equality; it cannot establish that this change is universally faster.

## Correctness and static evidence

Models, Unit and Integration built serially in standard Release with
`UseLocalCanDoItAllLibraries=true`, `--no-restore` and `/m:1`: zero errors and
0/200/211 warnings respectively. Existing outputs and the isolated fixture were
reused. No custom configuration, database or container was created.

| Disjoint selection | Executed/passed |
| --- | ---: |
| Complete schema-five journal class, including five new size cases | 23/23 |
| Prepared run witness unit | 3/3 |
| Actual owner witness | 11/11 |
| Existing conflict/read checks | 19/19 |
| Existing interrupted-update recovery | 15/15 |
| Actual receipt-store cost | 2/2 |
| UTF-8 and durable-write compatibility | 47/47 |
| Usage projection integration | 15/15 |
| Total | 135/135 |

The new differential cases compare the previous serializer across schema versions
1–5, omitted fields, Unicode and invalid surrogate replacement, large single tokens,
the exact 24 MiB boundary, validation failures/cache behavior and 32,768 proposals.

Portability tooling passed 6 + 4 tests; rendering tooling passed 7 tests and the
register passed for 1,198 inputs. The full scan included nonignored untracked source:
36,584 findings. Its sole added finding is a Windows-path heuristic match in the
intentional JSON escaping test string; zero findings became stale. All 15,467
protected findings are unchanged. No baseline edit was required; final enforcement
without `--write-baseline` and `git diff --check` passed.

## Matched performance and limits

The predecessor had 11 passing and 11 failing rows. The candidate has 12 passing
and 10 failing rows: five pre-operation idle/headroom refusals, four cancelled
samples and one four-run incremental-private-memory envelope failure. Every host
exited naturally and every owned file fixture cleaned up. Failed samples were not
retried. Their observations do not qualify capacity.

The comparison verifies identical 22 workload objects, prepared bytes, runtime, OS,
architecture and GC configuration. Only complete passing cohorts of seven measured
samples receive medians. Eleven rows pass in both candidates. Large FreshDetail
cohorts reduce median allocation by 17.65–18.18% and latency by 17.51–28.24%; the
maximum-proposal native refresh reduces allocation by 9.93% with latency up 2.20%.
Small CoreSlice latency rises 31.63% and 7.81%, while allocation changes by less
than 0.3%. The sequential single-machine runs do not establish a general speedup.

For the serial near-maximum-byte CoreSlice cold sample, both candidates completed
the same four outcomes. Allocation rises from 1,062,533,640 to 1,379,452,832 bytes
(29.83%), while elapsed time falls from 2,490.1116 to 2,305.2457 ms (7.42%). Both
cohorts fail and have no measured samples. This adverse observation remains open;
it is neither hidden by the passing detail cases nor treated as steady-state proof.
The maximum-proposal CoreSlice cold outcomes differ and cannot be compared as an
equivalent completed operation. Paired ordinal/outcome flags are retained.

## Retained identities

Evidence root: `.artifacts/concurrent-mutations/validation/r1bc-journal-size-candidate-validation`.

| Evidence | SHA-256 |
| --- | --- |
| Reviewed source inventory | `29D752E330425DD434619657488DB27E4A7206B15625A3703DE50FCE9AB9510F` |
| Complete source inventory, 8,626 inputs | `3E7E1CD00F49FB9613245F3A07698F15D1BE1077DC6100E8863183E3337B739C` |
| Runner inventory | `462C924C740FD13A12E2937EE0B6B041BF05CE3322DB92BF4ED414CFF3DD9667` |
| Selection | `950060AD4087E0A2926F7BCD999C81F40D014564E4D51D933B024F7D739C573F` |
| Completed 157-case summary | `4DF7EB6E313D9C1F52C81D1A2901F73F7A6868472782427C9008244D793B5F61` |
| Matched comparison | `F762D5BFD33EC66B1DEFF99CE904BBF7069DCC4E93166DBEAE0F6E9D680975DA` |

All 8,626 source inputs and HEAD were rechecked after reporting. The source freeze
ended at `2026-10-17T04:02:03.016248+00:00`. This executed record and status updates
were written afterward. Historical/C3 changes were excluded. Earlier blocked cleanup
resources and the configured GPG signing wait remain in [resource ownership](../resources.md).
