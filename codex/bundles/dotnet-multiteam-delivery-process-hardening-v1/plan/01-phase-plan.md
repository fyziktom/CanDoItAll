# Phase Plan

## Phase Sequence

1. Confirm source inventory and bundle readiness.
2. Add .NET architecture subprocess and update process/subprocess permissions.
3. Add runtime command and UI screenshot writeback subprocesses.
4. Validate templates, tests, raw-note closure, and app handoff without running the process.

## Subbundle Dependency Map

```mermaid
gantt
title .NET multi-team delivery hardening
dateFormat  YYYY-MM-DD
section Foundation
SB01 inventory and readiness :done, sb01, 2026-05-31, 1d
section Process contracts
SB02 architecture subprocess and permissions :after sb01, sb02, 1d
section Project structure writeback
SB03 runtime commands and screenshots :after sb02, sb03, 1d
section Closure
SB04 validation and handoff :after sb03, sb04, 1d
```

## Critical Subbundles

- SB02 is a critical foundation. If role permissions or subprocess references are wrong, all downstream process runs are untrustworthy.
- SB03 is a critical foundation. If writeback targets are vague, the live run can pass validation but fail the architect's evidence requirements.
- SB04 is process-critical closure. It must prove the template model and tests agree with the requested behavior.

## Phase Gates

- Gate after preparation: run `validate_bundle.py --stage prepared`.
- Gate before SB02: confirm no active existing bundle already owns this exact task.
- Gate after SB02: source assertions show architecture/review/classification/QA/writeback steps are non-mutating and subprocess references resolve.
- Gate after SB03: source assertions show `Run command`, `Run app`, `Run tests`, and `Screenshots` process-run writeback targets.
- Gate before closure: run targeted process-template tests, run a build or record exact blocker, and audit every raw note.
