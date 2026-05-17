# Staged demo corpus and trace workbook

## Status

- `Completed`

## Objective

- Verify and maintain the staged source corpus and XLSX tracker that all later memory-cycle observations will use.

## Success Criteria

- 24 staged Markdown source files exist.
- `source-manifest.json` lists every staged source file.
- `cognitive-memory-demo-source-tracker.xlsx` opens and maps every source file to expected memory behavior.
- The workbook has Source Manifest, Cycle Plan, Chat Probes, Memory Analysis, and Repair Log sheets.

## Covered Inputs

- R2 staged detailed demo corpus.
- R3 XLSX source traceability.
- R9 on-the-fly repair subbundle tracking.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\staged-sources`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\source-manifest.json`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\trackers\cognitive-memory-demo-source-tracker.xlsx`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\scripts\build-demo-corpus.mjs`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\validation\scripts\verify-demo-tracker.mjs`

## Deliverables

- Confirmed staged source corpus.
- Confirmed XLSX tracker.
- Source-count and workbook-verification evidence.

## Dependency Impact

- Later stage loading, duplicate analysis, source-reference checks, and chat scoring all depend on this tracker. If any source is missing or misclassified, downstream conclusions about memory quality are invalid.

## Validation Depth

- Critical data foundation.

## Implementation Steps

1. Inspect `source-manifest.json` and confirm 24 source rows.
2. Inspect stage folders and confirm six project files per stage.
3. Verify the XLSX workbook can be opened and inspected.
4. If any source is missing, repair the corpus and regenerate the workbook.
5. Record proof in `reviews/01-execution-report.md`.

## Scope Exceptions

- This phase does not load data into Cognitive Memory.

## Do Not Do

- Do not put staged source data into automated test code.
- Do not reduce the staged corpus to fewer than four waves.
- Do not remove the XLSX tracker; it is a hard requirement.

## Acceptance Checklist

- Completed: Manifest includes all source files.
- Completed: Workbook includes all manifest rows.
- Completed: Stage 04 includes email/instruction-style Markdown assets.
- Completed: Verification evidence is recorded.

## Proof Required

- Run `node validation\scripts\verify-demo-tracker.mjs` with the bundled workspace Node runtime.
- Record tracker path, source count, and verification output.

## Browser Validation Logging

- N/A. This subbundle prepares data artifacts and does not affect a browser-visible UI.

## Progression Gate

- Downstream API loading may start only after the tracker opens cleanly and every staged source file appears in the workbook.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Verify the staged source files and XLSX tracker before loading anything into Cognitive Memory. Record source count, workbook verification, and any repairs in reviews/01-execution-report.md. Stop if the tracker cannot be opened or if any staged file is missing from the workbook.
```
