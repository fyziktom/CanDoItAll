# Agent prompt — B90 Conditional runtime architecture correction

You are the senior C# architect and implementation agent for **CanDoItAll Runtime, Tools, and Process Drivers**.

## Objective

Repair duplicated execution, lifecycle, capability, or process-semantic ownership before downstream integration continues.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `B90`.
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

- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`
- `{{REPO_ROOT}}/src/Processes`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`

## Tasks

- **B90-T01 — Freeze dependent runtime work:** Invalidate downstream evidence and stop implementation.
- **B90-T02 — Map the duplicate or wrong owner:** Trace plan, execution, lifecycle, capability, receipt, recovery, and domain semantics.
- **B90-T03 — Select the existing authoritative owner:** Consolidate through a narrow port/adapter; do not add another facade over duplicates.
- **B90-T04 — Add architecture and lifecycle regression tests:** Prove one host, one registry, one owner, and preserved Processes/MAF boundaries.
- **B90-T05 — Re-run the invoking gate and refresh traceability:** Proceed only after independent GO.

## Exit

- Runtime ownership is unambiguous.
- Duplicate process/capability stacks are removed.
- The invoking gate is GO.
