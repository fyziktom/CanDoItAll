# Target Solution

## Data Flow

- Raw source packs stay under `codex/bundles/input`.
- `scripts/extract_project_sources.py` creates bundle-local extracted markdown/json artifacts.
- `source-truth/*-time-sliced.md` becomes the curated source-truth baseline.
- `validation/load-realistic-project-memory-validation.ps1` parses source-truth headings into project nodes and stage source chunks.
- CanDoItAll APIs create projects, nodes, links, external sources, ingestion runs, consolidation runs, review decisions, snapshots, and recall probes.
- `validation/analyze-realistic-project-memory-quality.ps1` compares recall output against `source-truth/source-manifest.json`.

## Boundaries

- Bundle data is allowed to contain normalized source facts.
- Application code must not embed the source facts.
- The API runner is validation infrastructure, not product code.
- C# repairs are downstream and evidence-gated.

## Project Structure Shape

- Project root
- Stage node such as `S03 - Development, Production Preparation, And Launch Sequence`
- Category node such as `Production Preparation`
- Fact/detail node such as `Tooling And Equipment`
- Stage source file node under the stage node
- `DerivedFrom` links from content nodes to the stage source file
- `DependsOn` links between sequential stage nodes
