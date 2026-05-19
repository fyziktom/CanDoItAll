# Current State

## Findings

1. The Cognitive Memory page already has a broad tab surface, but new quality-foundation capabilities are not first-class UI operations. Diagnostics, cluster planning, dream runs, aggregate candidate inspection, aggregate application, and synthesized recall/reference records are mostly invisible.
2. `CognitiveMemoryReviewUiQuery` has a single `Take` value and no per-collection paging metadata. This is not enough for an operator page where each tab can have many rows independently.
3. Several review UI query methods materialize rows with `ToListAsync` before sorting and taking. That violates the new requirement because large datasets would be loaded into memory.
4. The page renders all tab panels through one snapshot. That can remain acceptable only if every collection is bounded by page requests and the service applies `Skip`/`Take` before materialization.
5. Several tab components use `ColumnTemplateLg`, and the CSS contains media queries for smaller widths. The new requirement explicitly forbids medium/small tuning.
6. Existing tabs are functional but uneven: many panels repeat generic cards and do not show consistent page controls or total counts.

## Current Tab Coverage

- Dashboard: shows review queue, selected recall trace, projection health, and procedure panel.
- Probe workbench: supports session start/reuse, ask, voice, feedback, source refs, and trace stages.
- Settings: supports schedule/model/manual ingestion/operations actions.
- Sources: supports file and URL ingestion.
- Memory: supports list/detail over loaded memory records.
- Review queue: supports list/detail and review decisions.
- Recall traces: supports trace list/detail.
- Health: shows projection, consolidation, replay, procedure, and audit panels.
- Self-regulation: shows probe sessions, self-regulation, answer gates, professor reviews, and learning proposals.
- Scale: shows cross-project promotions and distributed jobs.

## Reopen Conditions

- Any list still loads all rows before paging.
- Any long list lacks visible paging controls.
- Any new quality operation remains inaccessible from `/cognitive-memory`.
- Any added CSS or layout work targets medium/small screens.
- Browser proof is missing for the large desktop viewport.
