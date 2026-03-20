# Repository Audit

## Scope of the audit

I audited the uploaded repository as static source only. The most relevant project for this feature is:

- `src/App.PdmxTool`

Related validation/test surface already present:

- `tests/App.PdmxTool.Tests`
- `tests/App.PdmxTool.PlaywrightTests`
- `docs/pdmx-workstation/*`
- existing handoff example:
  - `pdmx-harmonic-analysis-codex-bundle-v1`

## Solution shape

The workstation is already a serious internal app, not a toy prototype.

### Relevant strengths already present

- Razor/Blazor Server UI with internal-curation workflows.
- EF Core persistence with:
  - SQLite,
  - PostgreSQL,
  - InMemory test provider.
- Durable background tasks via:
  - `ProcessingTask`
  - `ProcessingTaskService`
  - `ProcessingTaskWorker`
- Existing long-running enrichment pattern:
  - indexing,
  - harmonic analysis,
  - Ollama metadata suggestion.
- Existing score detail page and grouped catalog browsing.
- Existing test fixtures and Playwright harness.

## Key files and what they mean

### App bootstrap and registration

- `src/App.PdmxTool/Program.cs`
  - registers DB, catalog/admin services, indexing, grouping, Ollama, harmony, hosted background worker.

- `src/App.PdmxTool/Services/PdmxWorkstationDatabaseRegistration.cs`
  - supports `Sqlite`, `PostgreSql`, `InMemory`.
  - important for rollout planning: the feature must not hard-lock itself to one provider.

- `src/App.PdmxTool/appsettings.json`
  - default provider is SQLite.
  - root path points to local PDMX data.
  - Ollama is already configured.

### Persistence model

- `src/App.PdmxTool/Data/WorkstationModels.cs`
  - `IndexedScore`
  - `ScoreReview`
  - `SongGroup`
  - `OllamaSuggestion`
  - `ScoreHarmonicAnalysis`
  - `ProcessingTask`
  - settings and Ollama Lab entities

- `src/App.PdmxTool/Data/PdmxWorkstationDbContext.cs`
  - current `IndexedScore -> SongGroup` relation is many scores to one group via `SongGroupId`.
  - this is the central structural limitation for multi-group support.
  - `SaveChanges` normalizes max length and UTC timestamps automatically.

### Current normalization and grouping

- `src/App.PdmxTool/Services/WorkKeyNormalizer.cs`
  - currently very simple token normalization.
  - no diacritics strategy.
  - title/composer use the same logic.
  - no structured extraction for catalog numbers, keys, movement numbers, aliases, or composer surname handling.

- `src/App.PdmxTool/Services/PdmxGroupingService.cs`
  - groups by exact `ManualGroupKey` or `WorkKey`.
  - rebuilds all groups destructively.
  - nulls all `SongGroupId`.
  - deletes all `SongGroups`.
  - creates groups again from scratch.
  - chooses a representative member by review/rating/title.

This is acceptable for a small deterministic sample, but not acceptable for a real 200k+ curation system.

### Indexing and search

- `src/App.PdmxTool/Services/PdmxIndexingService.cs`
  - upserts rows from `PDMX.csv`.
  - computes `NormalizedTitleKey`, `NormalizedComposerKey`, `WorkKey`.
  - queues downstream enrichment.
  - already has resumable cursor handling and batch-save checkpoints.

- `src/App.PdmxTool/Services/PdmxCatalogService.cs`
  - catalog search already queries normalized fields and source text.
  - group-related view models exist.
  - detail loading already includes review, suggestion, harmony, and group.

### UI surfaces

- `src/App.PdmxTool/Components/Layout/MainLayout.razor`
  - nav already includes `Groups`.

- `src/App.PdmxTool/Components/Pages/Home.razor`
  - maintenance already has `Queue grouping`.

- `src/App.PdmxTool/Components/Pages/Catalog.razor`
  - grouped filter exists.
  - status column shows current group title.

- `src/App.PdmxTool/Components/Pages/Groups.razor`
  - currently simple list of groups.

- `src/App.PdmxTool/Components/Pages/GroupDetail.razor`
  - currently simple member list.

- `src/App.PdmxTool/Components/Pages/ScoreDetail.razor`
  - currently shows one group chip.
  - review tab includes `Manual group key`.
  - no multi-group UI, no evidence UI, no split/merge, no candidate review.

### Existing tasks and lanes

Current task kinds:
- `IndexSubsetSample`
- `ProcessSubsetPipeline`
- `GenerateSongGroups`
- `GenerateOllamaSuggestions`
- `GenerateHarmonicAnalysis`

Current queue lanes:
- `Indexing`
- `Harmony`
- `Ollama`

This matters because new grouping work should reuse the durable task system instead of creating hidden ad hoc background jobs.

### Tests already present

Unit/integration:
- `tests/App.PdmxTool.Tests/WorkKeyNormalizerTests.cs`
- `tests/App.PdmxTool.Tests/IndexingWorkflowTests.cs`

Playwright:
- `tests/App.PdmxTool.PlaywrightTests/WorkstationUiTests.cs`

Important current behavior captured in tests:
- two Moonlight rows index into one group,
- group page displays that duplicate cluster,
- score review and preview flows already work.

## Architectural reading of the current system

### What is already good enough to reuse

- DB provider abstraction
- one-to-one enrichment pattern (`OllamaSuggestion`, `ScoreHarmonicAnalysis`)
- durable tasks
- sample dataset fixture pattern
- Playwright end-to-end harness
- existing curation UX language and layout primitives

### What must change for real grouping

- grouping must become **non-destructive**.
- grouping must support **many-to-many memberships**.
- grouping must distinguish:
  - authoritative manual override,
  - automatic high-confidence assignment,
  - low-confidence review candidate.
- normalization must become **domain-specific**.
- grouping must store **why** something matched.
- grouping must support **dry runs** and **incremental reruns**.
- grouping must not rely on exact `WorkKey` only.
