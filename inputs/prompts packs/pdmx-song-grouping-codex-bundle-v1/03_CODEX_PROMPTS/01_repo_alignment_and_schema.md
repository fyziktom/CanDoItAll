# Prompt 01 — Repository Alignment And Safe Schema Plan

## Objective

Align the implementation with the actual current repo state and prepare the schema changes without overbuilding.

## Tasks

1. Re-audit the actual files in `src/App.PdmxTool` before editing.
2. Confirm the current grouping baseline:
   - `WorkKeyNormalizer`
   - `PdmxGroupingService`
   - `SongGroup`
   - `IndexedScore.SongGroupId`
   - existing UI pages and tests
3. Implement the schema additions needed for:
   - `ScoreGroupingProfile`
   - `ScoreEmbeddingVector`
   - `SongGroupMembership`
   - `SongGroupingRun`
   - run preview tables
4. Preserve compatibility with existing `SongGroupId` by treating it as cached primary-group pointer.
5. Add or extend enums as needed for:
   - group type
   - membership role
   - confidence band
   - run kind/status
   - grouping lock mode
6. Create migration(s).

## Boundaries

- Do not implement full grouping logic yet.
- Do not remove current UI behavior yet.
- Do not break existing tests unnecessarily.
- Do not delete `SongGroupId`.

## Expected outputs

- updated entities
- updated `DbContext`
- migration files
- any lightweight compatibility model changes needed in services/tests

## Likely files

- `src/App.PdmxTool/Data/WorkstationModels.cs`
- `src/App.PdmxTool/Data/PdmxWorkstationDbContext.cs`
- `src/App.PdmxTool/Data/Migrations/*`

## Required tests

- migration/model smoke test
- compatibility test proving existing group summary path still works structurally
- one DB test confirming membership rows and primary-group cache can coexist

## Review checklist

- [ ] `SongGroupId` preserved for compatibility
- [ ] many-to-many membership introduced
- [ ] run tables exist
- [ ] no canonical logic stored only in tags
- [ ] schema supports dry-run preview
