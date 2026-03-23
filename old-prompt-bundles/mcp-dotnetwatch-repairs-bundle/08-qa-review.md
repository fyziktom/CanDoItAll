# QA Review

## Review outcome

Accepted, with the following implementation-level additions now folded into the bundle:

1. The wrapper must be compatible with the actual PowerShell runtime Codex launches
- A wrapper that only works in newer .NET APIs is still a production failure.

2. Long waits must not be cut off by hidden transport defaults
- If `app_wait` or `operation_wait` can be shorter than the MCP timeout because of `HttpClient.Timeout`, the contract is broken.

3. Startup speed matters to reliability for this repo
- A large Blazor solution that always starts from a cold session-id artifacts folder will cause avoidable wait failures and wasted agent time.

## Why this bundle is sufficient

- It addresses the actual observed failure chain:
  - broken MCP transport
  - healthy detached backend
  - stale shadow proxy path
  - wrapper runtime incompatibility
  - hidden backend proxy timeout
  - unnecessarily cold app-start artifacts
- It improves both prevention and diagnosis.
- It aligns the operational path used by Codex with the path validated by tests.

## Final QA gate

The bundle is approved only if the implementation proves all of the following:
- Codex config launches the wrapper
- the wrapper can refresh shadow artifacts automatically
- wrapper startup can answer `workspace_info`
- startup failures write persistent diagnostics
- backend proxy waits honor the caller timeout instead of a hidden 100-second cap
- focused MCP tests pass after the changes
- managed app lifecycle remains usable after wrapper launch
- no new stdout protocol noise is introduced

## QA conclusion

Approved for implementation and validation. The only remaining non-code caveat is that a live Codex session may need the MCP host layer to reload before it starts using the repaired wrapper config. That does not invalidate the repo-side repair, but it must be called out in final validation evidence if the in-session tool transport still points at stale process state.
