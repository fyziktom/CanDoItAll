# Receipt JSON UTF-8 candidate

Status: the frozen UTF-8 candidate run is closed: 84 executed, 72 passed, 12 failed, zero skipped. All 62 compatibility cases passed; the 22 performance rows produced 10 passes and 12 failures. Maximum-shape capacity remains open. The later [controlled diagnostic](R1bc-controlled-allocation-diagnostic.md) supplies accepted sampled allocation stacks on unchanged production binaries; it is separate from this matched experiment.

The closed [probe baseline](P02b-R1bc-probe-baseline-candidate.md) executed all 22 rows
with 10 passed and 12 failed. Its maximum CoreSlice serial row allocates approximately
1.7–2.1 GB per completed operation and exceeds the entry-memory envelope before its
measured samples. The first allocation trace is incomplete and is explicitly refused;
it supplies no accepted stack attribution. Product/runtime diagnosis remains open.

The smallest source-proven avoidable cost is the whole JSON UTF-16 string followed by
UTF-8 re-encoding in `FileSandboxWorkspaceJsonStore`. Non-provider-usage payloads now
serialize directly to UTF-8 once and enter the same `DurableFileWriter.WriteBytesAsync`
path used by the previous text writer. Serializer options, durable flush/publication,
path validation, cancellation and write telemetry are retained. Provider usage keeps its
observed string path because the usage index/history protocol consumes that representation.
The forwarding private string method returns the existing Task without an extra async
state machine. Only changed method braces are normalized to the repository style.

The managed candidate was independently reviewed. Parent integration adds the two
[compatibility Facts](R1bc-json-utf8-compatibility-source.md); the passing selection covers actual
old/new persisted bytes, object-typed payloads, Unicode/converters, durable publication
failure, and provider-usage index/history recovery. Parent production SHA256 before this
experiment was `D575EDEA1863934B698C205DA84A544EF8B03A23C7EBB62BD93E40743A0F4704`;
the adopted source is `9A580CD4B357B814E079B5817C74C32EEE94AD6430839BD40AA5F3043DE4C985`.

The executed validation order was to refresh standard Release Persistence, Unit and Integration outputs;
run the full portability/static procedure, review any findings and require final no-write
enforcement; freeze complete source and binaries; discover and execute 47 Unit plus
15 file-owner Integration cases; then discover and execute the identical 22 performance
rows in separate hosts. The existing isolated PostgreSQL fixture is reused for the 15
Integration cases. Historical reads, C3, browser source and unrelated changes stay out
of this experiment. No forced GC, memory-budget increase, global lock or workload change.

Evidence root: `.artifacts/concurrent-mutations/validation/r1bc-utf8-candidate-validation`.
The runner retains a 900-second process deadline, bounded output pumps and explicit
owned-process cleanup even when termination fails. A failed compatibility selection
blocks the later performance rows. Source, binaries, exact runtime/discovery identities,
unique hosts and fixture cleanup must match before interpretation. Complete-row medians
are compared only where both sides have all seven measured samples; incomplete rows
retain per-sample evidence and their failures.

## Closed execution and retained identities

The three standard Release builds passed. The full portability scan covered 8,539 files;
final enforcement without writing the baseline passed with 15,467 reviewed executable-source
findings unchanged. The rendering register passed for 1,198 inputs; rendering, baseline and
secret-scanner self-tests passed 7, 6 and 4 respectively. Exact discovery/execution matched
47 Unit and 15 file-owner Integration cases, all passed, followed by all 22 separately hosted
LongRunning rows. The separate earlier 80-case checkpoint is not added to this 84-case total.

The parent retained source/binary and discovery-label checks as matching through execution.
Post-execution verification covered 8,608 source-manifest paths with no path or byte drift.
All 22 performance hosts exited and owned fixture cleanup was verified, including failed rows.
These observations concern the frozen experiment; later diagnostic harness edits are a new
candidate and cannot be substituted for its recorded identities.

| Evidence identity | SHA256 / identity |
| --- | --- |
| Parent proof document before this documentation-only update | `4335547EE2E7410745F32C6584A5FF8E8D0C2DCEAF70F88143B42000FB7FA332` |
| Frozen candidate source identity | `271aecd5e34c3df94ce1a0ffb4cc0a7257136460cde398bcca6eb5ccfff1b3ce` |
| Selection manifest | `74DD0DFDEA91215D305824C38E34BF6CD93E180F11740E9D757948EB47E0A92B` |
| Complete source manifest | `EBF4CC6B0D2DE66CAB6DD39B2A7D804A57E8ABB3A4C44C4928BD199C942DC19E` |
| `focused-candidate.json` | `20C35F80E5CAEDF7E2AB4FAD46FD0641E1CC0479FCC3FE2D04C85E8CD1617DF8` |
| Candidate `completed-summary.json` | `BC24CD446CBC8002A1F54E19A843D02E46E5BD1C9B5373FA86E59DEB8834E42A` |
| `matched-comparison.json` | `14368EEF858753A7FA6EF1310F35FD19CB8F747BC0354216CF1826D118DF4947` |
| Compared baseline summary | `2109FBB7B9148C8DE5A39608E3B5FC050FCC79A583AC2D4E3F809DBD480E565F` |

The runtime recorded by the candidate intent is .NET 10.0.12. The source identity is a
content freeze, not a new commit; the verified parent HEAD was
`702d0a93b1081c42d5a06893e7070219aee5d089`. Per-lane TRX, command, binary, intent and
completion hashes remain in the evidence root and its summary.

## Exact performance failures

The 12 failed TRX results classify as seven reference-profile headroom refusals, four
aggregates containing `TaskCanceledException`, and one observed operation-window
incremental-private-memory violation. Cancellation is not relabeled as memory refusal or
as successful bounded reconciliation. Each row ran once; none was retried to improve the
comparison. The sample count below counts recorded samples, including a failed sample;
it does not assert that every worker returned a completed result.

| Failed candidate row | TRX classification | Recorded samples / measured samples |
| --- | --- | ---: |
| CoreSlice / MaximumProposals / Serial 1 | Cancellation; zero returned worker results in its cold sample | 1 / 0 |
| CoreSlice / NearMaximumBytes / Serial 1 | Pre-operation reference headroom refusal | 3 / 0 |
| FreshDetail / NearMaximumBytes / Serial 1 | Pre-operation reference headroom refusal | 7 / 4 |
| NativeSessionRefresh / NearMaximumBytes / Serial 1 | Pre-operation reference headroom refusal | 5 / 2 |
| CoreSlice / MaximumProposals / Independent 2 | Pre-operation reference headroom refusal | 2 / 0 |
| CoreSlice / MaximumProposals / Independent 4 | Cancellation; one returned worker result, three cancellations | 1 / 0 |
| CoreSlice / NearMaximumBytes / Independent 2 | Pre-operation reference headroom refusal | 2 / 0 |
| CoreSlice / NearMaximumBytes / Independent 4 | Observed operation-window incremental private bytes exceeded the declared four-run reference envelope | 1 / 0 |
| FreshDetail / NearMaximumBytes / Independent 2 | Pre-operation reference headroom refusal | 3 / 0 |
| FreshDetail / NearMaximumBytes / Independent 4 | Pre-operation reference headroom refusal | 1 / 0 |
| CoreSlice / MaximumProposals / Shared 4 | Cancellation; zero returned worker results, four cancellations | 1 / 0 |
| CoreSlice / NearMaximumBytes / Shared 4 | Cancellation; three returned worker results, one cancellation | 1 / 0 |

Headroom refusals mean the declared reference profile was not met; they are not an
application-capacity measurement. The four-run incremental-memory violation is an observed
reference-envelope failure. Incomplete rows keep their partial samples without a qualified
seven-sample median. In particular, the two detail/refresh rows with four/two measured samples
are still failed rows.

## Matched comparison and limits

Prepared shapes match for all 22 rows. Baseline and candidate are sequential, unprofiled
runs on one machine with the same matrix/harness, runtime and prepared bytes. Both sides
pass the same ten rows; each of these has seven measured samples. Their median changes
are mixed (candidate relative to baseline):

| Complete row | Elapsed median change | Process-allocation median change |
| --- | ---: | ---: |
| CoreSlice / OneProposal / Serial 1 | -12.50% | -6.63% |
| CoreSlice / FourProposals / Serial 1 | +4.35% | -0.37% |
| FreshDetail / OneProposal / Serial 1 | +13.41% | approximately 0.00% |
| FreshDetail / FourProposals / Serial 1 | +12.73% | +0.04% |
| FreshDetail / MaximumProposals / Serial 1 | -12.30% | -0.17% |
| NativeSessionRefresh / OneProposal / Serial 1 | -12.12% | -0.01% |
| NativeSessionRefresh / FourProposals / Serial 1 | -10.54% | -0.02% |
| NativeSessionRefresh / MaximumProposals / Serial 1 | -10.40% | +1.95% |
| FreshDetail / MaximumProposals / Independent 2 | -15.55% | -0.03% |
| FreshDetail / MaximumProposals / Independent 4 | -6.19% | approximately 0.00% |

These medians establish no general speedup. Process allocation includes the whole measured
process window and is not an allocation-stack attribution or a retained-heap measurement.

The maximum-proposal CoreSlice serial cold samples have different outcomes: the baseline
returned a confirmed result examining four proposals, while the candidate cancelled and
returned zero results. Their raw allocation values (1,957,614,488 versus 1,551,931,224 bytes)
therefore **must not be presented as a 20.72% optimization**. Both rows have zero measured
samples; the candidate recorded only its failed cold sample.

The near-maximum-byte CoreSlice serial cold sample has matching returned outcomes. It
observed allocation of 1,963,134,320 versus 1,427,387,272 bytes (-27.29%) and elapsed time
of 2,471.527 versus 2,679.0939 ms (+8.40%). This is one comparable cold datapoint inside
incomplete rows (zero measured samples on both sides), not performance qualification.
The original 22-row baseline and candidate results remain failed overall. The next work
is sound allocation/GC attribution and a separately reviewed bounded owner-path change;
the capacity, native refresh, physical-I/O and activation gates are not closed by UTF-8
byte compatibility or these partial observations.

## Diagnostic refusal and next collection

Diagnostic collection root is `r1bc-baseline-gc-diagnostic-v2` under the validation root.
The original attempt failed during discovery on an empty StrictMode sum and created no
trace/workload fixture; its process exit and unchanged source were verified. V2 repaired
only the empty enumeration and used the exact baseline source/binaries. It collected
one maximum serial CoreSlice row with two samples, zero measured, on .NET 10.0.12.
The retained stream SHA256 is
`B26BB78A7832D26E0B5E094700A286BE12C92862AC1994EC9B537BC76D546F62`.

The standalone parser builds in standard Release and uses pinned installed TraceEvent
3.1.23 without modifying installed tools. It checks complete parsing, loss, PID, product
runtime version, rundown, resolved application allocation frames and bounded output.
The product version uses the event's BCL product fields; VM fields report the separate
CLR file build. Both attempts fail at EventPipe conversion with a FormatException in
RewindableStream.Read/ReadBlockHeader, before any accepted allocation results. No parser
error is suppressed and no missing trace terminator is synthesized. Both parser hosts
closed normally, with bounded output and no cleanup failures.

Next collection requires explicit graceful session stop while the workload host remains
alive, then complete stream/rundown verification. A runtime testhost's natural exit is
insufficient evidence that its startup EventPipe stream finalized. The direct UTF-8
experiment proceeds on the source-proven redundant encoding and exact compatibility
tests; it does not discharge this diagnostic, physical-I/O or capacity gate.
