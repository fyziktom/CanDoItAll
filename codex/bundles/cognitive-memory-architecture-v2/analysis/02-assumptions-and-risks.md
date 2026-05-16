# Assumptions And Risks

## Working Assumptions

- The first implementation slice will target a project-scoped Workbench memory path before process reflection, distributed workers, or cross-project promotion.
- Workbench Z coordinates remain metadata-backed until there is a proven need to migrate the Workbench schema to `PositionZ`.
- Existing RAG and SemanticCompletion repos can be referenced by the main solution or packaged without changing their ownership model.
- Low-risk generated canonical items can be machine-generated, but high-risk procedure, security, deployment, finance, legal, or destructive automation memory requires review.
- The current simple workspace memory remains as compatibility fallback while Cognitive Memory becomes the primary context provider later.

## Critical Path Risks

- If MAF context integration is hardwired into `MafAgentRuntime.Capabilities.Context.cs`, Cognitive Memory will leak durable-memory policy into the executive-control adapter and become hard to test or replace.
- If source ingestion reads EF tables ad hoc instead of through stable source snapshot adapters, later schema changes in Workbench, Processes, or Workflows will break memory silently.
- If Cognitive Memory adapters fail to consume the completed typed RAG filters, large projects will fall back into expensive post-filtering and recall quality will degrade under realistic data volume.
- If source hashes, projection hashes, and algorithm versions are not in the first model, consolidation and projection rebuilds will not be trustworthy.
- If the recall trace is weak, wrong-agent-context bugs will be nearly impossible to debug.
- If scoring remains local weighted sums, the system will tune recall, attention, belief, replay, probing, and answer confidence independently and create contradictory behavior that cannot be audited.
- If generated summaries are allowed to feed back as raw source truth, the system can produce circular hallucination.

## Validation Risks

- Small demo mindmaps can hide the production/test Docker separation problem; the golden dataset must include semantically similar but context-separated nodes.
- Unit tests can prove score geometry mechanics but miss bad source boundaries; integration tests must include Workbench ingestion, projection, recall, context-boundary shapes, and trace inspection.
- Qdrant unavailable cases must be tested early, otherwise the fallback path will be theoretical.
- UI proof must inspect review queue, memory detail, trace viewer, and consolidation run visibility, not only page load.
- Distributed worker validation cannot be accepted from happy-path job completion alone; wrong hashes, stale lease tokens, and incompatible algorithm versions must be rejected.

## Reopen Triggers

- A later phase needs Cognitive Memory to write into Workbench, Process, Workflow, or plugin tables directly.
- A MAF context provider implementation requires referencing `CanDoItAll.Modules.CognitiveMemory` from the durable MAF adapter instead of a narrow abstraction.
- Any projection point lacks a durable memory item id, source hash, projection version, embedding profile, or payload hash.
- Docker production and test/simulation knowledge are merged into one authoritative memory item.
- A high-risk procedure becomes active without source evidence or human review.
- Consolidation overwrites a human-validated canonical item automatically.
- Distributed worker outputs are accepted without input hash, output hash, lease, and algorithm/model version checks.
- Any downstream phase introduces behavior-affecting `FinalScore`, untyped `ScoreBreakdown`, scalar-only priority/confidence/weight, or local add/subtract scoring outside the score geometry driver.
