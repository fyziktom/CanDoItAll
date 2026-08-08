# Agent prompt — B00 Core handoff anchor, ownership review, and runtime inventory

You are the senior C# architect and implementation agent for **CanDoItAll Runtime, Tools, and Process Drivers**.

## Objective

Rebase the runtime plan to the exact core-portability commit and reapprove ownership before touching process/runtime code.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `B00`.
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

- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/architecture/15-exact-code-adaptation-inventory.md`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`
- `{{REPO_ROOT}}/src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs`

## Tasks

- **B00-T01 — Verify Core C4 handoff:** Confirm exact passing commit, active CI, support matrix, migrations, open limitations, and no uncommitted operator work. Stop if the handoff is incomplete.
- **B00-T02 — Rebase all runtime source references:** Compare prepared commit and Core C4 HEAD; update renamed files/projects, new process/tool surfaces, and requirement ownership.
- **B00-T03 — Generate full runtime execution inventory:** Find every ProcessStartInfo, Process, shell/script, terminal, executable resolver, environment binder, watcher, WMI/proc, MCP stdio, external tool, Docker/FileTools, and process-driver call path.
- **B00-T04 — Map authoritative ownership:** For each surface record plan compiler, execution primitive, lifecycle owner, process registry, cancellation/kill, capability probe, receipt/evidence, UI presentation, recovery, and domain failure owner.
- **B00-T05 — Characterize current behavior on all OSes:** Run existing process-host, Workbench, Manager, MCP, plugin, and process-driver tests; add no implementation yet. Capture process trees, output, environment, and failure modes.
- **B00-T06 — Review external package/native dependencies:** Record FileTools package, Docker, node/npm/npx, Playwright MCP, PowerShell, Python/Conda, terminal, WMI, procfs/libproc/ps, Keychain/Secret Service interactions, versions, and support evidence.
- **B00-T07 — Apply split triggers:** If implementation exceeds 60 production files, crosses more than 8 project ownership boundaries, needs external package source changes, or cannot preserve independent gates, create child execution bundles before edits.
- **B00-T08 — Issue Gate R0:** Require no unclassified P0/P1 runtime surface and approve the B01–B07 ownership/dependency graph.

## Exit

- Gate R0 is GO against the exact Core C4 commit.
- One owner exists for every runtime responsibility and no process-semantic rule is assigned to MAF/Infrastructure.
- Split triggers were evaluated and recorded.
- B01 is the only eligible implementation subbundle.
