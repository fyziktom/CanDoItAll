# Refactor Boundaries

## Recall

Split `Recall\CognitiveMemoryRecallServices.cs` only after tests lock behavior. Candidate boundaries:

- Recall request validation and normalization.
- Lexical candidate retrieval.
- Vector candidate retrieval.
- Graph/relationship expansion.
- Temporal replay contribution.
- Score aggregation and ordering.
- Context pack construction.
- Source coverage diagnostics.

## Advanced Services

Split `Advanced\CognitiveMemoryAdvancedServices.cs` around actual capabilities:

- Probe sessions and turn feedback.
- Self-regulation assessments.
- Answer gate decisions.
- Professor review.
- Epistemic drive.
- Cross-project promotion.
- Distributed worker/job coordination.

These should remain cohesive with shared helper types for review decisions, source coverage, and model execution metadata.

## Consolidation

Split consolidation by behavior:

- Source item selection.
- Chunk/candidate extraction.
- Model-assisted candidate generation.
- Deterministic policy checks.
- Candidate application.
- Review item creation.
- Run reporting and metrics.

The refactor must keep provenance and review semantics explicit.

## API

`CognitiveMemoryApi.cs` can be split into route group extension files or endpoint mapper classes:

- Status/database/settings.
- Ingestion/external sources.
- Snapshot/source/consolidation.
- Recall/probes/review.
- Advanced governance.
- Distributed jobs.

Route strings should be constants or typed route groups where the codebase pattern supports it. The split must not change public routes accidentally.

## Blazor UI

Split the large page into focused component wrappers using the project component style:

- Status and profile panel.
- Ingestion/source operations panel.
- Snapshot and consolidation panel.
- Recall/probe panel.
- Review queue panel.
- Advanced governance panel.
- Distributed jobs panel.

Keep non-trivial logic in services or typed view models rather than large markup blocks.

## Shared Helpers

Potential shared helpers must earn their existence by reducing duplication or improving testability:

- Source file kind classifier.
- Ingestion manifest validator.
- Secret/exclusion matcher.
- Text chunker and source span builder.
- Model execution profile selector.
- Token budget/truncation metadata builder.
- Review decision guard.
- Score vector component builder.
- API problem response builder.
