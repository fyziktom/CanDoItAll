# 04 — Validation Matrix

Every row names **where** it runs, **how** the result is observed, and **what passes**. Record
results in `proof/VALIDATION-LOG.md` with the exact command, the environment and the output
excerpt.

## Layers

| Layer | Where | Covers |
| --- | --- | --- |
| U | Unit, all three OS in the CI `stable` job; Windows locally | Pure policy: per-OS defaults with an injected platform, budgets, composition rules, sweep decisions |
| H | `[HostPlatform]` Integration: Windows locally, Linux in Docker, macOS in the CI `tests-host` lane | Real file modes, links, named pipes, deletion safety, real child processes |
| I | Integration with isolated PG18 | Launchers wired end to end, receipts, runtime registry |
| C | Components (bUnit) | Diagnostics and runtime-node guidance rendering |
| A | HTTP API against a real host | Readiness payload, process launch and readback, receipts |
| E | UI on 5032 (Claude Browser), large screen only | Operator and agent journeys |

## Cases

| # | Case | Layers | Pass condition |
| --- | --- | --- | --- |
| V1 | The default root per OS is short, deterministic and ignores a long `TMPDIR` on Unix | U | Windows root ≤ 96; Linux/macOS root ≤ 36; a long `TMPDIR` still gives the literal `/tmp` or `/private/tmp` form |
| V2 | An over-budget or invalid **explicit** override blocks startup | U, H | A readiness failure with an actionable message naming `ControlPlane:ProcessScratchRootPath` |
| V3 | An unusable **default** root degrades | U, H | Readiness `Degraded`; the launch succeeds with the inherited host temp; the receipt says `InheritedHostTemp` with a reason |
| V4 | Unix root privacy | H (Linux, macOS) | Mode 0700; a pre-created foreign or linked root is rejected (not used) |
| V5 | **Deletion does not follow links** | H (all OS) | A junction or symlink inside scratch that points at an outside folder: after release, the link is gone and the outside file is intact |
| V6 | Socket budget | H (all OS; macOS is the critical one) | A child .NET process creates and connects a `NamedPipeServerStream` with `TMPDIR` = its scratch |
| V7 | Composition never emits empty temp or home values | U | Null or empty explicit values are dropped and diagnosed; a missing Unix `HOME` gets the toolchain home |
| V8 | Toolchain by executable | U, I | Static serve, skill `.py`/`.ps1`/`.js`, spreadsheet and Direct nodes get their toolchain list |
| V9 | Secrets stay out | U, I, E | `OPENAI_API_KEY` is absent in every managed child **and** in the terminal presentation |
| V10 | `NetworkTrust` opt-in | U, I | Off: proxy/CA names absent. On: present. A denylisted extension name blocks startup |
| V11 | Scratch shared per execution run and released at its end | I, E | Two commands in one run see the same `TEMP`; the folder is gone after the run's terminal state |
| V12 | Runtime-node scratch released on Stop and on exit | I, E | The folder exists while running and is gone after Stop, and after a self-exit |
| V13 | MCP session scratch | I | Created on start, gone after the session is disposed |
| V14 | Sweep after a crash | H, E | A leaf with a dead-PID marker is removed at the next startup; a live one is kept |
| V15 | Read-only dotnet target untouched | H, E | Restore/build/test/run succeed; the target tree hash is unchanged; the receipt names `artifactsRoot` |
| V16 | Writable dotnet target unchanged | H | `bin`/`obj` in the project as today |
| V17 | Windows working-directory budget | U, I | A path of 270 gives typed `WorkingDirectoryTooLong` guidance, not a generic start failure |
| V18 | Stale `subst` sweep | U; E (Windows) | Only letters mapped to exactly the workspace root are removed |
| V19 | Receipts carry names only | U | No environment values in `receipt.json` or `request.json` |
| V20 | Diagnostics and API show scratch readiness | C, A, E | Mode, root, budget and reason are visible; Windows long-path state shown |
| V21 | Stage A: every start-failure kind has operator and agent text | U | Table completeness; agent text never contains a physical path |
| V22 | Stage A: a missing executable, missing folder or 270-character folder is explained on each surface | I, C, E | Runtime-node card, agent tool result (typed code in the MAF trace), process diagnostic `NextAction`, chat stream code |
| V23 | Stage A: the start cause is logged on the agent path | I | The log contains kind and path; the agent text does not |
| V24 | Stage B: `NetworkTrustNotPassed` hint | U, I | Host has `HTTPS_PROXY`, `NetworkTrust` off → receipt note and operator hint; on → no hint and the variable is passed |
| V25 | Stage D: the D8 per-agent option | U, C, I | Default off → denial with the settings hint; on → read-only access to the own run's leaf only, never another leaf; the setting survives save and reload when it is the only change (`IsDefault`) |
| V26 | Switch-offs | U, I | `ReadOnlyTargetDotnetRedirect=false` and `ManagedScratch=Disabled` restore today's behaviour exactly |
| V27 | Scratch never blocks startup | H, E | A broken explicit override → the host starts; the capabilities page shows an error-styled `Degraded` |

## End-to-end journeys (5032, Windows)

Restart 5032 on the new build first. Capture its stdout log (see the process-run diagnosis
memory). List every piece of project data a journey changes in the log, and revert test-only
nodes afterwards.

| J | Journey | Steps | Observe |
| --- | --- | --- | --- |
| J1 | Runtime node Direct run (Tetris3) | Double-click the .NET node → Run; open preview; Stop | The app serves; the node scratch `n-*` exists while running and is gone after Stop; the host temp gains no new build folders |
| J2 | Temp probe node | Add a PowerShell Script node `Write-Output $env:TEMP; Write-Output $env:OPENAI_API_KEY; exit 3` → Run | Operator guidance shows the last-exit output: `TEMP` = scratch leaf, and the key line is empty |
| J3 | Agent script in chat | Ask the agent to run a pwsh script that prints `$env:TEMP` and writes a temp file | The receipt (`artifacts/…/tool-runs/*/receipt.json`) shows the scratch mode and path; after the run ends the `e-*` leaf is gone |
| J4 | Agent dotnet build on a read-only external target | Select a node under a project block with an external output folder; ask for a build and test | Succeeds; the target tree hash before and after is identical; the receipt `artifactsRoot` is under scratch |
| J5 | Process run via API | `POST /api/processes/launch/check`, then `/launch` of a small definition with a script step; read history and receipts | Step receipts show the execution-run scratch; the leaf is removed after the run completes |
| J6 | Local MCP | Capability setup test for a local stdio MCP (UI), or an agent run with Playwright MCP | An `m-*` leaf exists during the session and is gone after it |
| J7 | Crash recovery | Start a keep-alive `dotnet run` through the agent; kill 5032; restart | The sweeper log removes the stale leaf; no stale `subst` letters remain for the workspace root |
| J8 | Terminal parity | Runtime node → Open terminal; `echo $env:OPENAI_API_KEY` | Empty; `PATH`/`TEMP` present |
| J9 | Misconfiguration | Restart 5032 with `ControlPlane__ProcessScratchRootPath` set to (a) an over-budget path and (b) an unwritable path; then remove it and simulate an unusable default (deny ACL) | The host always starts. Overrides show an error-styled `Degraded`, the default shows a warning; launches still work with the system temp and receipts say why |
| J10 | Readiness API | Call `GET /api/runtime/capabilities` and open `/settings/runtime-capabilities` | Process scratch entry with mode, root, budget and remediation |
| J11 | Per-agent temp read option (D8) | Agent dialog → Workspace tools → toggle "Read run temporary files"; ask the agent to read a temp file its script created | Off: a clear denial naming the setting. On: it reads its own run's file and is denied another run's file |
| J12 | **Full delivery process** (stage C and final) | Launch the `blazor-app-delivery` process on a fresh sample project (as in the Tetris3 run): build, test, run, publish and static serve against the external output folder | The run completes; review steps on read-only targets leave the product tree unchanged; receipts show redirects and scratch; no new files in the host temp |
| J13 | Explained failures (stage A) | Runtime node pointing at a missing exe; agent command in a missing folder; a node in a 270-character folder | Each surface shows the reason and remediation (V22) |

**Note on J9:** since D3 was revised, a broken override no longer blocks startup. Check V27
instead of a blocked start.

## Linux (Docker, `candoitall-maf122-validation:local`)

- Run the `[HostPlatform]` scratch classes (V4, V5, V6, V14, V15) and the focused Unit classes.
  Use the Linux Docker recipe from the CI memory: explicit network subnet and `--init`.
- Rerun `evidence/linux-tmpdir-pipe-probe.sh` with `TMPDIR` set to 150 characters **in the host
  environment**. Show that the allocator still produces a short scratch and that the pipe connects.
- Run with no `HOME` (`env -u HOME`): the composition supplies the toolchain home, and
  `dotnet --info` in a child succeeds.

## macOS (CI `tests-host` lane; cannot run locally)

Hand this checklist to the operator: trigger CI with `platform-scope: full`, or rely on the
`HostPlatform` split scope, then share the log zip.

- `ProcessScratchHostPlatformTests`: the root is `/private/tmp/candoitall-<u8>` at mode 0700; the
  pipe connects (V6); links are not followed (V5).
- `ApplicationStoragePortabilityContractTests` still passes (the `RuntimeTemporary` defaults are
  unchanged by D1).
- `WorkspaceDotnetReadOnlyTargetTests` passes.

## Regression lanes before closure

- **Unit:** the whole assembly.
- **Integration (focused):**
  - `ProjectStructureRuntime*`
  - `ProjectStructureAgent*`
  - `MafGenericToolAdmission*`
  - `WorkspaceCommandExecution*`
  - `LocalStdioMcp*`
  - `ProcessScratch*`
  - `WorkspaceDotnetReadOnlyTarget*`
  - `ExecutionFoundationPortabilityIntegrationTests`
  - `ProcessCapabilityPortabilityIntegrationTests`
- **Components (focused):** `ProjectStructurePage*`, `EmbeddedBrowserTests`, diagnostics.
- **Gates:** the portability static gate and the documentation check.
