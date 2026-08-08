# Agent prompt — A91 Conditional secret and key recovery

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Recover protected state safely after an interrupted, partially committed, or unreadable secret/key migration.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A91`.
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

- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`

## Tasks

- **A91-T01 — Stop all destructive cleanup:** Preserve old key rings, DPAPI payloads, vault generations, database backups, and migration journals.
- **A91-T02 — Classify committed generations:** Identify source/destination/provider/key IDs without logging values.
- **A91-T03 — Restore read capability on the source host:** Use the original authorized Windows/profile context where DPAPI or old key protection requires it.
- **A91-T04 — Resume or roll back transactionally:** Verify every record before pointer commit; clean orphans only after independent confirmation.
- **A91-T05 — Produce a redacted incident/recovery report:** Include root cause, affected records count, proof, residual risk, and prevention tests.

## Exit

- All expected records are readable or explicitly declared unrecoverable with evidence.
- No old generation was destroyed prematurely.
- Security Gate C2 is re-reviewed.
