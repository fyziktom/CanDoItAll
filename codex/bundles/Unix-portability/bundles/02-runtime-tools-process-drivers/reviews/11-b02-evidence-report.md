# B02 evidence report

## Outcome

B02 completed with independent Workbench gate GO. Project Structure runtime metadata compiles into typed plans; ordinary execution delegates to a host-lifetime Workbench registry that retains exact B01 process/session ownership; script approval is enforced at launch; agent capability projections do not expose resolved physical paths; terminal presentation and elevation remain separate optional capabilities; and path authority remains with the Core workspace/external-target contracts.

The proof tier is `Governed` because B02 changes P0 runtime execution, path, shell, and elevation behavior. Validation follows the operator-requested fast ladder: the named runtime slice on Windows/Linux, the affected component adapter, two focused browser profiles, and incremental affected-project builds. A new full solution suite is intentionally deferred to the next aggregate gate.

## Implementation

- `ProjectStructureRuntimePlanCompiler` is pure. It compiles typed .NET, direct executable, Docker, Python, PowerShell-script, and POSIX-script definitions without filesystem, process, UI, or dependency-injection access.
- `ProjectStructureRuntimeExecutionAdapter` resolves an executable through the B01 locator and delegates an exact `WorkspaceProcessSessionRequest` to `ProjectStructureRuntimeSessionRegistry`. The host-lifetime registry retains the exact session identity through completion/stop/shutdown, never detaches it, and uses one explicitly owned DI child scope for its canonical scoped process host.
- `ProjectStructureTerminalPresenter` is presentation only. Windows uses the explicitly enabled PowerShell presenter; Linux/macOS require configured executable/prefix values. A missing desktop terminal does not block startup or direct execution.
- `ProjectStructureRuntimeElevationAdapter` exposes explicit Windows `runas` only. Linux/macOS return `Unsupported`; no `sudo`, `pkexec`, AppleScript, password prompt, or implicit fallback exists.
- `ProjectStructureRuntimePathResolver` composes `IWorkspacePathAccessGuard`, scoped external-target bindings, host syntax classification, and reparse-safe storage inspection. Operator-selected paths and agent-execution paths retain distinct authority; agent plans revalidate the working directory and every target.
- Python virtual environments invoke `Scripts/python.exe` on Windows and `bin/python` on Linux/macOS. Conda compiles to `conda run --no-capture-output`; activation is not execution authority.
- PowerShell and POSIX shell are explicit script kinds. `RequiresApproval` is enforced by the launcher and the Workbench asks for explicit confirmation immediately before each script start. The legacy migrator unwraps only bounded static cmd/PowerShell shapes, allowlists harmless pre-`-Command` host flags, and rejects dynamic, chained, redirected, encoded, duplicate-command, or semantically ambiguous host options for operator repair.
- Workbench UI and agent projections use `Run`, `Stop`, `Open terminal`, `Elevated launch`, and typed unavailable diagnostics. Environment entry point/arguments are persisted and edited as typed metadata. Agent projections intentionally omit resolved display-command and physical-working-directory fields.
- `Workbench:RuntimePresentation` is documented. Windows presentation can be disabled; Linux/macOS terminal executables and argument prefixes are opt-in configuration.

## Requirement evidence

| Requirement | Proof |
|---|---|
| NODE-001 | Pure compiler tests plus source assertion that launcher/execution/display/presentation are separate; ordinary plans are typed executable/argv/environment/working-directory/target values. |
| NODE-002 | Explicit PowerShell/POSIX plan kinds; launch-time operator approval enforcement; encoded/dynamic/chained negative tests; ordinary .NET/Docker/Python/Node plans do not use shell fallback. |
| NODE-003 | Windows `Scripts/python.exe`, Linux/macOS `bin/python`, direct actual process adapter test, Conda `run` compilation, and no activation command. |
| NODE-004 | Direct and terminal probes are independent; default/configured terminal tests and the headless browser profile prove startup/direct behavior is not terminal-dependent. |
| NODE-005 | Windows-only `runas` adapter; Linux/macOS `Unsupported` tests and source scan prove no implicit Unix/macOS elevation. |
| NODE-006 | Bounded cmd/PowerShell unwrap tests and explicit operator-repair diagnostics for encoded/dynamic/chained input, duplicate `-Command`, `-File ... -Command`, and `-WorkingDirectory ... -Command`. |
| NODE-007 | Capability resolver/component tests plus four visually inspected screenshots for available, dependency-missing, headless terminal-only, and foreign-path states; agent projections omit physical command/path fields. |
| NODE-008 | Workspace/external-target/foreign-path/reparse tests, agent versus operator authority tests, and browser proof for a persisted foreign-host path. |

## Focused behavior evidence

| Host/profile | Slice | Result | Artifact |
|---|---|---:|---|
| Windows / .NET SDK 10.0.302 | Runtime compiler, launcher, path, adapter, metadata, capability, approval, and lifecycle tests | 98/98 | `artifacts/unix-portability/B02/windows/b02-unit-windows.trx` |
| Linux Docker / `mcr.microsoft.com/dotnet/sdk:10.0` / digest `ed034a8...ad4664` | Same current Release test assembly under Linux .NET | 98/98 | `artifacts/unix-portability/B02/linux/b02-unit-linux.trx` |
| Windows | Affected action-catalog component adapter | 24/24 | `artifacts/unix-portability/B02/windows/b02-components-windows.trx` |
| Windows default presentation | Runtime-node Playwright scenario | 1/1 | `artifacts/unix-portability/B02/windows/b02-runtime-node-playwright.trx` |
| Windows headless presentation | Same scenario with terminal presentation disabled | 1/1 | `artifacts/unix-portability/B02/windows/b02-runtime-node-playwright-headless.trx` |

The unit slice includes an actual `dotnet --version` process start through `LocalWorkspaceProcessHost`, typed request capture, exact owned-session stop and host-shutdown cleanup, already-cancelled/interrupted shutdown cleanup across every owned session, new-scope recovery, cancellation, enforced script approval, dependency missing, terminal-only, configured Linux terminal, Windows/non-Windows elevation, Python layouts, Docker plans, strict legacy repair, path containment, external alias authority, agent non-disclosure, and metadata composition.

## Browser proof

- `b02-runtime-capabilities-available.png`: direct `Run` plus optional Windows terminal/elevation.
- `b02-runtime-capabilities-dependency-missing.png`: disabled action with executable-dependency remediation.
- `b02-runtime-capabilities-headless.png`: a terminal-only Python plan remains explicitly unavailable when terminal presentation is disabled.
- `b02-runtime-capabilities-foreign-path.png`: persisted foreign-host metadata fails with a typed path-syntax diagnostic.

All four screenshots use a 1600×1000 viewport, remain on the Project Structure route, contain no physical path, credential, token, or connection value, and were visually inspected after the final runs.

## Build and architecture evidence

| Affected project | Result | Artifact |
|---|---:|---|
| `CanDoItAll.Modules.Workbench` | 0 warnings / 0 errors | `artifacts/unix-portability/B02/windows/b02-workbench-build.log` |
| `CanDoItAll.Composition` | 0 warnings / 0 errors | `artifacts/unix-portability/B02/windows/b02-composition-build.log` |
| `CanDoItAll.Web` | 0 warnings / 0 errors | `artifacts/unix-portability/B02/windows/b02-web-build.log` |

- `ProjectStructureRuntimeLauncher` contains no `Process.Start`, `ProcessStartInfo`, terminal selection, or executable-resolution implementation.
- The only B02 runtime `Process.Start` sites are the optional terminal and Windows `runas` adapters.
- No `.csproj`, `.props`, `.targets`, solution, or package file changed, so the accepted dependency graph is unchanged.
- `artifacts/unix-portability/B02/b02-governed-proof.json` records the original four failing-first/coverage corrections plus the four independent-review remediations and scoped-host composition correction, 37 source hashes and 13 test/build/UI/host artifact hashes, refreshed source assertions, and Linux image provenance. Primary recomputation found 0 mismatches and 0 anti-stub markers.

## Redaction and artifact coverage

The schema-3 scanner accounts for ten text artifacts plus its control output and four binary screenshots. It reports 0 oversized or unreadable text files and 0 findings. The screenshots are explicitly classified as non-text and were manually inspected. Artifact: `artifacts/unix-portability/B02/b02-secret-scan.json`.

## Residual boundaries

- Genuine macOS execution and terminal presentation remain deferred under `RUNTIME-MACOS-VALIDATION-001`. Deterministic macOS compiler/capability fixtures do not constitute actual-host proof.
- The Linux actual-host slice proves direct execution and policy behavior, not a desktop terminal launch. Linux terminal presentation remains opt-in and requires its configured terminal contract to be tested on the deployment profile that enables it.
- The B02 registry is an in-memory Workbench host-lifetime owner, not a durable cross-restart supervisor. B03 separately owns Manager-launched process supervision/recovery; it does not take ownership of Workbench runtime-node sessions.
- The full solution suite, hosted validation, and final R4 remain deferred. Any aggregate failure remains blocking at its later gate.

## Gate recommendation

Final result: `B02 Workbench gate GO`, accepted by the independent architecture/runtime/security re-review in review 13. B03 is the next eligible subbundle after bundle index/checksum validation.
