# R1bc B: count the existing journal JSON without retaining its final byte array

Status: approved design; the four-file B candidate and five Facts are now authored,
unbuilt and unexecuted. See [the source proof](../validation/R1bc-journal-json-size-source.md).
The approved design predecessor was `D004F77B280B54DBB864B554EABEF82884E5EFC34879753DE0C464102193905C`.
Parent reports A's 112 focused correctness rows passed and its
unchanged 22 performance rows ended 11 passed / 11 failed with clean process/resource
closure. The complete A comparison remains parent-owned. This proposal is the
journal-size option previously called C in
[the accepted-trace decision](R1bc-accepted-trace-next-owner-change.md); it is a separate
successor experiment, not part of A's proof or historical-graph transfer.

## Decision and exact compatibility boundary

Prefer one Models-local typed helper, `AgentToolJournalJsonSize.Count(AgentToolJournalRecord)`,
which calls the **synchronous Stream overload** `JsonSerializer.Serialize(sink, journal)`
with no options argument. Its private counting/discard stream retains only a checked
`long` count. Replace only `SerializeToUtf8Bytes(this).Length` in `ValidateCore` with
that helper. Keep the preceding raw-payload sum, its short circuit, every semantic
validation, the exception message and the existing identity-keyed
`ConditionalWeakTable` unchanged.

The actual journal limit is **24 MiB**, not 64 MiB:
`AgentToolJournalModels.cs:208` defines `MaximumUtf8Bytes = 24 * 1024 * 1024`.
Individual protocol envelopes permit 8 MiB; prepared arguments permit 4,194,304
UTF-16 characters, and source preparation permits 65,536 UTF-8 bytes. These are
different limits. No constant, serializer option, wire version or acceptance envelope
changes in B. The unrelated retained-read/cache 64 MiB bounds are not journal limits.

Count the complete serialization, then compare the exact count with the same constant.
Do **not** throw when the sink first crosses 24 MiB. The previous expression completes
serialization before checking its length; early termination can hide a later serializer
failure. Continuing also avoids treating requested buffer capacity as actual encoded
length. The sink has constant retained state, but this is neither a hard allocation
ceiling nor a time/cancellation limit on validation. It does not replace graph validation.
This supersedes the earlier decision card's provisional suggestion to reject from the
sink immediately on crossing the bound.

The helper must not call `Validate`, copy or normalize the journal, create JSON options,
use Web defaults, supply a different type, return serialized bytes, or change the
validated-snapshot cache. Successful validation still inserts only after every check;
an exception never marks a journal valid. A record `with` expression is a new identity
and is validated independently. No logging, filesystem access, asynchronous work,
Models-to-Infrastructure reference or public/serialized witness is introduced.

## What the pinned .NET 10 implementation actually does

The accepted diagnostic ran .NET 10.0.12. These are primary-source observations at
that version, not a promise about every future serializer implementation:

| Source seam | Consequence for this choice |
| --- | --- |
| [`JsonSerializer.Write.ByteArray.cs`, `WriteBytes`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonSerializer.Write.ByteArray.cs#L120-L133) | The old API serializes into a pooled output and then calls `WrittenSpan.ToArray()`. B removes that final complete array from this validation step. |
| [`JsonTypeInfoOfT.WriteHelpers.cs`, synchronous Stream path](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Metadata/JsonTypeInfoOfT.WriteHelpers.cs#L232-L311) | The reflection path enables continuation, writes completed chunks to the stream and clears the pooled writer between chunks. Its `finally` returns the writer/buffer. |
| [`JsonSerializer.Write.Stream.cs`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonSerializer.Write.Stream.cs#L13-L19) and [`JsonConverter.ShouldFlush`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonConverter.cs#L168-L177) | Streaming uses a threshold based on 90% of capacity. It is a flush heuristic, not a bound on the next token or total scratch memory. |
| [`ObjectDefaultConverter`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Object/ObjectDefaultConverter.cs#L331-L364), [`IEnumerableDefaultConverter`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Collection/IEnumerableDefaultConverter.cs#L33-L47), and [`ImmutableEnumerableOfTConverter`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Collection/ImmutableEnumerableOfTConverter.cs#L8-L10) | Ordinary journal properties and immutable-array elements expose continuation points. There is no handwritten journal encoder or duplicated schema. |
| [`StringConverter.Write`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Value/StringConverter.cs#L14-L24) and [`Utf8JsonWriter.WriteValues.String.cs`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Writer/Utf8JsonWriter.WriteValues.String.cs#L169-L187) | A string is still written as one token. Escaping can rent a complete temporary character buffer before transcoding into contiguous output. |
| [`JsonWriterHelper.GetMaxEscapedLength`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Writer/JsonWriterHelper.Escaping.cs#L71-L76) and [`Utf8JsonWriter.Grow`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Writer/Utf8JsonWriter.cs#L1065-L1103) | The writer may request substantially more space than the bytes eventually advanced. An 8 MiB control-character payload can expand to roughly 48 MiB of escaped text even though the completed journal must be refused at 24 MiB. The sink cannot prevent that earlier token allocation. |
| [`JsonTypeInfoOfT.WriteHelpers.cs`, writer overload](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Metadata/JsonTypeInfoOfT.WriteHelpers.cs#L15-L54) and [`Utf8JsonWriter.Grow`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Writer/Utf8JsonWriter.cs#L1079-L1096) | Wrapping the sink in `new Utf8JsonWriter(stream)` and using the writer overload is not equivalent buffering: that serialization path does not enable continuation and the writer's stream buffer grows until flush. Use the Stream overload directly. |
| [`Utf8JsonWriterCache.ReturnWriterAndBuffer`](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Text.Json/src/System/Text/Json/Writer/Utf8JsonWriterCache.cs#L57-L65) | The serializer clears/returns rented buffers. Pool retention and peak process memory still need measurement; a sink with no byte storage does not establish zero allocations. |

The existing call has no generated context or custom options, and the journal graph
uses the normal reflected immutable models. Retain that selection. A future generated
fast path or non-resumable custom converter can buffer a larger unit; do not generalize
this helper into a framework-wide memory guarantee. Default escaping, null/default
omissions, numbers, property names and serializer errors remain the serializer's work.
Resource-exhaustion exception timing cannot be promised identical after an allocation
optimization; ordinary semantic/serialization refusal must remain explicit.

## Minimal source ownership

Paths are relative to the repository; source preparation, if released, must start from
the exact completed A predecessor and remain separate from historical/C3 activation.

| File | Edit |
| --- | --- |
| `src/MAF/Common/CanDoItAll.AgentFramework.Models/Execution/AgentToolJournalModels.cs` | Replace only the final encoded-length expression. Parent/current predecessor SHA256 `4C6B1BB44C041ADDBFF9866FC4866552CFE3F7832421A1AE717E0510D601D923`. |
| New `src/MAF/Common/CanDoItAll.AgentFramework.Models/Execution/AgentToolJournalJsonSize.cs` | One internal typed count method and private sealed Stream implementation. No generic serializer utility or new project reference. |
| `tests/Unit/CanDoItAll.Tests.Unit/AgentToolJournalSchemaFiveTests.cs` | Only make the existing class partial to share its real versioned journal fixtures with the new cohesive test file. Predecessor SHA256 `F5B3424B646919C07E6FB5471037A82C56DFD952ED2A45467E96DC7331C4EEC8`. |
| New `tests/Unit/CanDoItAll.Tests.Unit/AgentToolJournalSchemaFiveTests.JsonSize.cs` | The five prospective Facts below; no copied journal factory and no production test callback. Existing Models friend access already permits testing the internal helper. |

The private stream supports synchronous `Write(byte[], offset, count)`,
`Write(ReadOnlySpan<byte>)` and `WriteByte` through the same checked counter; Flush
does nothing. Read/seek/resize operations are unsupported. It owns no external resource
and retains no input span or payload. The helper disposes it locally. There is no need
for an asynchronous adapter, pooled-buffer implementation, user-selectable limit,
custom cancellation source or mutable global counter.

## Meaningful proof and cost decision

Source recount of the existing class is **18 rows**: ten Facts and two fixed four-row
theories. No dynamic provider, trait exclusion, inherited test or skip is present.
Those rows include existing raw-total and escaped-only over-limit refusal, legacy
versions, typed context, owner receipt/cancellation immutability and exact approvals.
The following **five new Facts are prospective**, not authored/discovered proof:

1. Count the existing real version 1–5 fixture shapes and compare with
   `JsonSerializer.SerializeToUtf8Bytes(journal).LongLength`. Include optional provider,
   typed-context and owner receipt fields, so default/null omission cannot drift.
2. Exercise default-escaped controls, quotes, backslashes, HTML-sensitive characters,
   non-ASCII, supplementary Unicode and a lone surrogate in actual nested strings.
   Compare bytes to the old API, including a valid large single envelope. Do not
   substitute `Encoding.UTF8.GetByteCount` as the encoded-length oracle.
3. Build semantically valid journals whose old serialized lengths are exactly
   `MaximumUtf8Bytes - 1`, exactly the maximum and `MaximumUtf8Bytes + 1`, using bounded
   ASCII padding across individually valid envelopes. Derive fixed overhead from the
   old serializer, independently assert those lengths, then assert the unchanged
   public Validate acceptance/refusal and exception message. All raw sums remain at
   or below the raw limit so this exercises the encoded check.
4. Use a shape whose old default serialization throws (for example a default immutable
   collection passed directly to the count helper), and require the same exception
   type without conversion to a size result. Public invalid journal validation still
   fails repeatedly; a valid original cached identity remains valid while a malformed
   `with` copy is refused. No instrumentation or reflection into the cache is needed.
5. Use the actual 512-batch by 64-proposal maximum with tiny valid payloads and unique
   existing identities/order; assert the full old/new byte counts and public validation
   agree. This proves complete traversal across many flushes, including trailing
   properties, rather than only one small buffer or large token.

Expected class total after exactly those additions would be 23, subject to a fresh
source recount before discovery. These are correctness assertions, not timing tests.
Keep the actual persistence/recovery and A witness suites: matching bytes, altered
current bytes, all publication boundaries and recovered intents must stay unchanged.
The parent determines the deduplicated final focused selection from the authored source.

For cost, use the same unprofiled 22 rows and reference envelopes after focused/static
proof, preserving every failed sample and source/runtime identity. Compare only equal
outcomes/phases and complete cohorts. Expected mechanical savings are the final full
byte-array copy and, for many small properties, aggregate-buffer growth at this one
ValidateCore call. Encoding, traversal, raw sums, hash sets, digest work, escaped-token
scratch, durable previous/target intents and file serialization remain. A cached
immutable journal already skips ValidateCore; B helps only newly validated identities.

The accepted whole-host report attributed 266,486,648 weighted sampled bytes across
13 allocation samples to visible ValidateCore stacks. Report SHA256 is
`3E4CDEFC8D5E14193B6A35A74074CFA8EE01DCBF7D5345643324B2C19C86A4BC`.
This is evidence for the seam, not a per-operation saving, call count, retained-memory
amount or capacity forecast. Maximum and near-byte-limit workloads can still fail.
No forced GC, raised memory budget, relaxed envelope, new payload format or validation
skip belongs to this candidate.

## Rejected alternatives and remaining gates

- `Stream.Null` alone loses the length check. A MemoryStream/ArrayBufferWriter still
  retains output; `SerializeToUtf8Bytes` then discarding it retains the present cost.
- A custom `IBufferWriter<byte>` can discard advanced spans but must still satisfy
  token size hints, own/recycle writable memory and align writer options. It adds
  buffer/error obligations without a demonstrated benefit over the built-in stream
  serializer. Revisit only if the measured stream candidate fails for this specific seam.
- Hand-counting field JSON or using raw UTF-8 string totals duplicates escaping/schema
  rules and misses future serialized properties. It is not an equivalent bound.
- Early sink abort, new per-field caps, relaxed escaping and asynchronous validation
  change behavior or scope; none is needed to remove the final array.
- Reusing validation across unrelated journal instances or trusting revisions changes
  the immutable-cache boundary. That is outside B.

Parent approved the exact behavior and four-file ownership before source preparation.
Before any claim of improvement: independent source review, standard Release compilation, exact
focused discovery/execution, full portability/static closure and matched performance
evidence. Source preparation does not constitute runtime or capacity proof.
