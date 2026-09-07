# Overview direct-watch measurements

The tables use the corrected pre-extraction owner and final extracted source. All outliers are retained. First-visible and settled observations are separate; settled timing includes the frozen minimum/quiet/actual-chart stability requirements. Cold means a new process using existing restore/filesystem caches, not a cache-cleared machine.

## Process-cold startup

| Host | Runs | Minimum ms | Maximum ms | Range ms | Median ms |
|---|---:|---:|---:|---:|---:|
| pre-fullapp | 3 | 67465.970 | 87597.581 | 20131.611 | 69924.550 |
| post-fullapp | 3 | 67142.522 | 118188.710 | 51046.189 | 68057.648 |
| parity | 3 | 19421.396 | 24268.895 | 4847.499 | 19451.830 |
| fast | 3 | 19192.732 | 23761.468 | 4568.736 | 19272.340 |

## Warm forward observations

Each category contains three distinct edits, each repeated three times. Exact edit-level distributions are in measurement-summary.json.

| Host | Edit family | N / failures | First-visible min / max / range / median ms | Settled min / max / range / median ms | Classification |
|---|---|---:|---|---|---|
| pre-fullapp | Razor | 9 / 0 | 331.676 / 10666.372 / 10334.697 / 410.491 | 2332.813 / 12866.184 / 10533.370 / 2843.589 | {'browser-reload': 9} |
| pre-fullapp | C# | 9 / 0 | 270.419 / 463.858 / 193.438 / 294.251 | 2367.491 / 2780.521 / 413.030 / 2543.515 | {'browser-reload': 9} |
| pre-fullapp | CSS | 9 / 0 | 4656.980 / 7242.558 / 2585.578 / 5237.608 | 6606.735 / 9192.273 / 2585.539 / 7186.618 | {'hot-reload': 9} |
| post-fullapp | Razor | 9 / 0 | 269.247 / 4007.138 / 3737.891 / 579.167 | 2363.315 / 6373.483 / 4010.168 / 2817.748 | {'browser-reload': 9} |
| post-fullapp | C# | 9 / 0 | 99.400 / 223.989 / 124.588 / 152.548 | 2187.090 / 2510.765 / 323.675 / 2311.524 | {'browser-reload': 9} |
| post-fullapp | CSS | 9 / 0 | 4432.306 / 5895.394 / 1463.089 / 5018.232 | 6382.845 / 7844.998 / 1462.153 / 6969.206 | {'hot-reload': 9} |
| parity | Razor | 9 / 0 | 198.999 / 2036.078 / 1837.079 / 270.016 | 2166.120 / 4008.792 / 1842.672 / 2235.309 | {'browser-reload': 9} |
| parity | C# | 9 / 0 | 79.665 / 264.340 / 184.675 / 128.288 | 2033.363 / 2220.090 / 186.727 / 2093.447 | {'browser-reload': 9} |
| parity | CSS | 9 / 0 | 560.706 / 1073.722 / 513.016 / 712.008 | 2511.433 / 3006.236 / 494.802 / 2643.741 | {'hot-reload': 9} |
| fast | Razor | 9 / 0 | 265.834 / 1849.120 / 1583.286 / 278.348 | 2215.753 / 3827.762 / 1612.009 / 2240.433 | {'browser-reload': 9} |
| fast | C# | 9 / 0 | 82.015 / 223.295 / 141.280 / 144.702 | 2049.123 / 2173.081 / 123.958 / 2096.741 | {'browser-reload': 9} |
| fast | CSS | 9 / 0 | 524.620 / 1090.859 / 566.239 / 576.205 | 2443.728 / 3025.260 / 581.532 / 2520.225 | {'hot-reload': 9} |

## Warm reverse observations

Each category contains three distinct edits, each repeated three times. Exact edit-level distributions are in measurement-summary.json.

| Host | Edit family | N / failures | First-visible min / max / range / median ms | Settled min / max / range / median ms | Classification |
|---|---|---:|---|---|---|
| pre-fullapp | Razor | 9 / 0 | 343.820 / 2419.644 / 2075.823 / 404.379 | 2544.316 / 4516.434 / 1972.118 / 2660.555 | {'browser-reload': 9} |
| pre-fullapp | C# | 9 / 0 | 214.593 / 407.975 / 193.382 / 291.412 | 2454.563 / 2925.164 / 470.600 / 2724.338 | {'browser-reload': 9} |
| pre-fullapp | CSS | 9 / 0 | 4910.783 / 17813.136 / 12902.354 / 5206.308 | 6860.468 / 19763.121 / 12902.653 / 7155.507 | {'hot-reload': 9} |
| post-fullapp | Razor | 9 / 0 | 267.698 / 2473.679 / 2205.981 / 492.724 | 2351.312 / 4775.074 / 2423.762 / 2792.205 | {'browser-reload': 9} |
| post-fullapp | C# | 9 / 0 | 87.683 / 241.155 / 153.471 / 152.406 | 2127.414 / 2589.360 / 461.946 / 2386.230 | {'browser-reload': 9} |
| post-fullapp | CSS | 9 / 0 | 4483.245 / 7005.420 / 2522.175 / 4682.755 | 6433.405 / 8971.652 / 2538.246 / 6633.319 | {'hot-reload': 9} |
| parity | Razor | 9 / 0 | 259.700 / 1148.697 / 888.997 / 286.002 | 2209.146 / 3115.013 / 905.867 / 2251.822 | {'browser-reload': 9} |
| parity | C# | 9 / 0 | 83.106 / 159.923 / 76.817 / 144.582 | 2037.425 / 2134.928 / 97.503 / 2095.414 | {'browser-reload': 9} |
| parity | CSS | 9 / 0 | 573.183 / 888.518 / 315.334 / 699.108 | 2488.544 / 2821.026 / 332.482 / 2648.365 | {'hot-reload': 9} |
| fast | Razor | 9 / 0 | 197.110 / 968.919 / 771.809 / 278.294 | 2164.112 / 2920.368 / 756.256 / 2240.651 | {'browser-reload': 9} |
| fast | C# | 9 / 0 | 77.583 / 197.341 / 119.757 / 145.081 | 2043.677 / 2163.496 / 119.820 / 2122.917 | {'browser-reload': 9} |
| fast | CSS | 9 / 0 | 553.683 / 768.530 / 214.847 / 626.637 | 2487.756 / 2718.366 / 230.609 / 2565.268 | {'hot-reload': 9} |

## Interpretation limits

These are nine specific, supported edits on one Windows machine, SDK 10.0.303 and live sibling source mode. Warm C# or Razor results do not predict unsupported/rude edits or arbitrary application changes. The full app uses the real isolated canonical stores; the sandbox uses their frozen immutable rendering export and has no production runtime. Parity uses production Tailwind input/output; Fast uses its explicit bounded asset entry. Both use real shared controls, CSS isolation, fonts, avatars and Charts assets. The dashboard frame and chart geometry are recorded, with retained expected asset-mode typography differences.

All owned watch/Tailwind processes have matching exit receipts. Source/asset restoration is separately verified. SDK-driven reloads and any explicit fixture context restoration are recorded per sample. No manual refresh, managed watcher, parallel test/build workload or removed outlier is used. The original baseline before the responsive correction and failed browser/calibration attempts remain outside these corrected-source statistics with their original receipts.

## Raw totals

12 cold starts; 216 warm forward/reverse observations; 0 observation failures. Classification: {'browser-reload': 144, 'hot-reload': 72}.
