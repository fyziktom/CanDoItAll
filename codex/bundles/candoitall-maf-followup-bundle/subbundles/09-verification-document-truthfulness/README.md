# Subbundle 09 — Verification document truthfulness

## Problem

`docs/agent-runtime-hardening-verification.md` currently claims hardening-specific tests passed, but those test classes are not present in the uploaded repository ZIP.

## Required change

Make verification documentation auditable and reproducible.

## Rules

- Every test class named in docs must exist in the repository.
- Every command must include exact working directory, SDK version, and result.
- A timed-out repo-wide test must not be summarized as success.
- Existing failures may be listed as unrelated only if they are named precisely and the focused changed-surface proof is green.
- If focused filters are used, record discovered test counts. A filter with zero discovered tests is failure.
- If Codex cannot run a command, state exactly why.

## Suggested final doc sections

- Environment
- Restore result
- Build result
- Focused hardening tests result
- Integration tests result
- Repo-wide tests result
- Known unrelated failures
- Remaining risks
- Exact commit/snapshot identifier if available

## Static regression check

Add a simple test or script that verifies the verification document does not mention missing test class names. This catches the exact issue found in the uploaded ZIP.

## Status

Completed. Proof is recorded in `../../reviews/01-execution-report.md`.

## Requirements Owned

R06, R12.

## Prerequisites

All earlier subbundles that affect code, tests, or architecture claims must be completed or explicitly blocked with proof.

## Dependency Impact

Final closure gate for the full bundle.

## Validation Depth

Exact environment, restore, build, focused test, and integration test results must be recorded. The verification document must name only commands that actually ran and tests that exist.

## Progression Gate

The bundle may be closed only after verification documentation, execution report, and command results agree.
