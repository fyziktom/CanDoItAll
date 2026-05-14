# Phase Plan

## Phase Sequence

1. Prepare and validate the focused feedback bundle.
2. Execute `01-oauth-connection-defaults`.
3. Execute `02-generic-project-storage-skip-preview`.
4. Run targeted tests and a browser pass on the Project Structure start dialog.
5. Close raw notes and run the completed-stage validator.

## Subbundle Dependency Map

```mermaid
gantt
title OAuth Email Workflow Defaults
dateFormat  YYYY-MM-DD
section Foundation
01 OAuth connection defaults :crit, s1, 2026-05-14, 1d
section UI and workflow start
02 Generic project storage skip preview :crit, s2, after s1, 1d
section Closure
Final validation and evidence sync :s3, after s2, 1d
```

## Critical Subbundles

- `01-oauth-connection-defaults` is a critical foundation because email workflows cannot reach summary or storage steps while blank connection ids fail early.
- `02-generic-project-storage-skip-preview` is a critical UI/runtime foundation because the start dialog, start API contract, simulation plan, and project-structure executor context fallback must agree.

## Phase Gates

- Prepared gate: bundle validator and `validate_bundle.py --stage prepared` pass or all failures are repaired.
- `01` entry gate: source references exist and no prerequisite implementation is required.
- `01` closure gate: blank `connectionId` resolves only to an enabled connected OAuth connection; invalid explicit ids still fail.
- `02` entry gate: `01` closure proof is complete; workflow start dialog and service source references are current.
- `02` closure gate: Project Structure start dialog exposes generic project-structure write skip options and selected skips are passed into runtime.
- Final closure gate: targeted tests, build, browser proof, and raw-note closure rows agree.
