# PDMX Song Grouping Codex Bundle v1

This bundle is a **complete implementation-preparation package** for adding high-quality song/work grouping to `src/App.PdmxTool`.

It was prepared from a static audit of the uploaded repository and an external design review of proven record-linkage and music-metadata practices. The intent is to give Codex enough structure that it can implement the feature **incrementally, safely, and without getting lost**.

## What this bundle assumes

- The authoritative workstation app is `src/App.PdmxTool`.
- There is already a local DB in the real environment with 200k+ indexed songs.
- Codex may use real indexed data for validation, **but must never modify the original production-like DB file**.
- Ollama is available locally. If an embedding model is missing, Codex may pull it and wait for the pull to complete.
- The uploaded zip did **not** include the real DB file, so this package includes a **read-only / copy-first validation protocol** rather than direct findings from that DB.

## Top conclusions

1. The current workstation already has strong bones:
   - durable task queue,
   - resumable indexing,
   - existing grouping route/page,
   - review workflows,
   - Ollama integration,
   - harmonic enrichment,
   - unit and Playwright tests.

2. The current grouping implementation is **not sufficient** for 200k+ work-level deduplication:
   - one exact `WorkKey` only,
   - destructive rebuild of all groups,
   - one-group-per-song only,
   - no confidence model,
   - no rationale storage,
   - no dry run,
   - no review queue,
   - no embedding support,
   - no safe rerun story.

3. The right implementation is **not** “just compute embeddings and cluster everything”.
   - First normalize.
   - Then block / candidate-generate.
   - Then score with deterministic rules.
   - Then use embeddings as a strengthening or expansion signal.
   - Then cluster conservatively.
   - Then expose ambiguous cases to human review.

4. The current architecture should evolve toward:
   - `ScoreGroupingProfile` for normalized/extracted grouping metadata,
   - `ScoreEmbeddingVector` for persisted embeddings,
   - `SongGroupMembership` as canonical many-to-many truth,
   - `SongGroupingRun` + preview/run tables for safe dry-run review,
   - derived compatibility projection to `group:XYZ` tags rather than tags being canonical truth.

5. Phase 1 should preserve compatibility:
   - keep `IndexedScore.SongGroupId` as cached primary-group pointer,
   - keep existing pages/routes working,
   - add deeper grouping functionality without forcing a wide rewrite.

## Bundle structure

- `01_CONTEXT`
  - repository audit
  - current limitations
  - successful patterns from practice
  - runtime constraints
  - target-file map
- `02_DESIGN`
  - architecture
  - data model
  - normalization strategy
  - abbreviation catalog
  - candidate generation + embeddings
  - scoring/confidence/audit
  - UI/UX plan
  - ASCII layout sketches
  - pipeline rollout
  - performance/storage
  - real-DB evaluation strategy
- `03_CODEX_PROMPTS`
  - sequenced prompts for the implementation agent
- `04_TESTS`
  - test matrix
  - golden dataset recipe
  - read-only real-DB evaluation plan
  - false-positive / false-negative audit workflow
- `05_VALIDATION`
  - validator-agent protocol
  - validation checklists
  - smoke checks
  - post-implementation phase prompt
- `06_PATCH_GUIDES`
  - entity sketches
  - service contract sketches
  - evidence JSON sketches

## Audit limitations

This package is based on:
- static repository inspection,
- existing tests and docs,
- external design research.

It is **not** based on:
- executing `dotnet build`,
- running migrations,
- loading the real 200k+ DB,
- benchmarking the actual Ollama embedding throughput on the target machine.

Those runtime steps are deliberately pushed into the Codex and validator workflows in this bundle.

## Recommended execution order for Codex

1. Read this README.
2. Read `01_CONTEXT/01_repository_audit.md`.
3. Read `02_DESIGN/01_target_architecture.md`.
4. Read `02_DESIGN/02_data_model.md`.
5. Read `02_DESIGN/03_normalization_strategy.md`.
6. Read `02_DESIGN/05_candidate_generation_and_embeddings.md`.
7. Then execute prompts in `03_CODEX_PROMPTS` in numeric order.
