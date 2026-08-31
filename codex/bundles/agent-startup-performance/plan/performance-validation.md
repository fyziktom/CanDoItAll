# Performance Validation Protocol

Planned execution only. Prior measurements are context, not the paired baseline. The thresholds below are acceptance targets, not promised speedups.

## Freeze And Sampling

Before production edits, record each host's own build/source/image, agent ID, provider/model/thinking configuration, capability set, project scope, storage topology, background activity and run-history size. Compare each host against itself; native and relay/shared routes need not be identical.

Use one first-observed-after-start sample separately when available; do not restart a busy app to manufacture a cold sample. If absent, record that fact and collect a controlled post-replacement sample separately from warm proof. Collect **five warm fresh-session starts per host before and after**, serially, using the same short prompt: “Explain why decimal is preferable to binary floating point for currency calculations, in two sentences. Do not invoke tools.” All sends originate from the actual UI. Also measure continuation separately.

Do not run builds, tests, simultaneous benchmark conversations, catalog edits or other validation during sampling. Do not disable background maintenance/history to improve the result. Record every sample, failure and unavoidable contention; no cherry-picking or deleting outliers.

## Markers

| Marker | Evidence |
|---|---|
| T_submit | Browser monotonic timestamp immediately before actual Send click |
| T_created | run.createdAtUtc; initial durable run creation may still be pending |
| T_runtime_run | First runtime Run log; timestamp precedes its own persistence |
| T_dispatch | Actual outbound HTTP-send boundary correlated with run/operation |
| T_first_content | First rendered assistant content, excluding progress text |
| T_terminal | Persisted terminal outcome and UI agreement |

Use transport diagnostics/traces with correlation IDs, safe status and timings only. Existing `provider.call` in `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/MafStreamingTurnExecutor.cs` wraps framework invocation and is **not** actual HTTP-send proof. For5214 distinguish client-to5210 relay send from publisher-to-upstream send where available, without changing publisher5210.

Capture timings for provider revision probing before run creation as well as lock wait, run loading, path probing, journal/payload write and recovery-validation spans. Prefer existing diagnostics or test-only measurement seams/external tracing; no new production telemetry subsystem. Any diagnostic harness applied to both baseline/candidate must be identical, bounded and reviewed for perturbation. If true dispatch or component attribution cannot be obtained, explicitly leave that measurement/gate open; never rename a UI stage or first token as dispatch.

Use monotonic durations inside each process. Record clock alignment for cross-process UTC comparisons; without alignment keep browser totals and server intervals separate. Exclude prompts, file contents, credentials, headers and secret-bearing URLs from performance traces.

## Gates

- Both hosts: warm median T_created→T_dispatch improves **at least 15% and 0.5 seconds**.
- Submit→dispatch must show no repeatable regression; this includes SB02's pre-creation work. Separately prove fewer revision-query/materialization operations.
- Report min/median/max and all five samples. Do not claim a statistically meaningful p95 from five samples.
- If candidate maximum regresses by more than max(10% of baseline maximum, 0.5 s), repeat one matched five-sample batch. Unresolved regression/noise keeps the gate open.
- First-after-start (when safely available) and continuation must show no repeatable regression; do not pool them into warm fresh-session samples.
- Browser submit→first content/terminal is reported, but upstream LLM latency cannot establish startup improvement.
- Deterministic tests show reduced redundant case probes/reloads/materialization at unchanged directory/history scale. Security checks, required writes, stage logs and final outputs remain equivalent.
- If measured costs do not justify an allowed optimization, reopen the unit; do not implement batching, weaken validation or claim success from microbenchmarks alone.

## Portable Proof

Store sanitized raw sample CSV/JSON, environment/build identity, measurement commands, clock notes, query/I/O counts, stage durations and the comparison calculation under `proof/SB03/performance/`. Link SB01/SB02 isolated component measurements from their manifests. Actual token/usage records corroborate real provider activity; status/counts alone do not prove function.
