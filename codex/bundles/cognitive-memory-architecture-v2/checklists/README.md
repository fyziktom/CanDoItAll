# Implementation Control Checklists

## Purpose

This folder contains the durable execution-control artifacts for the Cognitive Memory implementation.

The implementation is too long for a single agent context. The workbook is the phase ledger that must survive context compaction, handoff, and resumed work.

## Workbook

- `cognitive-memory-implementation-control.xlsx`

## Required Update Rules

- Update the workbook before starting a subbundle, while implementing it, and before closing it.
- Keep `reviews/01-execution-report.md` consistent with the workbook.
- Treat `subbundles/` as the authoritative subbundle folder. `plan/subbundles/` is a synchronized mirror and must not diverge.
- If a phase exposes a broken upstream assumption, mark the current row `Blocked`, reopen the upstream row, and stop downstream work.
- Do not mark a phase `Passed` without evidence paths for build/test/source review/browser proof required by that phase.

## Status Values

Use these values exactly:

- `Not Started`
- `Ready`
- `In Progress`
- `Blocked`
- `Passed`
- `Deferred`
- `Reopened`

## Closure Bar

A phase is closed only when:

- all owned checklist rows have a status,
- all required validation evidence paths are recorded,
- open risks have owner decisions,
- downstream dependency impact is recorded,
- the execution report and workbook agree.
