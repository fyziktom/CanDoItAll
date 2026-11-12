# File publication performance review, 2026-10-19

## Pass 1: Initial Performance Review

The coarse catalog lease blocked unrelated workspace writers throughout a real five-second SQL wait. Selected Agent/run/chat keys remove that dependency. Catalog-only updates now witness catalog bytes and pending recovery, without treating unrelated run/index changes as catalog conflicts. The 71 functional cases pass; the original one-second six-writer criterion still fails. Removing an unnecessary flush on the empty case probe did not resolve latency.

The complete sampled-thread-time profile (`c4-selected-profile-v3`, dotnet-trace 10.0.750501) points to repeated ancestor validation. The bounded 14-second capture stopped before host exit, converted without warnings and reports zero lost events. Its compact retained proof includes the exact trace hash, UTC session/window and method samples. An earlier launch rejected a runner deadline, and v2 ended without complete rundown; neither supports attribution. Instrumented runs are diagnostics, not latency qualification.

## Pass 2: Deep Pattern Scan

Scope: seven production hot-path files (physical path policy, durable writer, workspace store, execution slice, JSON store, publication scope and mutation witness). Counts below are matching source lines, not defect counts. The full scan used rg and manual cross-method review.

| Recipe | Matched lines |
| --- | ---: |
| IndexOf literal without comparison | 0 |
| Substring | 0 |
| StartsWith EndsWith literal without comparison | 0 |
| Contains literal without comparison | 0 |
| Culture casing | 0 |
| Triple Replace chain | 0 |
| All Replace calls | 3 |
| Params signatures | 0 |
| LINQ char All Any | 0 |
| All Any candidates | 9 |
| Static Dictionary | 0 |
| Static FrozenDictionary | 0 |
| List allocations | 13 |
| Dictionary allocations | 3 |
| CurrentCulture comparer | 0 |
| LINQ projections | 66 |
| LINQ ordering grouping | 12 |
| Compiled regex | 0 |
| Generated regex | 0 |
| New regex | 0 |
| Unsealed class | 0 |
| Sealed class | 19 |
| IEquatable | 0 |
| New HttpClient | 0 |
| Explicit new serializer options | 0 |
| All serializer options declarations | 2 |
| Async void | 0 |
| Blocking async candidates | 0 |
| ValueTask uses | 16 |
| String Format | 0 |
| Compound assignment | 1 |
| ContainsKey | 0 |
| Json serialization | 7 |
| All file read | 0 |
| ArrayPool stackalloc | 0 |
| FileStream creation | 11 |
| Path policy creation | 6 |
| Multiline GetAwaiter GetResult | 6 |
| All GetResult | 3 |
| All path factory creations | 20 |

### Classification after the checklist

- All 19 class declarations in this scope are sealed; no sealing change is justified. Both static Dictionary and FrozenDictionary counts are zero. Regex counts are zero on both sides.
- The single-line blocking recipe missed three deliberate synchronous DurableFileWriter entry points. A multiline follow-up found all three (six lines). The measured workspace paths call async methods, so changing the compatibility wrappers would not address this workload. ValueTasks are consumed once; no re-await site was found.
- The three Replace matches are two File.Replace calls and one policy-metadata replacement, not string chains. The one compound assignment updates a numeric aggregate. No chained string-allocation finding remains.
- List/Dictionary and LINQ sites primarily build bounded persisted snapshots, write sets and deterministic recovery order. Their counts do not establish excessive allocation; keep their ownership/order semantics. The witness pending-name scan is bounded by current target sets and remains subject to larger-size qualification.
- SerializerOptions uses target-typed construction once per JSON-store instance and one static witness instance; the explicit-constructor recipe alone missed it. Seven serializer call sites use runtime types/options without a source-generated context. This is a profiling candidate, not evidence of a schema-compatible speedup; no serialization or enum semantics change is proposed.
- Cross-file comparison: batch reads already reuse one fresh path policy while checking each path; the witness does the same within each synchronous capture/check. Durable writes deliberately refresh across await/callback boundaries. Retaining those boundaries avoids cross-request filesystem authorization caching.
- The confirmed new optimization candidate is the repeated ancestor walk within synchronous existing-directory preparation, described in [the source plan](../plan/P04-existing-directory-cost.md). It is an algorithmic duplicate-check issue rather than a missing LINQ/async API pattern. No additional source change is justified by the generic scan alone.

No numerical speedup is claimed before the uninstrumented contention replay. The sampled trace measures managed thread stacks, including waiting and concurrent activity; nested method totals cannot be summed or called disk/CPU time.
