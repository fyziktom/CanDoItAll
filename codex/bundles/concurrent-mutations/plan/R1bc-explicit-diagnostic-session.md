# Receipt diagnostic collection with an explicit session boundary

Status: parent-authorized, managed-checkout source preparation only. No build, collection, test execution or performance qualification is claimed. This succeeds the startup-session route in [the allocation diagnostic plan](R1bc-allocation-diagnostic-execution.md) after retained collection evidence proved that route did not finalize its stream. The parent schedules this diagnostic **after** the matched unprofiled 22-row candidate comparison. It is a separate diagnostic harness binary, with unchanged production binaries and measured workload algorithm, not the identical earlier baseline test binary.

## Observed failure and selected correction

The original owned PID 16076 ended naturally from the runner's perspective, but its 32,314,087-byte V4 trace could not be converted. Both strict parser attempts refused; the second identified `RewindableStream.Read` through `FastSerializationObjectParser.ReadBlockHeader`. An authorized read-only framing inspection found 601 complete blocks: Trace 1, Metadata 12, Stack 265, Event 311, SequencePoint 12. The last EventBlock starts at 32,238,776, has payload offset 32,238,808 and length 75,278, and a valid EndObject footer at 32,314,086. Physical EOF immediately follows; the mandatory NullReference end tag is absent. SHA256 remained `B26BB78A7832D26E0B5E094700A286BE12C92862AC1994EC9B537BC76D546F62`. No payload was decoded, printed or modified during this inspection.

This is incomplete session finalization. It does not establish why host shutdown omitted finalization, or that a parser upgrade would repair it. Runtime v10.0.12 flushes blocks and emits the end tag when freeing the EventPipe file; TraceEvent rejects physical EOF while awaiting a block header. Preserve both refused interpretations and the raw trace. Never append a terminator, suppress parsing errors, or reinterpret missing rundown as zero work. [Runtime finalization](https://github.com/dotnet/runtime/blob/v10.0.12/src/native/eventpipe/ep-file.c#L135-L144), [strict EOF behavior](https://github.com/microsoft/perfview/blob/v3.1.23/src/TraceEvent/EventPipe/RewindableStream.cs#L62-L78).

Use one explicit IPC `EventPipeSession`, owned by a separate bounded collector. Do not enable startup EventPipe simultaneously. The existing installed DiagnosticsClient 0.2.661903 exposes asynchronous start, `EventStream`, `StopAsync(CancellationToken)` and `Dispose`; local metadata was inspected without invoking them. No installation or production dependency is needed. [Official client API](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostics-client-library).

## Exact source boundary

Tracked changes are limited to:

- `tests/Integration/CanDoItAll.Tests.Integration/AgentReceiptStoreDiagnosticSession.cs`: BCL-only optional handshake and one shared, bounded wire codec.
- `AgentReceiptStorePerformanceEvidence.cs` in that directory: retain the existing row/source identity for the handshake and include its source hash in evidence.
- `AgentReceiptStorePerformanceTests.cs`: begin before the idle sampler/setup, stop after existing workload/fixture cleanup and terminal evidence, preserve failures independently.

The collector is ignored task tooling under `.artifacts/concurrent-mutations/diagnostics/collector`, with an ownership record, standard `Release/net10.0` console project, source inventory and README. It links the same handshake source file instead of copying a codec. Its only external references are the installed DiagnosticsClient and its existing Logging.Abstractions/DependencyInjection.Abstractions dependency closure. No new app interface, provider, receipt abstraction, database operation, telemetry framework, startup assembly or NuGet package is introduced.

The pre-edit Tests and Evidence hashes matched main exactly: `86DDBFECA7407E157EA641056516D1126267E740EFF9FA137D9C4F205CADD1C0` and `A19645D4BB6E1960DC529B095D0A64075794D7AC26E08ACABFE82B8C849BC260`. All 22 explicit InlineData cases and their LongRunning trait remain unchanged. New test discovery count is zero. Source review, compilation and the one-row runtime protocol proof remain separate gates.

## Bounded protocol and lifetime

The parent pre-records and creates a fresh empty control directory below the selected row's owned output root. It supplies only that testhost with `CDITALL_RECEIPT_DIAGNOSTIC_ROOT` and a fresh nonempty GUID-N `CDITALL_RECEIPT_DIAGNOSTIC_NONCE`. Both absent is the ordinary unprofiled path; partial/invalid configuration fails explicitly. Existing startup EventPipe enable variables cause refusal.

Each control message is at most 4 KiB, strict schema version 1, and binds the nonce, exact process ID/start UTC, original row ID, frozen source identity hash and actual runtime string. The parent derives the collector's separate expectation file from the verified immutable intent and owned process tree, never from a process-name search. Every phase must match that complete identity. The collector explicitly acquires `host.SafeHandle`, rechecks identity after acquisition, and retains the handle through stop/session cleanup. The retained-handle PID-reuse argument applies to the selected Windows host/runtime and needs actual collection proof; this is not a claim of portable atomic attachment. It never kills a process.

1. The test writes `ready.json` after its existing intent and before any measured/setup sampler. It waits at most 30 seconds for `started.json`. The dedicated directory must be empty initially; preexisting/replayed messages refuse.
2. The collector starts exactly `Microsoft-Windows-DotNETRuntime`, Verbose, keyword mask `0x40020019`, 128 MiB buffer, rundown and stackwalk enabled. It arms a 64 KiB asynchronous stream pump, then writes the started acknowledgement. Only then may the test proceed. Discovery/host startup and intent generation are outside the trace; all setup, workload cohorts and owned fixture cleanup follow admission.
3. After existing cleanup and complete evidence, the test writes `stop-requested.json` and waits up to 120 seconds, even if the workload failed. The request uses an independent deadline, not a cancelled workload token.
4. The collector calls `StopAsync` on exactly its own session while continuing to drain. It awaits the stop response **and** stream EOF, then flushes, closes and hashes the trace. Session disposal is not treated as a replacement for acknowledged stop. The test may return or rethrow only after its matching stopped acknowledgement or the bounded failure deadline.

Messages use CreateNew and exclusive sharing during their small write; there is no overwrite, stale message eviction or automatic retry of an operation/session. Readers poll at 100 ms only outside measured windows. A missing file or sharing violation while its writer is active may be retried within the existing deadline; malformed, wrong-identity or unsafe-path inputs fail immediately. Paths must remain under the precreated owned root without reparse components. This assumes the parent exclusively owns that directory; it is not a defense against a hostile same-user process replacing directory components.

## Limits and outcome truth

The collector has a 600-second operation deadline. Stop and stream drain share 90 seconds, with up to five seconds to observe pump termination after exceptional disposal, leaving room inside the test's 120-second end wait for final evidence. The parent's independent watchdog still bounds synchronous local filesystem operations and the entire owned process tree. It retains the existing memory, disk-headroom and 16 MiB stdout/stderr pump controls. No test timeout is extended automatically.

At 448 MiB observed stream data the collector requests stop and marks collection refused. It retains at most 512 MiB in the trace file; excess bytes are drained without storage until bounded shutdown and recorded as truncation. A trace stopped for size/time or before the host's requested end cannot be accepted. No in-memory full trace/string is built. At least 2 GiB free output-volume headroom is required. Filesystem flush/hash duration remains subject to the parent watchdog, not a claimed cancellable I/O guarantee.

Workload failure, fixture cleanup failure and diagnostic failure are retained separately. A diagnostic failure cannot turn a failed workload into success, erase its original exception, or claim all seven measured samples ran. A passing workload with failed collection remains diagnostically refused. `collectionAcknowledged` means only that the explicit stop/EOF/file closure protocol completed; offline strict parsing, exact PID/runtime, rundown, event-loss, stack-resolution and source/binary identity gates remain mandatory. The root runner must additionally prove the exact host exited and its output pumps drained normally.

Do not sum this traced row into matched performance samples, force GC, increase the original memory envelope or hide profiling overhead. The trace now covers the controlled workload interval rather than the entire process lifetime. It still lacks exact per-sample EventPipe markers and cannot attribute all samples precisely to measured windows. The previously failed baseline remains valid evidence of failed reference-envelope admission, not seven successful samples.

## Parent validation and release

Before execution: independent source review; standard Release collector and Integration builds serialized by the parent; required repository static gates; one exact LongRunning row discovered; all production/test/PDB/source identities retained. Record the explicit three-file diagnostic harness delta and compare production assemblies against the candidate independently. The existing 22-row unprofiled comparison must finish first.

The first runtime proof is one owned CoreSlice/MaximumProposals/Serial-1 diagnostic row, including its natural failure if the memory envelope still rejects it. Require matching ready/start/end/stop identities, explicit stop response, drained stream, closed/hash-stable trace, natural host exit, preserved workload outcome and cleanup. Prove missing collector, wrong-identity acknowledgement and collector interruption are bounded refusals before trusting the mechanism generally; these are prospective focused protocol arrangements, not additional discovered source tests. No automatic recollection is authorized by a failed attempt.

No physical filesystem-operation, flush-to-device, native allocation-root or retained-object-root proof is added. Those limitations in the original diagnostic plan remain open.
