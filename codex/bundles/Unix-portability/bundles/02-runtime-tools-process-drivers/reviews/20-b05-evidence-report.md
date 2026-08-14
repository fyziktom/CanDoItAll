# B05 evidence report

## Outcome

B05 is remediated and ready for bounded independent Gate R3b re-review. Docker plugin execution now consumes the B01 process, executable, environment, workspace, timeout, cancellation, output, and tree-termination authorities. Its typed dependency snapshot is consumed by the production runtime availability API and by the workflow execution gate. FileTools desktop actions remain optional OS delegation behind explicit feature, runtime-profile, trusted-path, desktop-session, direct-source validation, and host-bound application gates.

The proof tier is `Governed`. Validation uses the operator-requested fast ladder: exact regressions, one affected unit assembly, affected project builds, focused Windows execution, and the same prebuilt assemblies under pinned Ubuntu Docker. No broad solution suite was rerun.

## Implementation

- `DockerHostToolService` no longer constructs `LocalWorkspaceProcessHost` or resolves PATH/environment independently. Composition supplies one scoped service behind both execution and capability-probe interfaces.
- Docker capability separates executable, context/configuration, daemon, and endpoint kind. Missing executable, invalid configuration, permission denial, timeout, remote endpoint, daemon unavailability, and actual ready states are explicit.
- The scoped asynchronous workflow availability catalog overlays that typed state only for the explicit runtime API. The same evaluator fails closed before workflow execution without blocking static descriptor enumeration or creating another probe authority.
- Docker container and image discovery now distinguishes authoritative absence from every indeterminate result. Timeout, denial, malformed state, start failure, and other nonzero results cannot advance to `start`, `pull`, or `run`.
- The canonical environment policy inherits only exact Docker names with Windows-insensitive or Unix-ordinal semantics. Configuration/certificate/socket paths use the physical path authority. Endpoint/configuration values and secret-shaped text are redacted before plugin results.
- `DOCKER_HOST` now has scheme-specific authority: local Unix sockets use the host physical/link policy, Windows named pipes use strict grammar, remote endpoints reject credentials and unsupported components, and protected values include host-normalized, separator-trimmed, and local-socket redaction variants.
- `ConfiguredDesktopFileLauncher` requires explicit enablement and `HostProfileAllowsDesktop`. Composition derives the latter from the resolved interactive runtime profile, so service/headless profiles do not delegate.
- The FileTools desktop source now requires a credible Linux/macOS desktop session and rejects foreign/ambiguous absolute path syntax before native path resolution.
- Until the reviewed FileTools source is published and re-pinned, desktop integration is compiled as validated only in explicit direct-source mode. Package fallback remains build-compatible but the unverified desktop capability is typed unavailable and cannot delegate.
- FileTools rechecks cancellation immediately before `Process.Start`, closing cancellation that arrives during availability or filesystem preflight while retaining the documented fire-and-forget boundary after OS acceptance.
- Preferred application and workspace local-open boundaries preserve significant physical whitespace, reject foreign syntax, retain host-bound rebind semantics, and use the shared reparse-safe containment policy.
- `Directory.Build.targets` keeps package fallback pins but selects direct Components/FileTools sibling project references for this development run. MSBuild evaluated `UseLocalCanDoItAllLibraries=true`.

## Requirement evidence

| Requirement | Proof |
|---|---|
| PLUG-001 | Zero Docker duplicate-process-owner hits; injected canonical dependencies; one scoped DI instance; build and both-host focused proof. |
| PLUG-002 | Typed staged probe consumed by production runtime availability and execution gating; fail-closed container/image mutation sequences; scheme-specific endpoint/path/redaction tests; missing, invalid, permission, timeout, remote, unavailable, and actual Windows daemon-ready evidence. |
| PLUG-003 | `evidence/b05-filetools-compatibility.md` binds the exact direct-source head/delta and both-host proof, records package `0.1.18` as unexecuted, and proves package-mode desktop integration is unavailable until publication/re-pin. |
| PLUG-004 | Two-factor application/profile/direct-source gating, host-bound preferences, trusted/reparse-safe local-open paths, no-delegation headless/package-mode tests, and a final pre-delegation cancellation checkpoint. |
| PLUG-005 | `inventories/external-dependency-ledger.csv` records identity, source, profiles, probes, permissions, failure modes, remediation, evidence, and status for every B05 external/native dependency. |

## Focused behavior evidence

| Host/profile | Slice | Result | Artifact |
|---|---|---:|---|
| Windows / .NET SDK 10.0.302 | Expanded B05 unit, architecture, environment, preference, and path slice | 144/144 | `artifacts/unix-portability/B05/windows/b05-unit-windows.trx` |
| Windows with Docker client/server 29.6.2 | Required Docker-ready and headless desktop integration | 2/2 | `artifacts/unix-portability/B05/windows/b05-integration-windows.trx` |
| Windows | FileTools desktop compatibility | 19/19 | `artifacts/unix-portability/B05/windows/b05-filetools-windows.trx` |
| Ubuntu 24.04.4 Docker / image digest `sha256:72dd743...01b0` | Exact required B05 unit filter | 62/62 | `artifacts/unix-portability/B05/linux/b05-unit-linux.trx` |
| Same Ubuntu container | Shared rooted physical-path identity | 4/4 | `artifacts/unix-portability/B05/linux/b05-path-linux.trx` |
| Same Ubuntu container | Typed Docker dependency state and headless desktop integration | 2/2 | `artifacts/unix-portability/B05/linux/b05-integration-linux.trx` |
| Same Ubuntu container | FileTools desktop compatibility | 19/19 | `artifacts/unix-portability/B05/linux/b05-filetools-linux.trx` |
| Windows | Bounded Docker availability/mutation/endpoint and FileTools integration remediation | 38/38 | `artifacts/unix-portability/B05/windows/b05-remediation-unit-windows.trx` |
| Same Ubuntu container | Same bounded remediation unit slice | 38/38 | `artifacts/unix-portability/B05/linux/b05-remediation-unit-linux.trx` |
| Windows | FileTools final-cancellation and desktop compatibility remediation | 20/20 | `artifacts/unix-portability/B05/windows/b05-remediation-filetools-windows.trx` |
| Same Ubuntu container | Same FileTools remediation slice | 20/20 | `artifacts/unix-portability/B05/linux/b05-remediation-filetools-linux.trx` |
| Windows | Plugin/desktop integration remediation | 2/2 | `artifacts/unix-portability/B05/windows/b05-remediation-integration-windows.trx` |
| Same Ubuntu container | Plugin/desktop integration remediation | 2/2 | `artifacts/unix-portability/B05/linux/b05-remediation-integration-linux.trx` |

## Build and architecture evidence

Ten affected build logs report zero warning/error hits: the original seven plus refreshed Web runtime availability composition, Docker plugin remediation, and sibling FileTools remediation builds. Durable logs are under `artifacts/unix-portability/B05/windows/`.

- Docker production has zero `Process.Start`, `ProcessStartInfo`, `new Process`, or `new LocalWorkspaceProcessHost` hits.
- The local graph contains 106 projects, 635 in-repository references, and zero cyclic projects. B05 adds only an incoming test-to-integration project reference.
- The source-reference manifest contains 135 records, 135 unique IDs, 135 unique paths, and zero missing paths.
- `artifacts/unix-portability/B05/b05-governed-proof.json` binds 15 failing-first/correction records, 16 semantic assertions, 29 source hashes including the three sibling FileTools files, 13 test hashes, ten build hashes, and two host-evidence hashes. Primary recomputation found zero mismatches.
- The Components catalog MCP required by the Components skill was unavailable twice with `Transport closed`. No Components source was changed; the exact clean Components head and active direct-reference mode are recorded and affected builds are green.

## Redaction and artifact coverage

The refreshed schema-3 scanner accounts for 27 candidates as 26 scanned text artifacts plus its output control. It reports zero oversized, non-text, unreadable, or otherwise uncovered files and zero findings. Artifact: `artifacts/unix-portability/B05/b05-secret-scan.json`.

## Residual boundaries

- Actual macOS desktop and Docker execution remains deferred by explicit operator instruction. Deterministic macOS profile tests are not represented as actual-host proof.
- FileTools package fallback remains pinned at `0.1.18` but is not claimed as executed compatibility evidence. Desktop integration is unavailable in package mode. This run executes exact direct source at base head `f31e20d...` plus governed working-tree files; publishing/re-pinning is deferred until the development run completes.
- Desktop cancellation is pre-delegation only; an OS shell-accepted application is fire-and-forget and cannot truthfully be recalled.
- Hosted CI and final broad Windows/Linux R4 evidence remain B07 scope.

## Gate recommendation

Final outcome: `Gate R3b GO`, accepted by the bounded independent remediation re-review in review 22.
