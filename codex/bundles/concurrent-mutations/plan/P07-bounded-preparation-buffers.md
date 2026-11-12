# Bounded preparation allocation review

The first finite matched measurement passes exact bytes but exposes unnecessary growth
allocation. At 1024 small steps, the existing serializer allocates 12,055,995 bytes per
call and the bounded MemoryStream path 27,159,029; measured medians are 66.484/73.730 ms.
At 128 small steps, allocation is 1,518,568/3,393,720 bytes and medians 12.006/12.657 ms.
The extra retained UTF-8 output buffer and discarded growth arrays explain that cost.
These are six alternating rounds of three serializations, not tail latency or throughput.

The 179-case candidate is retained and released. Replace only the private output
buffer with an ArrayPool-backed synchronous Stream. Check the logical output byte limit
before retaining each write, rent only when growth is necessary, copy existing bytes,
and return every replaced/final array with clearArray:true. Decode into the immutable
result string before disposal. No new package or persistent cache is introduced.

The private Stream implements required platform members; those members do not represent
separate domain responsibilities or a new runtime boundary. No domain owner moves.
The existing serializer options, exact bytes and messages remain unchanged.
An internal optional ArrayPool<byte> parameter is a resource-lifecycle test seam using
the framework's existing abstraction, not a new application interface.

Add resource tests proving every rental is returned on success, growth, size refusal
and converter failure, with clearing requested. Check retained references contain no
serialized input after return using the tracking pool's implementation of clearArray.
Repeat byte-boundary/escaping/multiple-flush tests and the same alternating measurements.
Retain the first measurements; do not rewrite them as results of the pooled candidate.

The byte limit bounds output length, not the framework pool's retained capacity, JSON
serializer scratch space, the final UTF-16 string or the caller's input graph. The pool
is shared and may retain arrays for reuse; clearing prevents prompt data from being left
available to the next renter. Record cold and warm cost separately: warm allocation
reduction does not prove a lower worst-case process working set under concurrent load.
The full-source first-acceptance cost remains separately measured and bounded.

This is a performance correction inside Persistence, not executor activation. Original
policy downgrade prevention and the remaining adapter/manager work remain mandatory.

The combined correction selects 183 cases: 68 serializer/codec unit cases and 115
prepared-source/input and authority integration cases. The unchanged root-scope/funding
and pure startup verifier retain the preceding 61-case evidence; no combined whole-suite
claim is made. Build Persistence directly, refresh Unit/Integration, discover exactly,
run full portability enforcement and retain observed source/runtime/resource proof.

## Continuation-boundary test correction

The first pooled candidate passes 180/183. Its three new lifecycle tests incorrectly
assume primitive arrays and a single custom converter yield output to the Stream.
The runtime uses continuation boundaries for streaming; a converter's writer.Flush
does not itself guarantee a write to the final Stream. See the
[.NET 10 implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Metadata/JsonTypeInfoOfT.WriteHelpers.cs).

Keep production unchanged. Use ordinary object records to exercise incremental output,
with the failing converter in the final record after earlier writes. Preserve rental,
growth and clearing assertions. Strengthen the adjacent multiple-flush test to prove
that a rejected object sequence stops enumeration before reaching its end.
Repeat these two classes, nine cases, after a Release Unit build without dependencies.
Verify every production source and production DLL copy against the initial pooled
candidate; retain its failures and the 180 unchanged passes. Re-run full portability,
source/runtime identity and resource reconciliation. This is an exact test correction,
not a new whole-suite claim.
