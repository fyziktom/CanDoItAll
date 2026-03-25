# Manual Vs MCP Build Comparison

## Final-revision comparison

The comparison below uses the finished projects-page revision.

### Backend-managed build

- Operation: `op_3e8d6db03075402b9ead3570cfc9498f`
- Target: `src\\CanDoItAll.Web\\CanDoItAll.Web.csproj`
- Duration: `62607 ms`
- Result: success
- Raw log volume: `44` entries
- Agent-optimized surfaced volume: `6` entries
- Suppressed volume: `38` entries
- Suppression notes:
  - `2` warning lines (`EF1002 x2`)
  - `17` restore/build progress lines
  - `16` artifact output lines
  - `3` blank lines

### Standard manual build

- Script: `tools/run-manual-build.ps1`
- Duration: `28867 ms`
- Log file: `artifacts/manual-build-web.log`
- Log size: `4756` bytes
- Log lines: `22`
- Result: success with `0` warnings and `0` errors

## Interpretation

- The unmanaged manual build was faster on this warm local run.
- The backend-managed build added overhead from isolation, artifact routing, and app preemption/resume.
- The backend-managed build still saved reading effort by collapsing `44` raw entries into `6` high-signal lines.
- The MCP value here was not raw build speed. It was cleaner log presentation plus runtime/session management.

## Extra note from an earlier build

An earlier backend-managed build on an intermediate revision took `86743 ms` and suppressed `66` of `72` raw entries, mostly noisy Razor warnings and restore/build chatter. That intermediate run reinforces the same pattern: the cleaner MCP log surface materially reduces reading load when the build is noisy.
