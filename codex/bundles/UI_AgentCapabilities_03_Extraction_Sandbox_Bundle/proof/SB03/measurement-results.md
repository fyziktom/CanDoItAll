# Direct watch results

All figures are direct source-flush-to-visible observations on the frozen machine, SDK, browser and live sibling graph. Process-cold startup includes the normal interactive fixture gate and populated restore/filesystem caches. It is separate from warm editing.

## Process-cold startup

| Host | Runs | Minimum (s) | Maximum (s) | Range (s) | Median (s) |
|---|---:|---:|---:|---:|---:|
| pre-fullapp | 3 | 56.865 | 58.040 | 1.176 | 57.959 |
| post-fullapp | 3 | 58.558 | 60.264 | 1.706 | 58.628 |
| parity | 3 | 15.216 | 15.374 | 0.159 | 15.287 |
| fast | 3 | 14.918 | 16.081 | 1.163 | 14.952 |

## Forward warm observations

| Host | Category | Count | First visible min / max / range / median (ms) | Settled median (ms) | Classification |
|---|---|---:|---|---:|---|
| pre-fullapp | Razor | 9 | 470.9 / 9482.9 / 9012.0 / 592.4 | 2388.4 | browser-reload: 9 |
| pre-fullapp | C# | 9 | 77.7 / 194.9 / 117.2 / 139.7 | 1984.1 | browser-reload: 9 |
| pre-fullapp | CSS | 9 | 3648.1 / 5170.8 / 1522.6 / 3780.2 | 5329.6 | hot-reload: 9 |
| post-fullapp | Razor | 9 | 413.6 / 3572.3 / 3158.6 / 926.8 | 2716.6 | browser-reload: 9 |
| post-fullapp | C# | 9 | 62.5 / 164.7 / 102.2 / 103.0 | 1828.7 | browser-reload: 9 |
| post-fullapp | CSS | 9 | 3530.3 / 4948.6 / 1418.3 / 3622.1 | 5178.8 | hot-reload: 9 |
| parity | Razor | 9 | 388.9 / 2185.2 / 1796.2 / 454.4 | 2022.6 | browser-reload: 9 |
| parity | C# | 9 | 66.3 / 190.0 / 123.7 / 87.1 | 1651.3 | browser-reload: 9 |
| parity | CSS | 9 | 392.9 / 745.2 / 352.3 / 402.7 | 1969.7 | hot-reload: 9 |
| fast | Razor | 9 | 399.2 / 2141.0 / 1741.8 / 498.7 | 2064.4 | browser-reload: 9 |
| fast | C# | 9 | 79.6 / 190.4 / 110.8 / 148.5 | 1699.2 | browser-reload: 9 |
| fast | CSS | 9 | 383.4 / 701.0 / 317.5 / 409.0 | 1953.9 | hot-reload: 9 |

## Reverse warm observations

| Host | Category | Count | First visible min / max / range / median (ms) | Settled median (ms) | Classification |
|---|---|---:|---|---:|---|
| pre-fullapp | Razor | 9 | 485.3 / 2204.9 / 1719.7 / 596.0 | 2358.8 | browser-reload: 9 |
| pre-fullapp | C# | 9 | 85.3 / 162.1 / 76.8 / 144.8 | 1829.2 | browser-reload: 9 |
| pre-fullapp | CSS | 9 | 3624.6 / 5818.5 / 2193.9 / 3771.3 | 5321.4 | hot-reload: 9 |
| post-fullapp | Razor | 9 | 393.0 / 2358.4 / 1965.4 / 598.0 | 2413.2 | browser-reload: 9 |
| post-fullapp | C# | 9 | 91.1 / 159.7 / 68.6 / 136.8 | 1835.5 | browser-reload: 9 |
| post-fullapp | CSS | 9 | 3491.2 / 11084.6 / 7593.4 / 3649.3 | 5202.4 | hot-reload: 9 |
| parity | Razor | 9 | 392.4 / 1529.5 / 1137.1 / 467.8 | 2034.4 | browser-reload: 9 |
| parity | C# | 9 | 82.8 / 151.9 / 69.1 / 101.3 | 1651.8 | browser-reload: 9 |
| parity | CSS | 9 | 405.3 / 490.8 / 85.5 / 450.3 | 1998.2 | hot-reload: 9 |
| fast | Razor | 9 | 392.9 / 1530.1 / 1137.2 / 511.6 | 2086.8 | browser-reload: 9 |
| fast | C# | 9 | 78.9 / 149.5 / 70.6 / 140.2 | 1689.6 | browser-reload: 9 |
| fast | CSS | 9 | 391.2 / 526.7 / 135.5 / 466.7 | 2016.0 | hot-reload: 9 |

Each category contains three distinct frozen edits, each repeated three times. Reverse observations are restoration measurements, not extra forward repetitions. Per-edit min/max/range/median, settled ranges, cold raw rows and exact restored hashes are in measurement-comparison.json; every warm row is in measurement-samples.csv.

The classifier groups SDK browser refresh/enhanced navigation under browser-reload; inspect document identity and browser events before claiming a full document reload. CSS hot-reload is the observed static-asset update, not proof of a managed code delta. Settled observations include the fixed 1.5-second minimum and quiet-window protocol, so they are deliberately conservative confirmation time.

Historical protocol-v1 cold attempts and the interrupted pre-move warm run remain excluded with their original evidence. The first complete post-move series is excluded because sandbox responsive utilities were missing; responsive-utility-adjudication.md records the direct RED and correction. These accepted rows come from the complete repeat with the corrected assets. No performance conclusion is inferred automatically from these numbers.
