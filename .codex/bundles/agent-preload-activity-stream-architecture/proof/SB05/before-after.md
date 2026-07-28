# SB05 Backend Before/After Evidence

## Measurement boundary

The same four-scenario PostgreSQL integration test and the same real
`FileSandboxWorkspaceStore` are used before and after. The runtime remains a
deterministic barrier so provider/network first-token latency is excluded.

The timing start milestone is intentionally stricter after the change:

- SB01 starts at the first catalog load because no earlier activity existed.
- SB05 starts at the synchronously published typed `Accepted` activity.
- Both end when the runtime is entered.

The durations are therefore descriptive and not a controlled apples-to-apples
latency benchmark. In particular, a favorable SB05 duration cannot be attributed
solely to the implementation, while an SB05 regression includes work that SB01 did
not measure. Operation counts and milestone ordering are the primary gate.

## Five final post-change repetitions

Milliseconds from typed `Accepted` publication to runtime entry:

| Scenario | Samples | Median | Observed p95 |
| --- | --- | ---: | ---: |
| Cold/new | 231.915, 257.906, 220.631, 578.993, 230.016 | 231.915 | 578.993 |
| Warm/new | 241.942, 316.867, 273.891, 214.265, 234.413 | 241.942 | 316.867 |
| Cold/existing | 153.681, 164.588, 185.760, 168.137, 183.230 | 168.137 | 185.760 |
| Warm/existing | 396.294, 433.011, 392.603, 527.575, 407.779 | 407.779 | 527.575 |

`Observed p95` uses the same nearest-rank convention as SB01. With five local
samples it is the maximum and is highly sensitive to filesystem/PostgreSQL/host
scheduling noise. The cold/new 578.993 ms sample is preserved rather than removed.

## Descriptive comparison with SB01

| Scenario | SB01 median | SB05 median | Median delta | SB01 observed p95 | SB05 observed p95 | p95 delta |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cold/new | 261.325 | 231.915 | -11.25% | 269.882 | 578.993 | +114.54% |
| Warm/new | 240.370 | 241.942 | +0.65% | 262.238 | 316.867 | +20.83% |
| Cold/existing | 212.254 | 168.137 | -20.79% | 277.801 | 185.760 | -33.13% |
| Warm/existing | 484.637 | 407.779 | -15.86% | 536.997 | 527.575 | -1.75% |

Three medians improve, warm/new is effectively unchanged, and the two new-session
p95 values do not improve. Because the start milestone changed and the sample is
small/noisy, SB05 does not claim a statistically proven wall-clock speedup.

## Material backend improvement

The deterministic improvement is structural:

- the UI/API-observable `Accepted` activity is synchronous and precedes catalog,
  provider, persistence, and runtime work;
- two catalog loads become one immutable catalog-snapshot read;
- the provider registry read becomes one O(1) immutable snapshot acquisition with
  no per-dispatch provider database read;
- existing-session duplicate reads and the startup summary scan are removed;
- run admission becomes one atomic persistence operation with typed recovery;
- warm preparation reuses only immutable configuration and never pools live agents,
  tools, sessions, credentials, authorization, or `DbContext`.

This meets the backend purpose of removing avoidable work and making the remaining
I/O gap observable without concealing the wall-clock variance.
