# Failure Scenarios

## Startup and transport risks

1. Shadow host is stale
- Cause: source project changed but `.artifacts/mcp-server-shadow` was not rebuilt
- Symptom: Codex MCP transport closes or behaves inconsistently while repo tests or direct app runs still work
- Protection: wrapper script that incrementally rebuilds shadow artifacts before launch

2. Shadow host is missing or partially deleted
- Cause: artifact cleanup, failed previous build, interrupted copy
- Symptom: stdio host never starts
- Protection: wrapper rebuilds missing shadow output before launch and fails with explicit stderr if build fails

3. Invalid settings file
- Cause: moved repo, bad path, malformed JSON, bad solution path
- Symptom: stdio host exits during startup
- Protection: keep fast validation, add bootstrap diagnostics file, preserve actionable stderr

4. Existing backend registration points to a dead process
- Cause: crash, machine reboot, forced kill
- Symptom: stdio startup waits or reconnect logic works unpredictably
- Protection: registration self-heal plus actionable timeout diagnostics

5. Existing backend registration points to a live but incompatible backend
- Cause: binary drift, settings change, repo mismatch
- Symptom: proxy can see a backend but must not reuse it
- Protection: identity check already exists; keep it and improve timeout messaging

6. Backend is running but HTTP manager endpoint is unreachable
- Cause: port bind issue, firewall, broken backend startup
- Symptom: proxy dies or times out without enough evidence
- Protection: capture failed ping diagnostics and write them to bootstrap log

7. Wrapper build fails because the server is validating itself
- Cause: server repair work needs a manual build path
- Symptom: agent gets blocked between tool repair and tool usage
- Protection: wrapper makes shadow build the default, not a comment-only manual step

## App and Playwright cooperation risks

8. MCP app session is restarting while Playwright reads stale UI
- Cause: hot reload or restart race
- Symptom: screenshots against old DOM or half-restarted runtime
- Protection: keep using `WatchSettled` and managed wait semantics once transport is healthy

9. Playwright is ready but MCP backend catalog still shows stale instances
- Cause: previous backend records survived and are not pruned until queried
- Symptom: noisy diagnostics and ambiguous backend inventory
- Protection: keep stale catalog cleanup and validate it in aggregate status flows

10. App is healthy but MCP stdio host is gone
- Cause: stdio crash, Codex-side process recycle, stale shadow launch
- Symptom: agent loses tools while app continues running
- Protection: wrapper-based restart path and bootstrap log so this becomes diagnosable and repairable
