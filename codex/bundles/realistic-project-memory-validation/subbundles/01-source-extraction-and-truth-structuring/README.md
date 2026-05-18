# Source Extraction And Truth Structuring

## Status

- Status: `Ready`

## Objective

- Extract both realistic source packs and produce normalized, time-sliced source-truth documents that can drive project-structure creation and memory validation.

## Success Criteria

- Both input project roots are inventoried.
- Extracted markdown/json views exist under `inputs/extracted`.
- AI Tap and Curacao source truth each contain at least five chronological `Sxx` groups.
- Financial, team-growth, facility/construction, CAPEX/OPEX, and risk facts are represented in source truth.

## Covered Inputs

- User request to analyze the two projects in `codex/bundles/input`.
- AI Tap business plan, budget/product-cost/water workbooks, XMind maps, GraphML/PPTX, and media metadata.
- Curacao government-submission business plan, executive summary, compendium, QA review, financial QA checklist, detailed model, and charts.

## Prerequisites

- Raw source folders exist under `C:\repositories\CanDoItAll\codex\bundles\input`.
- Workspace Python dependencies can read DOCX/PDF/XLSX/PPTX/XMind/GraphML formats.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\input\AI kohoutek
- C:\repositories\CanDoItAll\codex\bundles\input\Glass_Recycle_Curacao_Master_Pack_Government_Submission_Checked_2026-04-06
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\scripts\extract_project_sources.py
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\inputs\extracted
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\ai-tap-time-sliced.md
- C:\repositories\CanDoItAll\codex\bundles\realistic-project-memory-validation\source-truth\curacao-glass-time-sliced.md

## Deliverables

- Extracted source index and per-file markdown/json artifacts.
- Source-truth markdown for AI Tap.
- Source-truth markdown for Curacao glass recycling.
- Source-truth manifest with recall probes and required terms.
- Mindmap outline of the intended project structures.

## Dependency Impact

- API loading depends on the headings and stage IDs in source truth.
- Memory-quality analysis depends on required terms in the manifest.
- Weak source-truth normalization would invalidate every later API and recall result.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Run or verify source extraction.
2. Inspect extracted docs and workbooks for project facts.
3. Normalize facts into chronological source-truth groups.
4. Define recall probes and required source-truth terms.
5. Validate bundle readiness before API execution.

## Scope Exceptions

- Raw video content is represented by media metadata only; no video transcription is required for this validation.

## Do Not Do

- Do not add raw source data to app code.
- Do not flatten each raw file into one project node.
- Do not discard workbook financial data as secondary material.

## Acceptance Checklist

- `source-truth/source-manifest.json` lists both projects.
- Each source-truth markdown file contains `S01` through `S05`.
- Extracted source index exists.
- Prepared bundle validator passes.

## Proof Required

- Extractor output under `inputs/extracted`.
- Prepared bundle validator command and output.
- File paths to source-truth markdown and manifest.

## Browser Validation Logging

- N/A. No browser-visible or host-visible UI proof is required.

## Progression Gate

- Downstream API loading may start only after source-truth markdown and manifest are present and the prepared bundle validator passes.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Verify the extracted source artifacts, keep source truth bundle-local, preserve financial and operational details, and stop if the normalized source truth cannot be traced back to the input packs.
```
