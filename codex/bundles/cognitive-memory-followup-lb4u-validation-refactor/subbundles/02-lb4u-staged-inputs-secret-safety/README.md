# 02 LB4U Staged Inputs Secret Safety

## Status

- Status: `Ready`

## Objective

Create and validate the LB4U staged ingestion manifest, translated source summaries, asset-node candidates, and secret exclusion checks.

## Covered Inputs

- LB4U business plan.
- LB4U product presentation.
- LB4U custom-button engineering presentation and PDF.
- LB4U procurement spreadsheets.
- LB4U asset folders and explicit secret exclusion.

## Prerequisites

- Subbundle 00 baseline must be complete.
- LB4U folder must be treated as read-only.
- `routery hesla` must be excluded before any file enumeration or ingestion plan is executed.

## Exact Source References

- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U
- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\LB4U-BP.docx
- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\2020-06-09-prezentace LB4U.pdf
- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\2020-06-09-prezentace LB4U.pptx
- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\LB4U Vývoj vlastního tlačítka.pdf
- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\LB4U Vývoj vlastního tlačítka.pptx
- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\Alza nabídka Brano 21.4.xlsx
- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\Alza nabídka Brano 27.4.xlsx
- C:\Users\lucys\OneDrive - TechnicInsider\Brano\LB4U\routery hesla

## Deliverables

- Typed LB4U staged ingestion manifest.
- Source extraction summaries in English.
- Asset-node classification list.
- Secret exclusion proof.
- Tests for manifest validation and exclusion matching.

## Dependency Impact

- Unblocks subbundle 04 extraction/consolidation and subbundles 08/09 validation.
- Must not write to LB4U source files.
- Must not include secret-file contents in any generated artifact.

## Validation Depth

- File-existence checks.
- Read-only extraction from allowed semantic sources.
- Manifest validation tests.
- Absence checks for excluded sources in operation manifests, logs, prompts, snapshots, and recall results.

## Implementation Steps

1. Build stage manifest using `inputs/03-lb4u-translated-stage-inputs.md`.
2. Add typed exclusion support if missing.
3. Extract compact summaries from allowed files.
4. Register asset-node candidates without semantic over-ingestion.
5. Add tests for excluded file handling.
6. Update workbook source and stage sheets.

## Do Not Do

- Do not open, copy, summarize, or ingest `routery hesla`.
- Do not bulk-ingest every binary file as text.
- Do not hardcode LB4U-only behavior into generic cognitive memory services.
- Do not overwrite source documents.

## Acceptance Checklist

- Manifest lists all stages and allowed sources.
- Excluded file is listed only as an exclusion.
- Extraction summaries are traceable to source files.
- Asset-node candidates are separated from semantic chunks.
- Tests prove exclusion behavior.

## Proof Required

- Manifest path and sample content.
- Test output for manifest and exclusion behavior.
- Workbook update.
- Execution report evidence.

## Browser Validation Logging

- Browser validation is not required unless a UI ingestion screen is changed.
- If UI is touched, capture before/after route evidence.

## Progression Gate

- Proceed to subbundle 04 only after the manifest and exclusion tests pass.

## Suggested Agent Prompt

Build the LB4U staged ingestion manifest and secret-safety tests. Keep the source folder read-only and never read the excluded password file.
