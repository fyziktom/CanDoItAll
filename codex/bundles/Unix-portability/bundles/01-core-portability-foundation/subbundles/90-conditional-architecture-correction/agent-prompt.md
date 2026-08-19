# Agent prompt — A90 Conditional architecture correction

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Repair a foundational ownership, dependency, contract, or scope defect without smuggling correction into a later implementation subbundle.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A90`.
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

- `{{REPO_ROOT}}/CanDoItAll.slnx`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`

## Tasks

- **A90-T01 — Freeze downstream work:** Mark all dependent evidence invalid and stop later subbundles.
- **A90-T02 — Document the failed invariant:** Record exact source, dependency graph, reproduction, affected requirements, and why the current plan is unsafe.
- **A90-T03 — Choose the smallest owner-correct repair:** Prefer moving behavior to the existing owner or adding a narrow port over introducing a cross-cutting service.
- **A90-T04 — Add architecture characterization/failing tests:** Prove the defect and prevent recurrence.
- **A90-T05 — Implement and re-run the failed gate:** Update manifests/traceability and proceed only after independent GO.

## Exit

- The failed architecture invariant is restored.
- Dependent proof has been regenerated.
- The invoking gate records a new GO.
