# SB08 Proof Workspace

## Purpose

This directory is reserved for evidence produced while executing **Workaround cleanup, rollout, and closure**.

## Rules

- Do not fabricate evidence.
- Record exact repository SHA, commands, exit codes, timestamps, and relevant environment details.
- Store failing-first and passing proof separately.
- Hash cross-version fixtures and any persisted-state payloads.
- Redact secrets and provider credentials.
- Update `reviews/01-execution-report.md` with links to the final evidence.

## Final Evidence

- `final-validation.md` records package alignment, clean rebuild, focused tests,
  entry-surface UI checks, hosting parity, managed runtime identity, and residual
  risks.
