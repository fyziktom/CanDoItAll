# Agent prompt — B91 Conditional external dependency quarantine and recovery

You are the senior C# architect and implementation agent for **CanDoItAll Runtime, Tools, and Process Drivers**.

## Objective

Contain an unsafe or unsupported FileTools, Docker, Node/Playwright, terminal, or native discovery dependency without weakening unrelated core/runtime behavior.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `B91`.
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

- `{{REPO_ROOT}}/src/Integration/CanDoItAll.FileTools.Integration/CanDoItAll.FileTools.Integration.csproj`
- `{{REPO_ROOT}}/src/plugins/Implementations/CanDoItAll.Plugin.Docker/DockerHostToolService.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs`

## Tasks

- **B91-T01 — Disable the affected capability truthfully:** Preserve core startup and unrelated features; show a bounded remediation state.
- **B91-T02 — Capture exact version/profile/reproduction:** Do not generalize one host failure to all Unix systems or claim support without evidence.
- **B91-T03 — Choose upgrade, adapter, replacement, or deferral:** Keep package-source changes separate when required and preserve rollback.
- **B91-T04 — Run compatibility/security matrix:** Prove path, process, secret, permission, cancellation, and cleanup behavior.
- **B91-T05 — Update support ledger and re-run gate:** Re-enable only the exact proven profile/version.

## Exit

- Unsafe dependency behavior is contained.
- Support claims match evidence.
- Affected gate is re-reviewed.
