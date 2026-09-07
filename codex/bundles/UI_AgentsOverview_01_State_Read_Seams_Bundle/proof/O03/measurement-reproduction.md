# Reproducing the Overview observations

These are observations from the current Windows machine with .NET SDK 10.0.303 (global.json requests 10.0.302/latestPatch), Node 24.19.0, Tailwind 4.2.1 and Chromium 149.0.7827.55. Preserve the exact sibling revisions and the four-file O02 Components patch recorded in final/closure-repository-state.json and final/sibling-byte-verification.json. Live source mode is required. A different SDK, source graph or asset pipeline needs a new baseline.

## Evidence and protocol

- Corrected comparable results: [full distributions](final/measurements.md), [per-edit data](final/measurement-summary.json).
- Original pre-move series, retained but superseded: measurements/; original failure/calibration receipts remain in raw/ and browser-sandbox/.
- Corrected pre-owner series: corrected-pre-owner/planned-edits.json, replay-receipt.json, measurements/ and post-owner-backup/. It reconstructs exactly the original Module owner with only the proven xl-to-2xl responsive correction. The original UI project/import bytes and pre-owner dependency graph are checked. Eleven post-owner source paths and both asset outputs were restored afterward.
- Final comparison: final/planned-edits.json, measurement-runs.json, measurements/, measurement-restoration.json and measurement-final-source-check.json.
- Executed harness: tools/direct-watch.cjs, its exact SHA-256 is recorded and verified in each planned-edits.json. Use that machine-checked value.

The harness is a private proof tool, not a new production benchmarking API. Restore the retained tool files into a fresh owned scratch directory under .mcp-state before adapting paths. Gzip files are retained transcripts, not script inputs. Read the guards before running: existing receipts must not be overwritten, old/new source hashes must match, and concurrent source edits stop restoration. Do not remove those guards to force a rerun.

## Canonical fixture

The retained fixture/overview.json is an immutable rendering export from real registered workspace, provider registry, execution-run stores and ProviderUsageQueryService. The safe seed and export receipt is fixture/receipt.json. Its hash is 3621dcff873f79b8eb4f9f58a8f0697da7a6e35d3d04568b22a2e0822aed95c2. Six observations include two unknown and one unpriced record; the exporter verifies both real providers and consumers before recording the snapshot. The current baseline contains the same default-seeded catalog and Overview totals in every host.

The private fixture Program.cs/csproj are retained under tools. They use the repository PostgreSQL TestSupport fixture and create an isolated profile/root; generate a new local environment file on reproduction. Credentials and the original environment.json/password file are intentionally absent from retained proof. Never copy the developer's ordinary profile into this fixture or retain credentials in logs. The exporter was built in the pre-owner state and imports the old Module Overview namespace: reproduce in that frozen state or change only its private import to the final UI Overview namespace before rebuilding. That private import adaptation is not a production change or a claim that the old fixture program compiles unchanged after extraction.

No connector, diagnostic, model invocation, chat or process workload is launched by the timing fixture. The full app reads canonical isolated stores. The sandbox reads their frozen rendering export and has no database or runtime service registration.

## Sequential execution

Recreate the same source state first. Before any source movement, run the pre-owner Web build and actual evaluated graph audit; freeze the plan, source and generated Web/Fast CSS. The original baseline was taken before the first move. The later responsive correction required a full corrected pre-owner replay, not relabeling the earlier samples.

The retained run-pre.py and final/run-post.py enumerate the exact executed commands. Each invokes direct-watch.cjs with --plan, --host, --phase, --repetition, --environment and --output. The harness launches direct dotnet watch --verbose --non-interactive --no-launch-profile plus the native Tailwind CLI in watch mode. The project/mode arguments and environment ownership are recorded in every ledger. Web uses port 5293; Parity 5393; Fast 5394. Use an available explicitly owned equivalent port only in a newly labeled run.

For each host, run three process-cold iterations, then one warm driver containing three repetitions of each of nine distinct edits and their reverse. Do not run builds, tests or another measured host concurrently. The four hosts are corrected pre-fullapp, post-fullapp, parity and fast, in that recorded order. Cold means a fresh process with existing restore/filesystem caches; it does not mean a cold machine or empty NuGet cache.

The nine probes change three visible Razor labels, three C# metric labels and three isolated-CSS size/gap values. Every probe flushes bytes to disk before starting the monotonic timer. First-visible requires the unique DOM/computed-style predicate. Settled requires at least 1500 ms, a quiet window, real ApexCharts SVG plot series with stable geometry and two animation frames. The dashboard uses the same 1600 x 1000 matched frame; asset-mode typography differences are retained. Runtime ownership, PID/start, watch iteration/update generation, document UUID/timeOrigin and browser navigation are captured independently. Legend placeholder SVGs are not accepted as plot proof.

After every sample restore the exact source. After each series verify both generated assets and all frozen source hashes. Stop only the recorded owned watch/Tailwind process trees. The completed comparable series has 12 cold starts, 108 forward and 108 reverse observations, zero observation failures, 144 browser reloads and 72 CSS hot reloads. All 32 owned process starts have matching exits. All warm fixture-context restoration counts are zero. No manual refresh or managed-watch substitute was used.

## Interpretation

Median CSS first-visible time falls from 5237.608 ms before extraction to 712.008 ms in Parity and 576.205 ms in Fast; post-extraction full app remains 5018.232 ms. Process-cold medians are 69924.550, 68057.648, 19451.830 and 19272.340 ms respectively. These observations support the small-host CSS iteration and startup benefit for this fixture.

The full-app Razor median increases from 410.491 to 579.167 ms, and Parity C# is faster than Fast in this sample. Retain all outliers, including the corrected pre-owner Razor maximum 10666.372 ms and reverse CSS maximum 17813.136 ms. First-visible and settled measures answer different questions. This does not establish universal Fast superiority, arbitrary edit support, rude-edit performance, production bookmarkability or a performance guarantee on another machine.
