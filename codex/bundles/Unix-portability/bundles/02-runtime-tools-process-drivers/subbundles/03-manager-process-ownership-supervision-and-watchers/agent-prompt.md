# Agent prompt — B03 Manager process ownership, supervision, and watchers

You are the senior C# architect and implementation agent for **CanDoItAll Runtime, Tools, and Process Drivers**.

## Objective

Make Manager recovery and supervision safe on Windows, Linux, and macOS without name-only termination or watcher assumptions.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `B03`.
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

- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/CanDoItAll.Manager.csproj`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/WatchSupervisorService.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/TailwindWatchSupervisorService.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/TuningExecutionAdapter.cs`

## Tasks

- **B03-T01 — Create an owned launched-process registry:** Persist non-secret PID/start identity/executable/argv hash/workspace/user/parent/lease state for Manager-launched processes and reconcile it on restart.
- **B03-T02 — Isolate Windows WMI:** Move ManagementObjectSearcher and System.Management calls into a Windows leaf adapter selected by composition; neutral Manager contracts do not reference WMI.
- **B03-T03 — Implement Linux recovery discovery:** Use `/proc` or another bounded mechanism to obtain start time, owner, executable, command, and parent where permitted. Missing evidence must not authorize termination.
- **B03-T04 — Implement macOS recovery discovery:** Use a tested bounded adapter (native API or strictly parsed command fixture) with locale/permission/race handling and safe fallback.
- **B03-T05 — Strengthen ownership proof:** Require PID plus start identity and additional registry/user/executable/command/workspace evidence. Reject name-only, substring-only, or ambiguous matches.
- **B03-T06 — Unify launch/stop lifecycle:** Use B01 execution primitives for dotnet watch, Tailwind/npm, tuning, and related processes; implement graceful stop then bounded force kill.
- **B03-T07 — Make watcher pipelines convergent:** Reuse Core watcher hint/rescan principles, generation/fingerprint output, duplicate suppression, overflow recovery, and polling fallback without merging domain-specific supervisor logic.
- **B03-T08 — Fix path/comparer semantics:** Remove global case-insensitive path sets and use Core C4 logical/physical rules for project references, roots, output, ignored segments, and process signatures.
- **B03-T09 — Inject process and watcher failures:** Test PID reuse, process exit race, unreadable command line, permission denied, orphan registry, duplicate starts, watcher overflow, rename storm, shutdown interruption, and output convergence.
- **B03-T10 — Issue Gate R2:** No later MCP/plugin/process adaptation begins until process ownership and Manager lifecycle are independently approved.

## Exit

- Gate R2 is GO.
- No process is killed using name-only or ambiguous evidence.
- Windows/Linux/macOS recovery adapters and primary launched-process registry are proven.
- Supervisor/watcher pipelines converge after faults and shutdown.
