# B03 evidence report

## Outcome

B03 is implemented and ready for independent Gate R2 re-review. Manager-launched Watch, Tailwind, and tuning processes now use the B01 canonical process host while Manager retains a durable, non-secret, registry-first supervision policy. Recovery is host-specific and fail-closed: Windows WMI is isolated in one leaf, Linux uses bounded `/proc`, and macOS combines kernel `libproc` identity with a strictly parsed invariant `ps` command record. No missing or ambiguous evidence authorizes termination.

The proof tier is `Governed` because B03 changes P0 process ownership and termination behavior. Validation follows the operator-requested fast ladder: named Manager unit/lifecycle and `ManagerPortability` integration slices on Windows and pinned Linux, affected-project builds, one Manager startup smoke, source/graph assertions, and a complete artifact redaction scan. No broad solution suite was rerun.

## Implementation

- `ManagerProcessCoordinator` registers every launched process before returning it and persists PID, exact start identity, executable identity, observed/planned command fingerprints, the physical workspace root required for ownership, user, parent, purpose, lease owner, and lifecycle state. Raw argv, environment values, and secret text are not persisted.
- Recovery begins from the durable registry. A live PID is actionable only when exact start identity and available executable, owner, command, and workspace-bound evidence match. Parent identity is required and persisted at launch, but restart verification intentionally permits a changed PPID because Unix reparents a surviving child after its original Manager exits. Missing, denied, malformed, raced, or mismatched authoritative evidence becomes an explicit diagnostic/manual-cleanup state.
- `WindowsManagerProcessDiscovery` is the only `System.Management` owner. `LinuxManagerProcessDiscovery` reads bounded `/proc/<pid>` files. `MacOsManagerProcessDiscovery` reads microsecond start/parent/owner identity from `libproc`, then runs `/bin/ps` through the B01 host for bounded invariant executable/command evidence.
- Watch, Tailwind, and tuning no longer construct or enumerate `Process`. They use one composition-owned B01 host through the Manager coordinator and retain owner-specific lifecycle, status, registry, and output behavior.
- Long-running Manager sessions request graceful-then-bounded-force termination. Existing B01 callers retain force-tree termination by default, and recovered identities remain force-tree because there is no live session channel to trust.
- Tailwind source monitoring retains generation/fingerprint, duplicate suppression, overflow rescan, polling fallback, and convergence behavior. A fingerprint is committed only after successful output publication, so transient build failure is retried without another file change. Physical ignored-name/extension comparisons now use the detected filesystem comparer.
- Shutdown ignores an already-cancelled external host token only for two bounded mandatory phases: background-loop stop and registered-child reconciliation. Active lease termination and lifecycle persistence remain idempotent and retry-safe.
- Tuning tokenizes the configured template before substituting typed path values, so a legal Unix quote in a filename cannot restructure argv.

## Requirement evidence

| Requirement | Proof |
|---|---|
| MGR-001 | Durable registry restart/non-disclosure tests, exact identity verification, lease conflict tests, startup recovery, real Linux registry-backed launch/termination, and actual Linux parent-exit/recovery. |
| MGR-002 | Source/architecture assertion that `System.Management` occurs only in the Windows leaf; deterministic Windows mapper coverage; actual Windows permission denial remains fail-closed. |
| MGR-003 | Bounded Linux `/proc` parser tests for spaces/parentheses, malformed/oversized/missing/raced evidence, plus actual Linux discovery and lifecycle integration. |
| MGR-004 | Kernel `libproc` microsecond identity buffer fixture plus strict invariant macOS `ps` ProbeAsync fixtures for shape, locale, rooted executable, timeout, cancellation, permission, generic failure, and race handling. Actual macOS is explicitly deferred by operator instruction. |
| MGR-005 | PID reuse and owner/executable/command/start-identity mismatch tests plus reparent-aware recovery; no name/substr termination or broad process enumeration remains. B01 independently revalidates exact start/executable identity before terminating. |
| MGR-006 | Watch, Tailwind, and tuning use the B01 host through an owner-specific coordinator; opt-in graceful/force lifecycle, already-cancelled shutdown reconciliation, lease-persistence retry, and transient Tailwind build convergence tests pass. |
| MGR-007 | Core physical policy governs restore roots/references and executable identity; Windows case-variant root, deterministic sensitive/insensitive executable, and Linux case-distinct watcher/capsule regressions pass. |

## Focused behavior evidence

| Host/profile | Slice | Result | Artifact |
|---|---|---:|---|
| Windows / .NET SDK 10.0.302 | Manager ownership, platform parser, lifecycle, watcher, tuning, and architecture tests | 139/139 | `artifacts/unix-portability/B03/windows/b03-unit-windows.trx` |
| Windows | `ManagerPortability` integration, including shutdown interruption and Tailwind retry | 11/11 | `artifacts/unix-portability/B03/windows/b03-integration-windows.trx` |
| Linux Docker / `mcr.microsoft.com/dotnet/sdk:10.0` / digest `ed034a8...ad4664` | Same unit/lifecycle slice, including deterministic reparent-aware verification | 139/139 | `artifacts/unix-portability/B03/linux/b03-unit-linux.trx` |
| Same Linux container | `ManagerPortability` integration, including actual `/proc`, real B01 session, actual parent-exit/recovery, shutdown interruption, and Tailwind retry | 11/11 | `artifacts/unix-portability/B03/linux/b03-integration-linux.trx` |
| Windows | Manager composition/startup with Watch and Tailwind autostart disabled | HTTP 200; both stopped | `artifacts/unix-portability/B03/windows/b03-manager-startup.stdout.log` |

The Windows WMI query is denied by the current sandbox. The actual-host test proves the typed `PermissionDenied` result and that no termination authority is produced. Complete Windows mapping is covered by pure fixtures; normal deployment availability is not overstated.

## Build and architecture evidence

| Affected project | Result | Artifact |
|---|---:|---|
| `CanDoItAll.Manager` | 0 warnings / 0 errors | `artifacts/unix-portability/B03/windows/b03-manager-build.log` |
| `CanDoItAll.Tests.Unit --no-dependencies` | 0 warnings / 0 errors | `artifacts/unix-portability/B03/windows/b03-unit-project-build.log` |
| `CanDoItAll.Tests.Integration --no-dependencies` | 0 warnings / 0 errors | `artifacts/unix-portability/B03/windows/b03-integration-project-build.log` |

- Manager production sources contain no `Process.Start`, `new Process`, or `GetProcessesByName` call.
- `System.Management` and `ManagementObjectSearcher` occur only in `WindowsManagerProcessDiscovery.cs`.
- B03 adds only an outer Manager application reference to the existing inner `CanDoItAll.AgentFramework.Core` assembly. A local XML graph audit found 105 projects, 632 in-repository project-reference edges, zero cycles, and no Core-to-Manager edge.
- CodeAnalytics was not used because the environment rejected private-source export; local source, build, and graph evidence is authoritative for this gate.
- The source-reference manifest contains 62 records, 62 unique IDs, 62 unique paths, and zero missing paths.
- `artifacts/unix-portability/B03/b03-governed-proof.json` binds eleven failing-first/characterization corrections, ten semantic assertions, 27 source hashes, and 11 test/build/host hashes. Primary recomputation found zero mismatches.

## Redaction and artifact coverage

The schema-3 scanner accounts for 13 candidate files: 12 text artifacts scanned and its output excluded as the control input. It reports zero oversized, non-text, unreadable, or otherwise uncovered files and zero findings. Artifact: `artifacts/unix-portability/B03/b03-secret-scan.json`.

## Residual boundaries

- Genuine macOS execution remains deferred under `RUNTIME-MACOS-VALIDATION-001` by explicit operator instruction. Deterministic macOS parser/locale/permission/race fixtures do not constitute actual-host proof.
- Windows WMI availability depends on deployment policy. `PermissionDenied` remains a safe manual-diagnostic state, not a silent fallback or ownership grant.
- Linux `/proc`, macOS `libproc`, and macOS `ps` evidence can disappear or become unreadable during races; those cases intentionally refuse automatic termination.
- A new full solution suite, hosted validation, and final R4 remain deferred to aggregate runtime gates.

## Gate recommendation

Primary recommendation: `Gate R2 GO`, pending the required independent architecture/runtime/security review.
