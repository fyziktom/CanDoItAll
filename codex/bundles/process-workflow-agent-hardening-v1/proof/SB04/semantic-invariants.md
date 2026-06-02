# SB04 Semantic Invariants

1. Every known browser tool is governed from the canonical tool catalog. Browser navigation, input, screenshot, snapshot, console, and evaluation tools all require `CaptureRuntimeProof`; they cannot be treated as ordinary read-only tools.

2. Tool policy decisions are operation-backed. A process step without `CaptureRuntimeProof` must deny browser tools with an actionable required-operation diagnostic, and a step with `CaptureRuntimeProof` must allow those same browser tools unless another policy rule blocks them.

3. Runtime host receipts are identity records. `workspace_dotnet_run` startup receipts include host URL, listen/probe URL, database profile id, database profile fingerprint, environment, process tree ids, lifetime scope, cleanup state, and an actionable stop command.

4. Browser proof is a structured record, not a chat claim. The proof record must carry process run id, step run id, execution run id, project id, captured timestamp, route, viewport, runtime host identity, browser tool outputs, durable evidence paths, and cleanup receipt.

5. Browser proof is current-run bound. Evidence paths must be under the current process run browser/runtime artifact roots or match successful browser tool output paths from the current execution.

6. Stale proof is invalid. `CapturedAtUtc` earlier than the execution start is rejected even when the screenshots or console paths look plausible.

7. Host and database drift are invalid. When the validation context provides an expected runtime host URL or database profile, the proof record must match it exactly after normalized URI comparison.

8. Browser proof requires multiple evidence types. A valid proof includes screenshot evidence, browser state evidence through `browser_snapshot` or `browser_evaluate`, and console evidence through `browser_console_messages`.

9. Interactive browser proof requires representative interaction. When the process expectation requires interaction, proof without an interaction-producing browser tool output is rejected.

10. Kept-alive runtime hosts require cleanup receipts. A kept-alive host without cleanup attempted/process-id evidence is invalid because it can leave build-locking app processes behind.

11. Validator failures are explicit. Missing host, viewport, tool output, cleanup receipt, wrong ids, copied artifact paths, stale timestamps, wrong host, and wrong database profile all produce validation diagnostics instead of silently passing.

