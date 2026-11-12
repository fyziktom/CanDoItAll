# Prepared run-read witness: executed candidate

Status: the isolated candidate passed all **112 correctness cases** and final
portability enforcement. The unchanged 22-row performance matrix executed with
**11 passed and 11 failed**. All 134 results have matching discovery, source,
runtime, execution and definition identities; no cases were skipped. This is a
correctness checkpoint, not maximum-workload capacity or end-to-end closure.

## Change and scope

The [reviewed source](R1bc-prepared-run-witness-source.md) avoids repeated decoding
and comparison of an unchanged stored run during the immediate owner update.
Both update overloads capture an immutable run and the digest of the same validated
decoder bytes, including BOM, whitespace and EOF. After durable intent publication,
the owner freshly hashes the stored file. Exact path, previous-object identity and
digest agreement permit reuse only within that invocation. Changed bytes use the
existing full decoder and previous-or-target checks. Ordinary Save and recovery do
not receive this evidence. No persisted format, public authority, global cache or
historical-read behavior changes. The cooperating-owner protocol remains optimistic
with respect to unrelated external writers; this is not atomic filesystem CAS.

The source inventory is `7B7BD88488133BDA59049FB2C4EF59C95004C0D9C2E5A52E99FB2726236BDA66`.
Eight source/test files were transferred with exact predecessor guards. The independent
review verified their hashes, immutable graph, private scope and disjoint case counts.
Historical reader, task-plan publication and browser changes were excluded.

## Execution and static checks

Persistence, Unit and Integration built serially in standard Release with
`UseLocalCanDoItAllLibraries=true`, `--no-restore` and `/m:1`: zero errors, with
existing dependency/compiler/analyzer warnings retained. No custom build outputs
or replacement fixtures were introduced.

| Correctness selection | Executed/passed |
| --- | ---: |
| New run witness unit contracts | 3/3 |
| New actual owner witness cases | 11/11 |
| Existing prepared commit/conflict reads | 19/19 |
| Existing interrupted-update recovery | 15/15 |
| Exact actual receipt-store cost cases | 2/2 |
| Existing UTF-8/durable-write/unit compatibility | 47/47 |
| Existing usage projection integration | 15/15 |
| Total, with no overlapping cases | 112/112 |

The new owner cases exercise both callbacks with payloads above 8 MiB, immutable
callback input, same-size/same-time/same-revision tampering, alternate valid encodings,
already-written targets, missing/truncated/trailing data, original-intent recovery and
ordinary Save. The maximum receipt-cost case confirms four fresh run reads remain,
while full decodes fall from four to two. This does not imply zero disk I/O.

The portability tool tests passed 6 + 4 cases; rendering tool tests passed 7 cases and
the register passed for 1,198 inputs. The complete scan included untracked sources:
36,583 findings. Against the prior UTF-8 scan, all 40 added findings and zero stale
findings were reviewed, including intervening diagnostic source/documentation.
The 15,467 protected executable-source findings were unchanged. No baseline refresh
was needed; final enforcement without `--write-baseline` passed.

After recording the executed checkpoint, documentation validation passed for all
426 maintained Markdown files and `git diff --check` passed. These prose-only updates
occurred after the source freeze ended and do not change the tested executable inputs.

## Matched measurement and limits

The prior UTF-8 candidate had 10 passing and 12 failing performance rows. This
candidate has 11 passing and 11 failing rows: nine pre-operation idle/headroom refusals,
one cancelled sample and one four-run incremental-private-memory envelope failure.
All 22 fresh hosts exited and their owned fixture directories were removed. The
small-case successes do not close the failed large-case gates.

The comparison checks the same 22 lane objects, prepared bytes, runtime, OS,
architecture, GC configuration and concurrency. The controlled diagnostic hook added
between candidates was disabled; inherited diagnostic configuration was refused.
Only complete passing cohorts of seven measured samples receive medians. Across the
ten rows passing in both candidates, median latency changes range from -8.77% to
+37.10%, and allocation changes from -2.11% to +1.19%. There is no general speedup claim.

For the serial near-maximum-byte CoreSlice cold sample, the same four outcomes
completed: allocations changed from 1,427,387,272 to 1,062,533,640 bytes (-25.56%) and
elapsed time from 2,679.0939 to 2,490.1116 ms (-7.05%). This is one cold sample from
two failed cohorts, not a steady-state or capacity qualification. The maximum-proposal
CoreSlice cold outcomes differ between candidates, so its byte/time differences are
not reported as improvement. Paired ordinal/outcome flags remain in the raw comparison.

The accepted earlier trace also attributes allocation to full journal byte-size
validation, checkpoint equality and publication. A bounded exact byte-counting design
is being evaluated next; no relaxed size limit, forced collection or predicted saving
is accepted as proof. Historical/currentness, producer activation and full release
validation remain separate work.

## Retained evidence and closure

Evidence root: `.artifacts/concurrent-mutations/validation/r1bc-run-witness-candidate-validation`.

| Evidence | SHA-256 |
| --- | --- |
| Complete source inventory, 8,619 inputs | `D2DDF28AD0F419C4F90B93B8127AAB87B79B0AF6AAA3009E9266AE7D5D630BE2` |
| Runner source inventory | `9862795D84F5FEF72ECA5EE9696D266324E7351B87C430E7BA9465512C0E9586` |
| Selection | `D214EC9096634FDB2B3A47417F4A67D9CC8BF9FE34298F2168D2438BA7F1D2D6` |
| Completed 134-case summary | `B9537BE892B661B6B1E08FA8F8013F9461131BDE65DD13A9928A87206F887625` |
| Matched comparison | `FB006F483A46BDB6BAD7035E8FF2E44FD63BC74DBEE8230C59BCADE54A5AFDD4` |

The first report-only closure invocation failed because StrictMode rejected property
access on an empty failure list. The original runner and inventory remain unchanged.
The separately retained V2 closure enumerates failure objects before selecting names;
its SHA-256 is `096CDC3EC86A3D3A82C3A65B973791FE8612E08017CFD33E501961550F242170`.
It completed without rerunning tests. The amendment records both identities and the
initial reporting failure. Final source verification matched all 8,619 inputs and
released the source freeze at `2026-10-17T03:35:49.8728707Z`.

The earlier blocked cleanup resources and configured GPG signing wait remain recorded
in [resource ownership](../resources.md). Neither is silently treated as resolved.
