# SB03 measurement and consumer closure

Status: closed for bounded catalog acceptance. The whole-repository documentation gate remains failed on reviewed historical artifacts.

The frozen matrix contains 81 primary warm trials and nine process-cold starts across pre-extraction full app, post-extraction full app and sandbox. Every primary warm trial succeeded through hot reload, with visible undo and exact source restoration. No primary browser reload, process restart, failure or missing repetition occurred. The retained ledger also contains the incompatible pre-CSS parser trial and two sandbox calibration trials; these three are explicitly excluded from comparison.

Median seconds:

| Observation | Pre full app | Post full app | Sandbox |
|---|---:|---:|---:|
| Razor warm | 10.853 | 12.384 | 13.588 |
| CSharp warm | 10.298 | 12.052 | 12.268 |
| CSS warm | 14.438 | 15.343 | 11.382 |
| Process-cold startup | 118.234 | 122.104 | 45.046 |

The sandbox has a lower observed cold-start and CSS median in this run. The observed Razor and C# warm medians did not improve. Full-app extraction alone does not establish an end-to-end iteration improvement. The per-edit minimum, maximum, range and median are in [results.md](results.md); every sample and mechanism is in the CSV and lossless ledger archive.

The primary clock spans flushed source bytes to the browser assertion and two animation frames, including managed-tool dispatch, readiness polling and browser-tool transport. It is an observed managed-loop latency, not the earliest possible visible update or an isolated compiler benchmark. Hosts were measured sequentially on the same machine/SDK/source mode/asset pipeline; OS and thermal noise were not experimentally controlled. Three repetitions per edit establish this bounded observation, not statistical significance or a universal performance claim. Cold samples use populated restore/build/browser caches and include launch dispatch; they are process-cold, not clean builds.

SDK-reported apply medians (milliseconds), correlated to exactly one event in each of the 81 primary cursor windows:

| SDK update | Pre full app | Post full app | Sandbox |
|---|---:|---:|---:|
| Razor | 1935 | 1894 | 169 |
| CSharp | 1831 | 1699 | 38 |
| CSS | 5777 | 5874 | 544 |

These show reduced SDK update work in the sandbox. They are separate from browser-visible latency. A static-asset 0 ms event is SDK reporting granularity. Raw log envelopes, sdk-events.json and sdk-durations.csv preserve this appendix.

All six changed production projects have direct Release build evidence across Providers-02D/SB01/SB02. Root solution restore/build and the freshly built stable solution passed. The one named cross-assembly stable checkpoint executed 9,802 cases: 1,307 Components, 1,365 Integration, 22 AgentFramework.Memory, 196 Memory and 6,912 Unit; zero failures or skips. The 9,747 discovery rows expand through seven unchanged theory methods, reviewed before execution. Exact filters, original discovery, source review, run IDs, counters and lossless TRX are adjacent. Special Docker, live, long-running, quarantine and runtime-portability lanes are not claimed.

SB01 focused validation passed 48 Components and six Unit cases. Browser acceptance from SB01/SB02 exercises the real production host and independent sandbox, cards/tree/tooltips/assets, all typed intents and loading/empty/fallback states. Product bytes were unchanged afterward. Final source restoration verifies all 25 frozen files and original launch settings. All 28 provider production files still match the Providers-02D closure. The final architecture snapshot snap-20260906035002-22fb4e81 has no blocking errors or new boundary warning/error; the two existing module/type cycles and collector limitations are explicitly reviewed.

Both measurement app ports are closed. The original isolated fixture is retained in ignored task data for reproducibility; no operator database was used. The idle, identity-verified measurement coordinator was stopped during final cleanup. No routing, provider-history, additional editor extraction, visual redesign, sibling refactor, merge or history cleanup was performed.

Final static/artifact status is recorded in final-static-gates.json. Existing whole-branch documentation debt remains separate and prevents an unconditional merge-ready claim.
