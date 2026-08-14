# Agent prompt — B02 Workbench runtime nodes, terminal presentation, and elevation adapters

You are the senior C# architect and implementation agent for **CanDoItAll Runtime, Tools, and Process Drivers**.

## Objective

Replace the Windows/PowerShell runtime-node launcher with typed direct execution and optional platform presentation adapters.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `B02`.
- Verify HEAD and dirty state before edits.
- Use CodeAnalytics/solution analysis where available before broad changes.
- Add failing-first tests or named characterization evidence.
- Prefer existing owners and narrow ports; do not create a parallel framework.
- Preserve Windows behavior and existing data.
- Run focused and stable gates; use actual Windows/Linux/macOS hosts when required.
- Update bundle evidence and stop on every NO-GO.
- Keep all source-code comments in English.
- Do not commit, push, or open a PR unless explicitly instructed.

## Source hotspots

- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureDirectDotNetCommandPolicy.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs`

## Tasks

- **B02-T01 — Extract a pure runtime-node plan compiler:** Compile typed node metadata into executable, argv, environment, working directory, target, capability requirements, and display projection without starting a process.
- **B02-T02 — Use direct execution as primary authority:** Run .NET, Docker, Python, Node/Tailwind, and other typed environments through B01 primitives. Never invoke a shell merely to display a terminal.
- **B02-T03 — Make scripts explicit:** Model PowerShell and optional POSIX shell as distinct script kinds with executable dependency, policy, arguments, and side-effect/approval rules.
- **B02-T04 — Implement OS-specific Python layout:** Resolve `Scripts/python.exe` on Windows and `bin/python` on Unix/macOS; invoke directly. Capability-gate Conda and avoid activation as execution authority.
- **B02-T05 — Separate terminal presentation:** Create optional Workbench-owned adapters for Windows terminal/PowerShell, macOS terminal presentation, and configured Linux terminal. Headless execution does not require them.
- **B02-T06 — Separate elevation capability:** Retain governed Windows runas where intended; default Linux/macOS to unavailable. Add no implicit sudo/pkexec/osascript path.
- **B02-T07 — Migrate legacy command metadata carefully:** Recognize bounded static cmd/PowerShell wrappers for migration to typed fields. Dynamic/encoded/unknown shell content stays explicit and requires operator repair.
- **B02-T08 — Use core path authority:** Resolve project/script/environment paths through the Core C4 logical/physical contracts and preserve agent-selected versus operator-selected authority.
- **B02-T09 — Update capability-aware UX:** Replace PowerShell-only labels/messages with Run, Open terminal, Elevated launch, dependency missing, unsupported, headless, and foreign-path states.
- **B02-T10 — Run browser and actual-host proof:** Test direct run, optional terminal, script policy, Python layout, Docker plan, cancellation, and unavailable capability states.

## Exit

- Runtime-node plans are typed and shell-neutral.
- Direct headless execution works on Windows/Linux/macOS.
- Terminal and elevation are truthful optional capabilities.
- Legacy metadata has a bounded migration/repair path and UI proof.
