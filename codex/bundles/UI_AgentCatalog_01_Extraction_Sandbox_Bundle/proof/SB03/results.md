# Catalog measurement results

Values are seconds: minimum / maximum / range / **median**. These are observed end-to-end timings, including tool transport; they are not isolated compiler benchmarks.

| Edit | Pre-extraction full app | Post-extraction full app | Sandbox |
|---|---|---|---|
| razor-heading | 10.336 / 13.644 / 3.308 / **11.060** | 12.771 / 15.115 / 2.345 / **13.021** | 13.588 / 16.993 / 3.405 / **14.644** |
| razor-empty | 9.740 / 11.064 / 1.325 / **10.361** | 11.333 / 12.384 / 1.051 / **11.621** | 11.303 / 15.878 / 4.575 / **12.417** |
| razor-action | 9.768 / 11.973 / 2.205 / **10.853** | 11.782 / 12.409 / 0.626 / **12.045** | 11.059 / 18.471 / 7.412 / **11.408** |
| csharp-summary | 8.946 / 10.647 / 1.701 / **9.581** | 11.508 / 12.143 / 0.635 / **12.052** | 10.992 / 17.324 / 6.332 / **12.371** |
| csharp-metadata | 9.337 / 11.208 / 1.871 / **11.156** | 11.248 / 13.226 / 1.977 / **12.505** | 10.754 / 32.643 / 21.889 / **12.268** |
| csharp-team-title | 10.024 / 32.227 / 22.203 / **10.298** | 11.365 / 12.891 / 1.526 / **11.485** | 10.950 / 12.455 / 1.505 / **11.587** |
| css-toolbar | 14.438 / 39.059 / 24.622 / **14.879** | 16.346 / 18.328 / 1.982 / **16.381** | 11.382 / 13.340 / 1.959 / **11.409** |
| css-spacing | 13.527 / 18.207 / 4.680 / **14.561** | 15.268 / 31.288 / 16.020 / **15.343** | 11.348 / 12.200 / 0.852 / **11.909** |
| css-tailwind | 8.157 / 10.395 / 2.238 / **8.931** | 10.735 / 12.783 / 2.049 / **12.601** | 10.881 / 11.358 / 0.477 / **11.100** |

Process-cold results use populated build/restore caches.

| Host | Min / max / range / median (s) |
|---|---|
| pre | 116.724 / 135.780 / 19.056 / **118.234** |
| post | 121.835 / 131.886 / 10.051 / **122.104** |
| sandbox | 44.259 / 70.368 / 26.109 / **45.046** |

Primary samples: 81 warm; 9 cold.
- Retained outside comparison: pre-css-toolbar-1 - Retained incompatible pre-CSS manager-parser protocol.
- Retained outside comparison: sandbox-calibration-razor-heading-1 - Calibration, outside primary hosts.
- Retained outside comparison: sandbox-calibration-css-tailwind-v2-1 - Calibration, outside primary hosts.
