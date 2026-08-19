# B01 primary Gate R1a review

## Scope

Primary architecture, runtime, and security review of EXEC-001 through EXEC-008 against the current B01 working tree and the recorded provisional source anchors.

## Findings

No blocking B01 source, dependency-direction, lifecycle, executable, environment, cancellation, or receipt finding remains.

- One low-level process implementation owns start, stdin, bounded output, timeout/cancellation classification, tree termination, and residual reporting.
- Managed long-running `dotnet run` now launches `dotnet` directly through a typed process session. The canonical host owns readiness, detach, disposal, and verified termination; no generated PowerShell `Start-Process` path remains.
- Durable stop/recovery checks PID, exact UTC launch timestamp, and the opaque kernel-reported executable fingerprint. The persisted identity round-trips through JSON without tolerance; a 250 ms mismatch is refused, closing rapid same-executable PID reuse. Actual Linux proof also covers directory-symlink executable aliases and short-lived-process capture races; a mismatched foreign identity is refused and its lease retained.
- Owner-specific Git and external-tool contracts adapt at the outward composition boundary; Foundation did not gain an outward process dependency.
- Windows/Linux behavior proves process-tree cleanup and native executable semantics. Deterministic macOS policy fixtures are present, while genuine macOS remains explicitly deferred.
- Environment inheritance is host-correct and excludes ambient credential variables. Explicit overlays remain visible in execution only; persisted receipts contain names and redacted projections.
- Windows path alias compatibility is explicitly typed and uses async cleanup through the same process host.
- No native process adapter was added because current actual-host tests do not prove it necessary.
- No project-reference file changed, the affected project builds are warning/error-free, and the source manifest remains 37/37 with no missing path.
- The focused 133/133 + 2/2 results pass on both Windows and Linux. The process and descriptor receipt regressions include next-element sensitive argv values. The governed manifest binds failing-first corrections, source/artifact hashes, source assertions, and anti-stub proof. The artifact scanner covers all retained B01 evidence and reports no finding or coverage gap.

## Residuals

Actual macOS, hosted proof, and the final aggregate suite remain deferred, so this review cannot be interpreted as R4 or a final three-platform support claim. MCP, Manager, and Docker domain adapters remain owned by B04, B03, and B05 respectively.

## Decision

Primary recommendation: `Gate R1a GO`.

This recommendation is not final until the independent reviewer accepts the implementation and evidence package. B02 remains blocked until then.
