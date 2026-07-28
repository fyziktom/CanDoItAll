# SB03 Proof Workspace

## Purpose

This directory is reserved for evidence produced while executing **Approval binding and state migration**.

## Rules

- Do not fabricate evidence.
- Record exact repository SHA, commands, exit codes, timestamps, and relevant environment details.
- Store failing-first and passing proof separately.
- Hash cross-version fixtures and any persisted-state payloads.
- Redact secrets and provider credentials.
- Update `reviews/01-execution-report.md` with links to the final evidence.

## Final Evidence

- `final-validation.md` records the exact focused unit command/result and the
  live approval persistence, rejection, and non-mutation proof.
