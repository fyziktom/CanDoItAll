# Agent prompt — A00 Anchor, baseline, and current portability inventory

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Re-anchor the supplied plan to the exact execution checkout and produce a complete, classified inventory before product code changes.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A00`.
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

- `{{REPO_ROOT}}/global.json`
- `{{REPO_ROOT}}/Directory.Build.props`
- `{{REPO_ROOT}}/CanDoItAll.slnx`
- `{{REPO_ROOT}}/.github/workflows-disabled/ci.yml`
- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/Program.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/WorkspaceRuntimeProcessTools.cs`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`

## Tasks

- **A00-T01 — Anchor and preserve the checkout:** Record branch, HEAD, merge base against prepared anchor, SDK, OS/architecture, git status, submodules, and every unrelated change. Stop rather than reset, clean, or overwrite operator work.
- **A00-T02 — Revalidate the source-reference manifest:** Verify every exact path, classify renamed/deleted files, add newly discovered portability surfaces, and update evidence status from Search-confirmed to Inspected where applicable.
- **A00-T03 — Run stable baseline and host characterization:** Run restore/build/stable tests on the available Windows host and on real Ubuntu/macOS runners or machines. Capture failures without making portability edits.
- **A00-T04 — Generate the full portability scan:** Run the supplied scanner, review every hit, and classify by logical path, physical path, filesystem, secret, process, desktop, hosting, test, or external dependency.
- **A00-T05 — Build the path-field inventory:** Map every persisted/configured/runtime string that can represent a route, logical locator, physical path, executable, URL, script, or opaque command. Record writer, reader, comparer, migration owner, and trust boundary.
- **A00-T06 — Build the persistence and migration inventory:** Map database columns, control-plane JSON, vault payloads, Data Protection key ring, storage tokens, runtime-node metadata, and host-bound preferences. Include backup/rollback and restart dependencies.
- **A00-T07 — Reconfirm architecture ownership:** Use the latest MAF refactor ADRs and project graph to approve owners for platform primitives, security, Workbench presentation, Manager supervision, MAF runtime, Plugins, and Processes semantics.
- **A00-T08 — Issue Gate C0:** No implementation starts until all P0/P1 findings are classified, the source anchor is current, baseline evidence is stored, and the revised work graph is internally consistent.

## Exit

- Gate C0 is GO with an exact current commit.
- No unclassified P0/P1 finding or unknown persisted path/key record remains.
- Baseline failures are distinguished from implementation regressions.
- The first eligible implementation subbundle is A01 only.
