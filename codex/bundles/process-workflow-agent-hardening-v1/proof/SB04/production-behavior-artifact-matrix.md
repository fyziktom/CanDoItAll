# SB04 Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Tool policy browser decision | `AgentToolInvocationPolicy` using `ToolContractCatalog.BrowserToolNames` | Agent runtime/tool invocation gate and process dispatch metadata | Built per invocation from process allowed operations; browser tools require `CaptureRuntimeProof` | `failing-first-browser-tool-policy-mutation.txt`; `browser-tool-policy-restored-tests.txt` |
| Runtime host identity receipt | `WorkspaceCommandPlanBuilder.BuildDotnetHttpRunPowerShellScript` | Browser proof records, dispatch review, process evidence consumers | Written to `startup.json` for `workspace_dotnet_run`; includes host URL, DB profile fields, environment, process ids, lifetime scope, cleanup, and stop command | `runtime-host-command-tests-after-cleanup-assertions.txt` |
| Process browser proof record | Process/browser evidence writeback under the current process run | `ProcessBrowserProofValidator` and dispatch completion validation | Parsed from current-run browser/runtime proof JSON and validated against process run, step run, execution run, project, host, route, viewport, output, and cleanup context | `browser-proof-validator-tests.txt` |
| Browser tool output paths | Browser tools and session file-write records | `ProcessBrowserProofValidator` through dispatch validation context | Tool output paths must match successful current execution browser tool outputs or current process-run browser/runtime artifact roots | `Validate_rejects_copied_browser_output_not_produced_by_current_execution` in `browser-proof-validator-tests.txt` |
| Cleanup receipt | `workspace_dotnet_run` script and proof record writer | Browser proof validator and downstream release/build validation | Non-kept-alive runs stop the process tree and record cleanup; kept-alive runs publish stop command and require cleanup receipt before proof is accepted | `Validate_rejects_missing_cleanup_receipt_for_kept_alive_runtime_host`; `runtime-host-command-tests-after-cleanup-assertions.txt` |

## Dependency Smoke Proof

- SB07 can display browser proof, runtime host identity, cleanup, and rejection diagnostics without inventing UI-only state.
- SB08 can produce real app proof records with route, viewport, Playwright actions, screenshots, console logs, host URL, DB profile, and cleanup receipt.
- SB09 can red-team stale screenshots, copied artifacts, wrong host, wrong database profile, and missing cleanup through deterministic validator failures.

