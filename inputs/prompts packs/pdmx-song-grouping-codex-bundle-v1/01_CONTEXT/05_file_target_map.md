# File Target Map

This is the shortest path map for Codex. It is not exhaustive, but it is the most likely change surface.

## Existing files that almost certainly need edits

### Core persistence and model layer

- `src/App.PdmxTool/Data/WorkstationModels.cs`
  - add grouping profile / embedding / run / membership entities
  - extend `ScoreReview` grouping override fields if needed
  - preserve compatibility with existing `SongGroupId`

- `src/App.PdmxTool/Data/PdmxWorkstationDbContext.cs`
  - configure new tables, indexes, conversions, FK relations
  - keep provider compatibility

- `src/App.PdmxTool/Data/Migrations/*`
  - create one or more new migrations

### Services

- `src/App.PdmxTool/Services/WorkKeyNormalizer.cs`
  - likely replace or split into a richer normalization stack
  - preserve backward-compatible call sites until migration is complete

- `src/App.PdmxTool/Services/PdmxIndexingService.cs`
  - update normalization/profile generation strategy
  - avoid hidden regressions in existing indexing flow

- `src/App.PdmxTool/Services/PdmxGroupingService.cs`
  - replace destructive rebuild logic
  - introduce run-based, evidence-rich grouping flow

- `src/App.PdmxTool/Services/PdmxCatalogService.cs`
  - support multi-membership read models
  - group review filters
  - richer detail/group DTOs

- `src/App.PdmxTool/Services/ProcessingTaskService.cs`
  - add request fields and/or new task kinds
  - preserve queue-lane semantics

- `src/App.PdmxTool/Services/ProcessingTaskWorker.cs`
  - dispatch new grouping modes/stages safely

### UI

- `src/App.PdmxTool/Components/Pages/Home.razor`
  - add safe grouping actions:
    - rebuild missing profiles,
    - dry run,
    - apply run,
    - possibly review queue shortcut

- `src/App.PdmxTool/Components/Pages/Catalog.razor`
  - add grouping filters / badges

- `src/App.PdmxTool/Components/Pages/ScoreDetail.razor`
  - add grouping section / memberships / evidence / manual actions

- `src/App.PdmxTool/Components/Pages/Groups.razor`
  - add richer filtering / review views

- `src/App.PdmxTool/Components/Pages/GroupDetail.razor`
  - add canonical editing, evidence, manual merge/split flow

- `src/App.PdmxTool/Components/Layout/MainLayout.razor`
  - possibly add link to group review page if a new route is introduced

### Tests

- `tests/App.PdmxTool.Tests/*`
  - normalization, scoring, migration, rerun, override, false-positive tests

- `tests/App.PdmxTool.PlaywrightTests/*`
  - group review UI, group detail actions, score detail group explanation

## New files that likely should be created

### Services

- `src/App.PdmxTool/Services/Grouping/ScoreGroupingProfileService.cs`
- `src/App.PdmxTool/Services/Grouping/ScoreGroupingEmbeddingService.cs`
- `src/App.PdmxTool/Services/Grouping/SongGroupingCandidateService.cs`
- `src/App.PdmxTool/Services/Grouping/SongGroupingScoringService.cs`
- `src/App.PdmxTool/Services/Grouping/SongGroupingClusteringService.cs`
- `src/App.PdmxTool/Services/Grouping/SongGroupAdminService.cs`
- `src/App.PdmxTool/Services/Grouping/SongGroupingRunService.cs`
- `src/App.PdmxTool/Services/Grouping/ComposerAliasNormalizer.cs`
- `src/App.PdmxTool/Services/Grouping/WorkTitleCanonicalizer.cs`

### UI components

- `src/App.PdmxTool/Components/Grouping/GroupMembershipPanel.razor`
- `src/App.PdmxTool/Components/Grouping/GroupingEvidencePanel.razor`
- `src/App.PdmxTool/Components/Grouping/GroupReviewTable.razor`
- `src/App.PdmxTool/Components/Grouping/GroupEditorDialog.razor`
- `src/App.PdmxTool/Components/Grouping/GroupMergeDialog.razor`
- `src/App.PdmxTool/Components/Grouping/GroupSplitDialog.razor`

### Models / DTOs

- `src/App.PdmxTool/Models/Grouping/*`

### Tests

- `tests/App.PdmxTool.Tests/Grouping/*`
- `tests/App.PdmxTool.PlaywrightTests/Grouping/*`

## Rule for Codex

Create new files in a **grouping-focused feature namespace/folder** instead of making existing broad service files even larger.
