# First canonical digest length: independent design review

Status: source implemented after the parent's complete baseline passed on
2026-10-16; parent reports candidate builds, codec 25/25, the HTTP regression lane
and matched performance 2/2 passed (0 skipped).
The [complete baseline](../validation/P02bc-protected-performance-baseline.md)
records all 33 samples; its large-definition allocation is not attributed wholly
to this candidate. This subagent ran no build, test or benchmark.

The approved source change is limited to OperationCodec's forwarding digest
overload plus exact `out int` byte length, OperationProtocol's get-only attempt
length and Writer's post-lookup current-limit comparison. The existing canonical
writer, SHA-256 bytes, result encoding and ordering remain unchanged. The golden
codec Fact now checks exact byte length, one additional codec Fact exercises
escaped Unicode at the inclusive boundary and one byte below, and one actual
HTTP integration Fact proves exact-boundary acceptance, historical replay after
a one-byte limit reduction and fresh-operation refusal without added root or
receipt effects. Source counts: codec class 25, HTTP integration class 15. Parent
recorded execution. The matched 33-sample repeat measures 1.016MB/attempt less
allocation for 1,000-child replacements (7.61%), with unchanged functional
outcomes. Latency is mixed and some small/contention samples are slower; no
general latency gain or capacity closure is claimed. See the complete comparison
in the baseline review.

## Finding and smallest safe seam

One source-supported duplicate exists: `ProjectDefinitionOperationProtocol`
prepares the digest with `VersionOneMaximum`; after a receipt miss,
`ProjectDefinitionWriter.ExecuteProtectedAsync` calls the same codec again only
to enforce current `MaxCanonicalCommandBytes`. `ComputeDigest` builds the full
canonical array, then hashes it on both calls. The first array's `Length` is the
exact answer to the second size check.

Recommended scope after baseline approval:

1. In `ProjectDefinitionOperationCodec.cs`, expose the first final canonical
   byte length with the digest, for example a small overload with an `out int`
   while preserving the existing digest-only forwarding signature. Keep the
   exact v1 writer and hard bounded stream unchanged. Do not retain the large
   plaintext byte array on the attempt or change digest equality/semantic bytes.
2. In `ProjectDefinitionOperationProtocol.cs`, carry that owner-computed length
   as a get-only property of `ProjectDefinitionOperationAttempt` beside its
   existing digest. The property is neither wire input nor persisted receipt
   metadata; callers cannot supply a substitute length.
3. In `ProjectDefinitionWriter.cs`, only after `TryReadAcceptedAsync` returns a
   miss, enforce `length <= current.MaxCanonicalCommandBytes` and keep the same
   `ProjectDefinitionLimitException`/typed error mapping. Retain current raw
   input validation in that branch. There is no second normalization/hash.

Read and use one current limits value for the paired new-effect checks if that
minor consistency improvement is included. The initial hard-v1 checks and
receipt disclosure still precede operational limits; lowering deployment limits
must not reject an already accepted original request. New accepted-result size
checks, source admission, atomic Search/receipt writes and all recovery ordering
remain unchanged.

## Correctness constraints

- Command constructors snapshot their child collections. Normalization produces
  new immutable scalar/row records, and `ProjectDefinitionNormalizedIntent`
  copies arrays into read-only collections. Operation, caller, expectation,
  creation/effect binding and string/date values used by both encodes are stable.
  The first length is valid for this exact attempt, not an equivalent current
  definition, raw transport JSON or accepted receipt.
- Raw field/count/aggregate limits cannot be replaced by canonical length:
  normalization can trim whitespace and change dates, and JSON escaping can
  expand a string. Preserve original raw bounds plus the canonical-byte check.
- Keep rejection before owner/effect admission for a new over-limit operation.
  Do not move the current-limit check before the original receipt lookup. Keep
  missing versus omitted child intent and the governed effect binding unchanged.
- The existing SHA-256 golden bytes and both accepted-result golden encodings
  must remain identical. Length is a transient observation, not a new codec or
  digest version, and no migration/Contracts/UI change is required.

## Focused validation changes

`ProjectDefinitionOperationCodecTests` should extend golden intent proof to
assert exact UTF-8 canonical byte length and exercise escaped/multibyte content
at the exact boundary and one byte below it. Preserve original hard-limit
failure and caller-array mutation/snapshot proofs. No test should merely assert
that the writer calls a helper once.

`ProjectDefinitionHttpIntegrationTests` currently tightens only a field limit.
Add a canonical-only current-limit case: first accept under valid limits, lower
only the canonical limit, prove original exact replay succeeds with the same
receipt, and prove a fresh operation rejects without a project/receipt effect.
This is meaningful regression evidence for the bypass-on-replay/new-admission
distinction. Do not substitute the current field-limit case for this branch.

Then rerun the same two protected performance Facts against unchanged workload,
host/GC/fixture configuration and compare all samples, allocation/GC and outcome
counts. No proportional latency or allocation gain is promised by source review.

## Other inspected costs and scan checklist

Scope was Writer, Protocol, OperationCodec, NormalizedIntent and Rules, using
the performance skill's relevant async, memory/string, collections and I/O
recipes. Exact lexical hits before classification:

| Recipe | Hits |
| --- | ---: |
| Literal IndexOf/StartsWith/EndsWith/Contains without comparison | 0 each |
| Substring; parameterless ToLower/ToUpper; three-Replace chains; params; char LINQ | 0 each |
| Static Dictionary / FrozenDictionary | 0 / 0 |
| Explicit new List / Dictionary | 6 / 0 |
| CurrentCulture comparer | 0 |
| Select/Where/Cast/Take/Aggregate calls | 9 |
| New HttpClient / JsonSerializerOptions; JsonSerializer calls | 0 each |
| Async void; Wait/GetAwaiter blocking; stackalloc; regex | 0 each |
| Sealed / unsealed concrete class declarations | 9 / 0 |

The collection hits support bounded row/result construction and are not blanket
rewrite recommendations. `.Result` uses in the inspected path denote typed
accepted receipts rather than blocking Task results. No repeated ValueTask await
was identified.

`DecodeAccepted` creates one avoidable full receipt copy for
`bytes.SequenceEqual(canonical.ToArray())`; a future internal span-comparison
method could avoid that copy without exposing mutable storage. That is a
separate measured candidate, not part of the first-length change. Keep canonical
re-encoding validation and defensive ownership of escaped arrays. Per-row
`Utf8JsonWriter.Flush` also costs work but feeds the hard bounded stream; simply
removing those calls could increase temporary growth and needs separate bounded
writer proof. Neither is established as a cheaper dominant bottleneck.

| Classification | Findings | Disposition |
| --- | ---: | --- |
| Critical correctness/performance defect in proposed length reuse | 0 | No blocker found under the constraints above |
| Implemented hot-path improvement | 1 | Duplicate canonical encode/hash removed; matched allocation benefit measured |
| Separate allocation/buffering candidates | 2 | Canonical comparison copy; flush frequency; profile before changing |

This source review identifies candidates and constraints; only matched measured
runs establish their benefit and regression limits.
