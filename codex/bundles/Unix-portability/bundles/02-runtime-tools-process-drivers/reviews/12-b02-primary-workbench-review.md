# B02 primary Workbench gate review

## Decision

`GO confirmed by independent re-review.`

B02 satisfies NODE-001 through NODE-008 on the frozen source/evidence set. The independent reviewer accepted all remediation in review 13; B03 becomes eligible after the bundle integrity records are regenerated.

## Architecture review

- The Workbench compiler remains owner-specific and pure; it does not migrate runtime-node semantics into AgentFramework Core or a broad platform service.
- Ordinary direct execution consumes the B01 executable locator and a host-lifetime Workbench session registry. The registry owns one explicit DI child scope/canonical B01 process host, retains exact sessions without detach, and provides exact node stop, new-scope recovery, natural-completion cleanup, and host-shutdown cleanup. No duplicate ordinary process runner was introduced.
- Terminal and elevation are narrow presentation adapters. Their two `Process.Start` sites are explicit boundary exceptions, not fallbacks for ordinary runtime plans.
- The compatibility launcher is materially thin: metadata validation, authorized path adaptation, compilation, capability probing, and delegation. It no longer owns raw process construction.
- Project dependency direction is unchanged because no project/build/package reference file changed.

## Correctness and security review

- Every path reaches the Core workspace/external-target authority before compilation, including all agent-selected targets. Foreign-host syntax, external alias authorization, and reparse traversal fail closed.
- Dynamic, chained, redirected, encoded, and ambiguous legacy shell metadata remains unresolved. Static cmd/PowerShell wrappers migrate only allowlisted runtime executables and harmless pre-command host flags; semantic host options and duplicate `-Command` shapes require operator repair.
- PowerShell/POSIX scripts are explicit plan kinds with explicit executable dependencies; ordinary .NET, Docker, Python, Node, and Tailwind commands are not shell-routed.
- Script `RequiresApproval` is enforced inside the launcher. Compatibility overloads do not bypass it, and the Workbench supplies `OperatorConfirmed` only after the user accepts a per-launch confirmation dialog.
- Windows `runas` is independently capability-gated. Linux/macOS cannot fall through to `sudo`, `pkexec`, AppleScript, or a password prompt.
- Capability diagnostics expose state and remediation without physical paths, environment values, or exception details. Agent-execution projections return empty resolved command/working-directory fields even when an opaque external-target alias was authorized.

## Evidence integrity

- Windows/Linux named unit slices: 98/98 on each host, including already-cancelled and per-session-interrupted host-shutdown cleanup.
- Affected component adapter: 24/24.
- Default/headless Playwright: 1/1 each.
- Workbench, Composition, and Web builds: 0 warnings / 0 errors.
- Governed manifest: 37/37 source hashes and 13/13 artifact hashes cover the lifecycle registry, approval dialog, strict migration, non-disclosure, focused tests/builds/UI, and Linux provenance; primary recomputation found 0 mismatch.
- Anti-stub scan: 0 markers. Launcher process scan: 0 matches. Project graph changes: 0.
- Schema-3 text scan: 10 scanned, 0 oversized, 0 unreadable, 0 findings; four PNGs separately inspected and hash-bound.

## Validation proportionality

The proof is intentionally targeted. The same seven-class unit slice ran on Windows and Linux; browser validation ran only the single new scenario twice to cover both default and headless presentation. No full solution suite was rerun. This matches the operator-requested fast ladder and preserves the later aggregate gate.

## Residuals

- Actual macOS remains deferred and is not claimed.
- Configured Linux/macOS desktop terminal launch remains deployment-profile evidence, not a headless-core dependency.
- B02 owns only Workbench runtime-node sessions for the current host lifetime. B03 owns the distinct Manager-launched process registry, durable recovery, restart, and termination UX.
- Hosted and final R4 evidence remain deferred.

## Handoff

Independent B02 Workbench gate re-review is GO. Regenerate index/checksums, run the portable validator, and make B03 the only eligible subbundle.
