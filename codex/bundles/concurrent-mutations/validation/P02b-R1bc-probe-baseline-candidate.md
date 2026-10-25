# Receipt probe initialization repair and baseline

Status: the owning Integration Release build and full static procedure passed. All 22
fresh-host performance rows executed: 10 passed, 12 failed, none skipped. The preceding
[Send-state checkpoint](P02b-R1bc-send-state-baseline-candidate.md)
passed all 80 native/API cases. Its 22 performance attempts failed before measurement,
with complete cleanup evidence; they remain preserved.

The probe compares activity names against the existing `AgentFrameworkTelemetry.SourceName`
constant. It no longer dereferences the static source during listener registration.
No production code, fixture shape, sample count or performance assertion changes.
Fresh-host execution itself provides the regression arrangement that the earlier
warm-process checks did not exercise.

Refresh only the owning Integration assembly in standard Release, then run the full
portability procedure without baseline rewriting. Discover the aggregate 22 rows and
each individual row before executing those 22 in separate hosts. The unchanged 80-case
production repair proof is retained separately; this harness-only edit does not justify
repeating it. Stop later rows if a workload fails before recording its first sample,
after preserving and verifying terminal ownership/cleanup evidence.

Evidence root: `.artifacts/concurrent-mutations/validation/r1bc-probe-baseline-validation`.
The frozen complete-source inventory has 8,604 inputs and SHA256
`4F06EE4A47CDF7A74AAE73F9C2EE5CE597C173441F66E9549FEB98F78045AE45`.
Selection SHA256 is `DF5FFF63BA66EED486F3BACC8ADEFA5E4D534E62B085FB0141B0AE92FC2F2C63`.
The completed summary SHA256 is
`2109FBB7B9148C8DE5A39608E3B5FC050FCC79A583AC2D4E3F809DBD480E565F`.
All 23 discoveries (aggregate plus individual selections), runtime labels/definitions,
source/binary identities, separate host identities and fixture cleanup were verified.
Static proof includes 17 tooling self-tests, rendering 1198, documentation 426, and
final full-source portability enforcement with 15,467 unchanged allowances and no write.

Passing rows: CoreSlice one/four proposals; FreshDetail one/four/maximum serial and
maximum independent two/four; NativeSessionRefresh one/four/maximum serial. Nine
rows failed the memory envelope and three CoreSlice concurrent rows failed cancellation
checks. No limit, workload or GC setting was relaxed. Incomplete rows do not have a
seven-sample measured median.

CoreSlice maximum serial completed only cold plus two warmups. Allocations were
1,957,614,488 / 2,145,666,432 / 1,745,083,336 bytes; entry private bytes rose from
625,405,952 to 1,686,687,744 and 2,653,798,400. The row then refused its 2 GiB entry
criterion. These measurements include ownership disposal and response work; earlier
partial-window measurements are not a matched comparison.

The unchanged source/binaries were used for one diagnostic collection. Its testhost
exited naturally and fixture cleanup passed, but the 32,314,087-byte NetTrace V4 stream
was refused by TraceEvent 3.1.23 during block parsing at physical EOF. Two interpretations
preserve that refusal; no allocation attribution is accepted. Natural host exit alone
does not prove EventPipe session finalization. See the
[UTF-8 candidate record](P02b-R1bc-utf8-candidate.md) for the next narrow experiment and
the remaining explicit-session-stop diagnostic requirement.

The JSON optimization, historical reader and Workbench C3 were excluded from this
baseline. Filesystem-operation corroboration, real browser Send proof, broad validation
and existing signing/cleanup blockers remain open. No capacity or end-to-end closure.
