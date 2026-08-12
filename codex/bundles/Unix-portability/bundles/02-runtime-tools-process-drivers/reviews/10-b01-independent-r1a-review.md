# B01 independent Gate R1a review

Date: 2026-08-11

## Decision

`Gate R1a GO`.

No blocking architecture, runtime, lifecycle, executable-resolution, environment, cancellation, ownership, receipt, redaction, or evidence finding remains for `EXEC-001` through `EXEC-008`. After final index/checksum and canonical gate bookkeeping, B02 alone may become eligible. This decision does not advance hosted validation, genuine macOS validation, a full-suite aggregate gate, or final Gate R4.

## Findings and remediation verified

The independent review found and held the gate for two material issues during review. Both are closed in the final frozen snapshot.

1. The first managed `workspace_dotnet_run` lifecycle used a generated PowerShell `Start-Process` path outside the canonical process host. The final source launches `dotnet` directly through `IWorkspaceLongRunningProcessHost` and `IWorkspaceProcessSession`. The host owns start, readiness-session identity, bounded output capture, detach, disposal, termination, and residual reporting; the durable execution-run lease remains the recovery handoff. `BuildDotnetHttpRunPowerShellScript`, `BuildDotnetStopPowerShellScript`, and the corresponding generated launch/stop scripts are absent.
2. The first owned-process check accepted start timestamps within one second. Because many applications share the same `dotnet` executable fingerprint, rapid PID reuse inside that tolerance could authorize termination of a foreign process. `LocalWorkspaceProcessHost.MatchIdentity` now requires exact UTC start-time equality as well as PID and the kernel-observed executable-path fingerprint. The actual-host test round-trips the identity through JSON before termination from a new host; the adversarial test changes the timestamp by 250 ms and proves `IdentityMismatch`, a possible residual, and that the process remains alive. Both tests are present in the final Windows and Linux 133/133 TRXs, and an independent Windows no-build rerun of `LocalWorkspaceProcessHostTests` passed 8/8.

No further blocking finding was identified.

## Architecture and requirement disposition

- `EXEC-001`/`EXEC-002`: requests carry executable, ordered argv, working directory, environment, timeout/output limits, and boundary facts as typed values. An independent production-source scan found `Process.Start`/`ProcessStartInfo` creation only in `LocalWorkspaceProcessHost` across the B01-owned Git, workspace, external-tool, and module surfaces. Git and external-tool implementations adapt to that host rather than owning parallel runners.
- `EXEC-003`: `WorkspaceExecutableLocator` separates explicit paths from ordered PATH lookup, applies PATHEXT only on Windows, requires Unix execute permission, resolves final symlink identity, preserves host case rules, and rejects URI/control/foreign absolute syntax with typed failure classifications. The focused artifacts contain the corresponding Windows, Unix, symlink, collision-order, missing, and not-executable cases.
- `EXEC-004`: environment construction uses ordinal-ignore-case comparison only on Windows and ordinal comparison on Linux/macOS, inherits a bounded common/host/tool allowlist, excludes ambient credential variables, and applies explicit overlays per request. Persisted command receipts record sorted names, not values.
- `EXEC-005`/`EXEC-008`: timeout and caller cancellation terminate the owned process tree, drain bounded output, distinguish their termination reason, and report `TerminationFailed`/`ResidualProcessPossible` when confirmation is unavailable. Actual Windows/Linux tests cover descendants, stdin closure, stream-drain races, detach/termination, and foreign-identity refusal. The evidence does not show a failing behavior requiring a Job Object or Unix process-group adapter, which is consistent with ADR-R11.
- `EXEC-006`: the public shell-neutral long-running contract is available for B02 without reconstructing shell commands. Hosting exposes one singleton host identity through both process interfaces; the product module exposes one scoped identity. Managed run registers a pending durable lease before launch, activates it only after the matching startup receipt is persisted, coordinates concurrent cleanup, and removes the lease only after confirmed stop. Identity mismatch and uncertain recovery retain the lease.
- `EXEC-007`: `SensitiveTextRedactor` is applied defensively by both process-result and receipt paths. Sequence-aware argv projection masks inline values and standalone sensitive switches followed by a separate value. Output, failure text, lifecycle JSON, and log artifacts are redacted before persistence. Receipts retain logical workspace paths and truthfully label the local host as policy-only rather than an enforced sandbox.
- Dependency direction remains coherent. The deleted Foundation Git process implementation is replaced by a Core adapter over Foundation contracts; SharedKernel contains only the pure shared redaction policy; composition remains in Hosting/Module. No project, props, targets, or solution file changed, and the accepted project graph therefore was not widened by B01.

## Evidence reconciliation

- Final TRX counters independently parse to 133 passed, 0 failed, 0 skipped on Windows and the same 133/133 on Linux. The `ProcessPortability` integration artifacts independently parse to 2/2 on each host. The Linux artifacts execute under the Linux .NET 10.0 test host and contain the same lifecycle/identity regressions.
- The five affected Release build logs report 0 warnings and 0 errors for SharedKernel, AgentFramework Core, Hosting, Tools, and Modules.AgentFramework.
- `artifacts/unix-portability/B01/b01-governed-proof.json` records four failing-first corrections, six production/source assertions including anti-stub, 31 current/deleted-source identities, four TRX hashes, and five build hashes. Independent SHA-256 recomputation matched all 31 source entries and all 9 evidence artifacts; every non-deleted path exists, the declared deleted Git implementation remains absent, and the anti-stub scan found no `TODO`, `FIXME`, or `NotImplementedException` marker in the governed source set.
- The runtime source-reference manifest independently resolves to 37 records, 37 unique IDs, 37 unique paths, and 0 missing paths. Direct assertions also confirm the obsolete shell lifecycle methods, `DefaultGitCommandExecutor`, and `LocalExternalProcessRunner` are absent.
- The schema-3 secret scan accounts for 11 candidates as 10 scanned text artifacts plus its control output, with 0 oversized, non-text, unreadable, or otherwise uncovered files and 0 findings. Findings, if present, are defined as metadata and truncated fingerprints rather than source excerpts.
- The portable runtime validator passed before this review was added with 317 files, 0 errors, and 0 warnings using `--skip-checksums`. `git diff --check` reports no content error; only the three already-recorded traceability CSV line-ending notices remain.

## Residual boundaries

- Genuine macOS process execution is still unverified. Deterministic macOS executable-name and environment-comparer fixtures are sufficient for this operator-authorized R1a progression only; no actual-macOS or final support claim may be inferred.
- The local host is deliberately `PolicyOnlyLocal`: child processes inherit the user's filesystem and network rights. B02 and later adapters must preserve that truthful boundary and must not present it as OS/container isolation.
- Detached managed-run stdout/stderr files are bounded readiness-time snapshots rather than a streaming lifetime log. The session continues draining bounded output and cleanup remains owned, so this does not violate the B01 gate, but later observability must define a separate durable streaming/final-output contract if complete post-readiness logs are required.
- A caller-selected occupied loopback port can still make readiness attribution inherently racy; default managed runs reduce this risk with a dynamically reserved loopback port. Later lifecycle UX should avoid claiming cryptographic/process attribution from an HTTP success alone.
- A new full solution suite, hosted validation, exact-commit branch/merge evidence, and final R4 remain explicitly deferred. Any aggregate regression discovered there remains blocking for the applicable later gate.

## Gate handoff

Record R1a GO in the canonical gate/status files only after regenerating the bundle index and checksums and running the intended final validator. B02 may then proceed against the typed execution/session boundary. B03 through B07 retain their declared dependency gates.
