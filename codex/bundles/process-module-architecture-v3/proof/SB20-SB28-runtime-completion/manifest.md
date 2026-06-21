# SB20-SB28 Runtime Completion Proof Manifest

## Scope

This proof closes the runtime/dispatch/project-start gap discovered after the UI parity repairs. The implemented repair keeps process core/runtime/dispatch generic and adds a governed `workspace_dotnet_stop` validation tool so process validation steps can stop kept-alive `workspace_dotnet_run` process trees by receipt instead of relying on raw PowerShell cleanup.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| `workspace_dotnet_stop` tool contract | `ToolContractCatalog`, `ToolCapabilityRegistry`, `AgentToolInvocationPolicy` | MAF runtime tool registration and process operation authorization | Static tool policy metadata classifies stop as validation/runtime-launch cleanup requiring `LaunchRuntime` or `CaptureRuntimeProof` | `bundle://proof/SB20-SB28-runtime-completion/transcripts/test-unit-workspace-dotnet-stop-focused.txt` |
| `IWorkspaceCommandExecutionService.DotnetStop` | AgentFramework workspace command service and plan builder | MAF workspace plugin and agent tool invocation | Builds a bounded PowerShell stop plan from a `startup.json` receipt, writes `cleanup.json`, updates startup cleanup state, and denies non-receipt paths | `bundle://proof/SB20-SB28-runtime-completion/transcripts/test-unit-workspace-dotnet-stop-focused.txt` |
| Agent seed capability `workspace-dotnet-stop` | Sandbox seed builder/normalizer and agent template skill lists | Delivery, QA, Blazor, programming, and screenshot agents | New and stale catalogs learn receipt-based cleanup and stop using `stopCommand` instructions | `bundle://proof/SB20-SB28-runtime-completion/transcripts/test-integration-seed-workspace-dotnet-stop.txt` |
| Template validation cleanup contract | Blazor process templates and seed inline skills | Blazor delivery validation/revalidation steps | Validation steps must call `workspace_dotnet_stop` with the startup receipt and cite `cleanup.json` before branch completion | `bundle://proof/SB20-SB28-runtime-completion/e2e/revalidate-blazor-repair-summary.md` |
| TetrisGame dev-DB process run | Project-structure process start API on `http://localhost:5032` | Generic Process launch/runtime/dispatch/application layers | Run `06f0c5bd-f425-44b9-9985-0a11e0a72a6f` completed through initial QA, repair, revalidation, and result recording | `bundle://proof/SB20-SB28-runtime-completion/e2e/tetris-launch-response-after-dotnet-stop.json`, `bundle://proof/SB20-SB28-runtime-completion/transcripts/tetris-runtime-status-after-dotnet-stop.txt` |
| Runtime cleanup receipts | `workspace_dotnet_stop` generated `cleanup.json` | Process QA evidence and runtime validation steps | Initial validation and revalidation both stopped all recorded process ids and left `stillRunningProcessIds` empty | `bundle://proof/SB20-SB28-runtime-completion/e2e/validate-cleanup.json`, `bundle://proof/SB20-SB28-runtime-completion/e2e/revalidate-cleanup.json`, `bundle://proof/SB20-SB28-runtime-completion/transcripts/tetris-processes-after-cleanup.txt` |

## Validation Evidence

- Full solution build passed with 0 warnings and 0 errors: `bundle://proof/SB20-SB28-runtime-completion/transcripts/build-solution-after-runtime-stop.txt`.
- Focused unit tests passed 186/186: `bundle://proof/SB20-SB28-runtime-completion/transcripts/test-unit-workspace-dotnet-stop-focused.txt`.
- Focused seed integration tests passed 5/5: `bundle://proof/SB20-SB28-runtime-completion/transcripts/test-integration-seed-workspace-dotnet-stop.txt`.
- Real TetrisGame process run completed: `bundle://proof/SB20-SB28-runtime-completion/e2e/tetris-launch-response-after-dotnet-stop.json`.
- Runtime status query shows completed dispatch state and operation target scopes: `bundle://proof/SB20-SB28-runtime-completion/transcripts/tetris-runtime-status-after-dotnet-stop.txt`.
- Initial validation cleanup receipt: `bundle://proof/SB20-SB28-runtime-completion/e2e/validate-cleanup.json`.
- Final revalidation cleanup receipt: `bundle://proof/SB20-SB28-runtime-completion/e2e/revalidate-cleanup.json`.
- Final startup receipt no longer emits a raw `stopCommand` and records `stopTool = workspace_dotnet_stop`: `bundle://proof/SB20-SB28-runtime-completion/e2e/revalidate-startup.json`.
- CodeAnalytics MCP post-change snapshot: `bundle://proof/SB20-SB28-runtime-completion/codeanalytics-snapshot-summary.txt`.

## Source And Scan Evidence

- Changed-file hashes: `bundle://proof/SB20-SB28-runtime-completion/changed-file-hashes.txt`.
- Runtime stop domain-vocabulary scan: `bundle://proof/SB20-SB28-runtime-completion/scans/runtime-stop-domain-vocabulary-scan.txt`.
- Runtime stop anti-stub scan: `bundle://proof/SB20-SB28-runtime-completion/scans/runtime-stop-anti-stub-scan.txt`.

## Result

Passed. The new runtime cleanup path is generic, policy-governed, exposed through the agent runtime, seeded into delivery agents, and proven by a real project-structure-launched multistep Blazor/Tetris process. The process created a repair branch for a real QA finding, revalidated successfully, recorded results, and left no `TetrisGame` process running.
