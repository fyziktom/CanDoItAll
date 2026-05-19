# 02-validation-workbook-and-runbook

## Status

- `Completed`

## Objective

Create an XLSX checklist that keeps UI, API, storage, transfer, ingestion, dreaming, approval, probe, and trouble evidence synchronized with bundle execution.

## Covered Inputs

- REQ-05, REQ-08, REQ-09.

## Prerequisites

- Bundle root exists.
- Spreadsheet runtime is available.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-cluster-search-realistic-validation\README.md`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-cluster-search-realistic-validation\inputs\00-original-request.md`

## Deliverables

- `checklists/cognitive-memory-realistic-validation.xlsx`.
- Checklist tabs for scope, environment, transfer, ingestion, clustering/dreaming, approvals, probes, UI, tests, troubles, and follow-up architecture.

## Dependency Impact

- Later validation subbundles write their status and findings back to the workbook.

## Validation Depth

- Process-critical.

## Implementation Steps

1. Generate workbook using the bundled spreadsheet tooling.
2. Include IDs that match bundle requirements and subbundles.
3. Verify the workbook opens/loads through the artifact tooling.
4. Record the workbook path in the execution report.

## Do Not Do

- Do not track validation only in chat.
- Do not make the workbook the only proof; keep execution report rows synchronized.

## Acceptance Checklist

- Workbook exists.
- Workbook has the required validation sheets.
- Workbook rows map to subbundles and requirement IDs.
- Workbook can be loaded or inspected after generation.

## Proof Required

- Workbook artifact path.
- Verification output.

## Browser Validation Logging

- N/A.

## Progression Gate

- Long-running validation should not start until the workbook exists.

## Suggested Agent Prompt

```text
Create the XLSX validation workbook for subbundle 02 and verify it can be loaded. Keep row IDs aligned with the bundle requirements and execution report.
```
