# Controlled allocation diagnostic and refusal proof

Status: collection, strict interpretation and three real protocol-refusal arrangements
closed at 2026-10-17T03:03:35Z. This supplies sampled owner-path attribution; it does
not qualify capacity or turn failed performance rows into passes. The preceding
[UTF-8 comparison](P02b-R1bc-utf8-candidate.md) remains 62 compatibility passes and
22 performance rows with 10 passes and 12 failures.

## Frozen candidate and execution

The explicit diagnostic harness and collector built in standard Release. The
693-file Integration runtime comparison changed only its test assembly and PDB;
all production binaries remained identical to the UTF-8 candidate. The complete
source manifest covers 8,612 paths. The collection/refusal runtime identity covers
702 files, including the separate collector. No dependency was installed.

The harness candidate passed all 17 scanner/rendering self-tests, rendering validation
for 1,198 inputs, documentation validation for 426 files and nine documentation-evidence
cases. The full portability scan produced 36,566 findings; final enforcement without
writing the baseline passed with 15,467 reviewed executable-source findings unchanged.
Logs are retained in `.artifacts/concurrent-mutations/validation/r1bc-explicit-gc-diagnostic`.

The first launcher attempt failed before workload entry because PowerShell's
case-insensitive environment-loop variable shadowed its process-name parameter.
The exact owned launcher tree was stopped, output drains closed, and no workload,
fixture or trace was created. That attempt remains retained. The separately named
V2 runner fixes the variable collision and uses a fresh owned output root.

V2 collected the one selected `CoreSlice / MaximumProposals / Serial-1` row from
host 43404, started at `2026-10-17T02:42:10.3870366Z`, on .NET 10.0.12 with
workstation GC. The actual test failed its pre-operation idle/headroom reference
profile after three samples and zero measured samples. The host exited naturally
and its fixture cleaned up. Explicit stop, stream EOF, output drainage, trace hash,
source/binary identity and collector exit all passed independently of that workload
failure. The complete trace is 40,219,650 bytes.

## Strict interpretation

The first two interpretations refused the suspension timeline. Bounded event detail
showed 220 overlapping starts on different threads, followed by 220 unmatched ends:
the old matcher keyed only by CLR instance. All four phase counts were 1,329.
Runtime suspension attempts can overlap before acquiring the runtime suspension lock.
The corrected matcher requires the same CLR/thread and four ordered phases, with
explicit bounds and rejection of duplicate, absent, unsupported, backward or unclosed
events. It performs no cross-thread repair. All other trace acceptance gates remain.

The parser built with zero warnings/errors. Nine tests passed, including the actual
two-thread interleaving, missing/out-of-order/duplicate phases, nonfinite/backward time,
unfinished streams, identity isolation and exact state/detail bounds. The same immutable
trace then passed every gate in `analysis3`; the two refused reports remain unchanged.

Accepted evidence: 149,431 expected-process events, 11,433 allocation samples, 8,858
application-stack samples, complete rundown, zero lost/foreign/truncated events and
zero invalid/open attempts or timestamp regressions. All samples have stacks; seven
of 586,221 frames are unresolved. The 1,329 suspension attempts comprise 100 GC,
eight GC preparation and 1,221 other runtime attempts. Their windows include acquisition
and restart overhead; they are not summed as fully stopped GC-pause time.

The whole-host sampled allocation weights total 5,011,972,792 bytes. Arrays and
strings dominate. The largest displayed owner stack passes through
`JsonStore.Serialize/RequiresSave` and `EnsureStoredPayloadIsTransitionCompatible`.
Seven displayed stack groups containing that owner path carry 885,029,656 weighted
bytes across 13 samples. This identifies repeated whole-run comparison as the next
narrow optimization candidate. It does not predict savings or isolate a measured
operation: setup, warmup, failed workload and cleanup are all present. No retained-root,
native-allocation or physical-I/O conclusion follows.

## Actual protocol refusals

Each arrangement independently discovered and executed exactly one existing test row;
all three retained the expected failed TRX result. These are three invocations, not
three new test methods or three passing performance rows.

| Arrangement | Observed behavior | Terminal evidence |
| --- | --- | --- |
| Missing collector, V2 | No collector or acknowledgement; no workload entry; actual 30.033-second start and 120.006-second completion deadlines | Expected refusal, natural host exit, fixture absent, no parser evidence |
| Wrong Started identity, V2 | Actual Ready followed by a wrong nonce and matching explicit refused stop; no workload entry | Both handshake errors retained; expected refusal, natural exit and cleanup |
| Interrupted collector, V3 | Actual Started and a unique bounded current-row idle marker precede the recorded exact collector kill | Actual 120.004-second completion timeout, natural testhost exit and cleanup; 196,608-byte partial trace permanently ineligible for parsing |

The V2 interrupted arrangement was superseded before discovery because Started alone
did not prove the testhost consumed the acknowledgement. V3 retained the idle marker's
hash/time before interruption and revalidated it at closure. This proves workload entry,
not completion of an owner operation. The deliberate collector exit is recorded separately
from unexpected cleanup failure. All three arrangements retained false diagnostic
acknowledgement and emitted no parser-admission evidence. Source/runtime closure passed.

## Evidence identities and remaining work

Paths below are relative to
`.artifacts/concurrent-mutations/validation/r1bc-explicit-gc-diagnostic-v2` unless stated.

| Input or result | SHA256 |
| --- | --- |
| Complete source manifest | `89BB703645677DAED8F71C83743E7505980F4BDFDB5C7867C95B81628B917C5F` |
| Completed diagnostic summary | `2425792164FA90D85048CD0E3D307F5C41B24866053D3D9145BA62DDDFB0CDCE` |
| Complete trace | `FC54220584A8FF9197D9EC4AEEE1DB849A216F75F54E32E05460437A0F9FB820` |
| Parser source inventory | `BD7A6FC85FF65BCC087BCE8D537377B8D2129D46F64875DAEFF1C584A8523F79` |
| Parser binary | `A1A5B64C7273BDAF20661C074EB5FA2CE56AD676E178699B5E14E1A00BA8971F` |
| `analysis3/report.json` | `3E4CDEFC8D5E14193B6A35A74074CFA8EE01DCBF7D5345643324B2C19C86A4BC` |
| `interpretation-3-closure.json` | `E8B8E3B8DB9917F9765A306CCF3CDBBD348738C3529A6E44A4CC49AC9EB33857` |
| Missing-collector closure in sibling `r1bc-explicit-gc-refusal-v2-missingcollector` | `0AA6E3B7A2FD06C44759F6EFC1D02C1782D7A27AEBA6B86601F29F54B046884B` |
| Wrong-identity closure in sibling `r1bc-explicit-gc-refusal-v2-wrongstartedidentity` | `14EB299FA3EACBB59FC6236D6B690D4C338D8F7912B47CA117EFD167C3267A5E` |
| Interrupted closure in sibling `r1bc-explicit-gc-interrupted-refusal-v3` | `9D9DE506763516446B00D77B6476A5176A8F74EA3DBF7DE5336E0A580DDAE8FE` |

The main validation freeze is released for the reviewed next candidate. Run-only
immutable evidence reuse must retain a fresh exact-byte check, full mismatch and
recovery validation, all authority fences and durable publication semantics. Its
implementation, correctness proof and matched unprofiled measurement are pending.
Historical/C3 work remains separate from that comparison. No producer activation,
broad-suite closure, capacity qualification or end-to-end completion is inferred.
