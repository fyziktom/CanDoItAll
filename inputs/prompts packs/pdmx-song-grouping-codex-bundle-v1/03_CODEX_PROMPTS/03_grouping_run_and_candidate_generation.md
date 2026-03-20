# Prompt 03 — Grouping Run Engine And Candidate Generation

## Objective

Implement run-based grouping preparation and candidate generation with blocking rules.

## Tasks

1. Expand `GenerateSongGroupsTaskRequest` or equivalent request model with explicit modes.
2. Implement `SongGroupingRun` lifecycle:
   - create
   - progress update
   - complete/fail
3. Implement candidate block generation using grouping profiles.
4. Add block-size guardrails and sub-block fallback behavior.
5. Emit run-preview rows (`SongGroupingRunGroup` / `SongGroupingRunMember`) without applying canonical groups yet.
6. Ensure checkpoint/resume support.

## Boundaries

- Do not apply canonical groups yet except if needed for small compatibility smoke paths.
- Do not implement embeddings as the only candidate source.
- Do not reintroduce destructive group rebuild.

## Expected outputs

- run orchestration
- candidate generator
- dry-run preview persistence
- task progress reporting
- UI-independent service-level tests

## Required tests

- run creation and completion
- resume/cancel behavior
- block-size guardrail behavior
- deterministic candidate generation on fixed input
- dry-run preview rows populated

## Review checklist

- [ ] dry run does not mutate canonical groups
- [ ] candidate generation is not all-vs-all
- [ ] oversize blocks are handled
- [ ] run preview rows are queryable for review UI
