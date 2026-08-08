# Agent prompt — B07 Runtime three-platform CI, E2E, and final closure

You are the senior C# architect and implementation agent for **CanDoItAll Runtime, Tools, and Process Drivers**.

## Objective

Prove runtime nodes, Manager, MCP, tools, plugins, and Processes on actual Windows/Linux/macOS hosts and close the full Unix portability program.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `B07`.
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

- `{{REPO_ROOT}}/.github/workflows-disabled/ci.yml`
- `{{REPO_ROOT}}/tests/Unit/CanDoItAll.Tests.Unit`
- `{{REPO_ROOT}}/tests/Integration/CanDoItAll.Tests.Integration`
- `{{REPO_ROOT}}/tests/Playwright/CanDoItAll.Tests.Playwright`
- `{{REPO_ROOT}}/tests/Playwright/CanDoItAll.Tests.Playwright/PlaywrightAppFixture.cs`

## Tasks

- **B07-T01 — Extend active CI runtime matrix:** Add focused actual-host jobs for process/executable/environment, Workbench runtime nodes, Manager, MCP/external tools, plugins, and process drivers on Windows, Ubuntu, and macOS.
- **B07-T02 — Run Workbench browser proof:** Capture capability-aware runtime actions, headless states, missing dependencies, foreign paths, terminal/elevation unavailability, and successful direct execution.
- **B07-T03 — Run MCP/external tool E2E:** Execute a deterministic local stdio MCP and governed external tool per claimed profile with approval, secret binding, workspace containment, timeout, invalid output, and cleanup.
- **B07-T04 — Run Manager lifecycle/recovery E2E:** Launch dotnet watch/Tailwind, restart Manager, reconcile registry/discovery, stop only owned processes, inject PID/metadata/watcher faults, and prove no leak/foreign kill.
- **B07-T05 — Run plugin/FileTools/Docker matrix:** Separate supported interactive/desktop/Docker profiles from headless/unavailable profiles and preserve truthful diagnostics.
- **B07-T06 — Run representative process-domain scenario:** Use a process with special tools and review/recovery. Prove success or exact missing-capability behavior, receipts, evidence, no authority regression, and no escalation loop.
- **B07-T07 — Run full Windows regression and core C4 recheck:** All core path/storage/security/headless gates remain green with runtime features enabled and disabled.
- **B07-T08 — Perform failure injection and security scan:** Cover child leaks, cancellation, secret output, executable substitution, path/symlink escape, missing native service, permission denial, cache corruption, and external dependency drift.
- **B07-T09 — Publish final support/limitation matrix:** Record exact OS/profile/RID/dependency versions, desktop/headless distinctions, known limitations, operator remediation, rollback, and evidence links.
- **B07-T10 — Issue Final Gate R4:** Only after all P0 requirements are Solved and independent architecture/security/runtime/QA/operations review is GO may the program be marked complete.

## Exit

- Final Gate R4 is GO with actual-host Windows/Ubuntu/macOS evidence.
- All runtime P0 requirements are Solved and no critical finding remains open.
- Core Gate C4 remains valid and Windows regression is green.
- Support/limitation, rollback, external dependency, and evidence manifests are complete.
