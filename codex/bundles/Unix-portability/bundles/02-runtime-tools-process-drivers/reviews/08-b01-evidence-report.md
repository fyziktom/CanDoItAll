# B01 evidence report

## Outcome

B01 is ready for independent Gate R1a review. Workspace commands, managed long-running `dotnet run`, Git, Windows path alias compatibility, and external process tools now share one low-level process implementation. Executable and environment rules are host-correct, cancellation and timeout produce explicit termination evidence, and persisted command receipts are defensively redacted.

The proof tier is `Governed`. Validation follows the operator-requested fast ladder: named regressions, affected projects, and actual Windows/Linux process behavior. A new full solution suite is intentionally deferred to the next meaningful aggregate gate; hosted and genuine macOS proof remain deferred and are not claimed.

## Implementation

- `LocalWorkspaceProcessHost` is the only `System.Diagnostics.Process` implementation in B01-owned production paths. It writes and closes optional stdin, distinguishes timeout from caller cancellation, kills the owned process tree, waits for bounded confirmation, drains output, and reports `TerminationFailed` plus `ResidualProcessPossible` when cleanup cannot be confirmed.
- `IWorkspaceLongRunningProcessHost` and `IWorkspaceProcessSession` make start, readiness, detach, disposal, and verified termination explicit. Managed HTTP `dotnet run` launches `dotnet` directly; no generated PowerShell plan or `Start-Process` intermediary owns the kept-alive child. Stop and recovery verify PID, exact UTC launch timestamp, and an opaque fingerprint captured from the kernel-reported executable identity. Linux directory-symlink aliases and already-exited short commands are handled without weakening the running-process identity check. A 250 ms start-time mismatch is rejected, closing rapid same-executable PID reuse.
- `WorkspaceExecutableLocator` deterministically resolves explicit paths and ordered PATH candidates, applies PATHEXT only on Windows, requires Unix execute permission, resolves final symlink identity, rejects foreign/URI/control-character syntax, and returns typed missing/not-executable failures.
- `WorkspaceCommandEnvironmentPolicy` uses `OrdinalIgnoreCase` only for Windows and `Ordinal` for Linux/macOS. It composes common, host-specific, and tool-specific safe inherited names. Ambient credentials are excluded; explicit recipe overlays remain possible.
- `WorkspaceExternalProcessRunner` adapts the external-tool contract to the canonical host, applies UTF-8 output byte limits, and maps timeout/cancellation/residual outcomes. `ExternalProcessToolInvoker` no longer owns a fallback runner and no longer copies stdout/stderr into diagnostics.
- `WorkspaceGitCommandExecutor` adapts the Git domain contract at the Core boundary. The old Foundation `DefaultGitCommandExecutor` direct-process implementation was removed; the Git project remains infrastructure-neutral and retains typed arguments and sanitized display text.
- `WorkspacePathAliasSession` invokes the explicit Windows system `subst.exe` path through the canonical host with typed create/delete arguments and bounded async cleanup.
- `SensitiveTextRedactor` is the shared pure redaction policy. Command output, failure text, argument projections, receipt payloads, and persisted stdout/stderr are redacted; receipts store approved environment names only. Argument redaction is sequence-aware, so both `--token=value` and the typed pair `--token value` are masked before process and descriptor receipts are persisted.

## Requirement evidence

| Requirement | Proof |
|---|---|
| EXEC-001 | Typed foreground and session requests, `GitCommandSpec`, and `ExternalProcessRunRequest`; managed `dotnet run` directly selects `dotnet` and no shell reconstructs the lifecycle command. |
| EXEC-002 | Static scan finds `ProcessStartInfo`/`Process.Start` only in `LocalWorkspaceProcessHost`; external and Git paths are adapters. |
| EXEC-003 | PATHEXT order, exact Unix/macOS names, PATH collision order, explicit path, execute-bit, symlink, foreign syntax, and typed missing/not-executable tests. |
| EXEC-004 | Windows/Unix comparer tests, case-distinct Unix overlay test, safe tool-specific inheritance tests, and ambient-credential exclusion tests. |
| EXEC-005 | Actual Windows/Linux timeout, caller-cancellation, descendant cleanup, stdin closure, output drain, and residual-result mapping tests. |
| EXEC-006 | Typed sessions own long-running launch/readiness/detach/disposal. Durable leases store a startup receipt containing PID, exact UTC start timestamp, and opaque executable fingerprint; stop/recovery terminate only a matching process. JSON round-trip and sub-second mismatch regressions prove exact identity persistence and fail-closed PID reuse. Per-execution factories own the host instance and DI adapters receive the configured long-running host. |
| EXEC-007 | Process and descriptor receipt tests cover sorted environment names without values and secret redaction across output, failure, inline arguments, next-element argv values, and direct writer use; schema-3 artifact scan is clean. |
| EXEC-008 | Actual Windows/Linux tests did not demonstrate a gap in `Kill(entireProcessTree: true)` plus bounded confirmation, so no Job Object/process-group adapter was added. ADR-R11 records the decision. |

## Focused behavior evidence

| Host | Slice | Result | Artifact |
|---|---|---:|---|
| Windows / .NET SDK 10.0.302 | B01 named unit slice plus managed lifecycle/lease regressions | 133/133 | `artifacts/unix-portability/B01/windows/b01-unit-windows-lifecycle.trx` |
| Windows / .NET SDK 10.0.302 | `ProcessPortability` integration (external `dotnet --version`, Git `git --version`) | 2/2 | `artifacts/unix-portability/B01/windows/b01-integration-windows-lifecycle.trx` |
| Linux Docker / `mcr.microsoft.com/dotnet/sdk:10.0` | Same B01 unit/lifecycle slice | 133/133 | `artifacts/unix-portability/B01/linux/b01-unit-linux-lifecycle.trx` |
| Same Linux container | Same `ProcessPortability` integration | 2/2 | `artifacts/unix-portability/B01/linux/b01-integration-linux-lifecycle.trx` |

The Linux container executes the current Release assemblies through the Linux .NET test host. The B01 slice includes actual Unix file mode/symlink behavior, lexical-versus-kernel executable identity, short-lived process races, detached-session termination from a new host, mismatched foreign-process refusal, and parent/descendant cleanup. Deterministic macOS fixtures prove exact executable naming and ordinal environment rules. Genuine macOS behavior remains deferred under `RUNTIME-MACOS-VALIDATION-001`.

## Build and architecture evidence

| Affected project | Result | Artifact |
|---|---:|---|
| `CanDoItAll.SharedKernel` | 0 warnings / 0 errors | `artifacts/unix-portability/B01/windows/b01-sharedkernel-build.log` |
| `CanDoItAll.AgentFramework.Core` | 0 warnings / 0 errors | `artifacts/unix-portability/B01/windows/b01-core-build.log` |
| `CanDoItAll.AgentFramework.Hosting` | 0 warnings / 0 errors | `artifacts/unix-portability/B01/windows/b01-hosting-build.log` |
| `CanDoItAll.AgentFramework.Tools` | 0 warnings / 0 errors | `artifacts/unix-portability/B01/windows/b01-tools-build.log` |
| `CanDoItAll.Modules.AgentFramework` | 0 warnings / 0 errors | `artifacts/unix-portability/B01/windows/b01-module-build.log` |

- No `.csproj`, `.props`, or `.targets` file changed in B01, so the accepted project dependency graph is unchanged.
- The Git Foundation implementation dependency was reduced: execution moved outward to an AgentFramework Core adapter instead of adding an outward Foundation reference.
- Static process scan over Git, workspace execution, external tools, and the module adapter finds only `LocalWorkspaceProcessHost` creating a process.
- The governed proof manifest records four failing-first corrections, every changed source SHA-256 (or the deleted Git blob identity), TRX/build hashes, semantic source assertions, and the anti-stub result: `artifacts/unix-portability/B01/b01-governed-proof.json`.
- Legacy `DefaultGitCommandExecutor` and production `LocalExternalProcessRunner` references are absent.
- The source-reference manifest contains 37 records, 37 unique IDs, 37 unique paths, and 0 missing paths after replacing the deleted Git source anchor.
- A fresh CodeAnalytics snapshot was not created because the analysis connector rejected private-source export under its policy. The compiler graph, unchanged project files, architecture source tests, and independent source review are the safe substitutes for this gate.

## Redaction and artifact coverage

The schema-3 scanner accounts for eleven candidate files: ten evidence files scanned as text and the scanner output excluded as its control input. It reports 0 oversized, non-text, unreadable, or otherwise uncovered files and 0 findings. Artifact: `artifacts/unix-portability/B01/b01-secret-scan.json`.

## Residual boundaries

- Actual macOS process behavior is deferred; B01 does not claim macOS support closure.
- MCP-specific launch resolution remains B04, Manager supervision and recovery remain B03, and Docker plugin policy/lifetime adaptation remains B05. These surfaces reuse or will adapt the primitive without moving their domain policy into B01.
- The full solution suite is deferred to a later aggregate gate in accordance with the fast validation ladder. Any broader failure found there remains blocking for final R4.
- Hosted evidence and final R4 remain blocked by their recorded external validation obligations.

## Gate recommendation

Primary recommendation: `Gate R1a GO`, subject to independent runtime/security review. B02 remains blocked until that review is recorded and portable index/checksum validation closes.
