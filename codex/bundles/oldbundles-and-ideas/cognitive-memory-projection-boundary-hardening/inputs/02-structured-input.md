# Structured Input

## Core Objective

- Prepare a follow-up prerequisite refactor bundle that makes projection-backed Cognitive Memory implementation safer by hardening the generic RAG and SemanticCompletion boundaries.

## Success Criteria

- The bundle explains why `cognitive-memory-boundary-hardening` is sufficient for source ingestion and MAF contributor boundaries.
- The bundle identifies the remaining projection-side risk: RAG search and cleanup lack typed filter/lifecycle contracts, and SemanticCompletion embeddings lack stable profile metadata.
- The bundle splits implementation into ordered, testable subbundles.
- The bundle includes source-grounded references, proof requirements, and architecture sync tasks.
- Prepared-stage bundle validation passes.

## Hard Constraints

- Do not implement Cognitive Memory.
- Do not implement product code while preparing this bundle.
- Do not add Cognitive Memory-specific names or semantics to generic RAG or SemanticCompletion repos.
- Do not rely on post-filtering unscoped vector search results as the safe design.
- Do not silently fall back to lower-quality embeddings or unfiltered search.

## Allowed Side Effects

- Create and edit planning artifacts under `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-projection-boundary-hardening`.
- During execution of this bundle, implementation agents may edit `C:\repositories\CanDoItAll.AgentFramework.Rag`, `C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion`, and architecture markdown in `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture`.

## Source Artifacts

- See `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- The implemented source boundary hardening must be respected and not repeated.
- The related RAG and SemanticCompletion repos must remain adapter/projection dependencies, not canonical memory stores.
- Future Cognitive Memory recall must be scoped, traceable, and rebuildable.

## Dependency And Sequencing Signals

- `01-rag-filter-and-payload-contracts` must land before lifecycle deletion by filter or source.
- RAG filter/lifecycle proof must land before Cognitive Memory uses projection-backed recall in strict or cross-project modes.
- Embedding profile metadata must land before projection hashes and rebuild decisions depend on SemanticCompletion embeddings.

## Validation Expectations

- RAG contract and mapper unit tests.
- Qdrant driver tests or mapper-level proof for filter translation and delete-by-filter when live Qdrant is unavailable.
- SemanticCompletion unit tests proving embedding profile metadata is stable and included in local hashing and ONNX result paths.
- Architecture sync proof in the Cognitive Memory bundle.

## Evidence Contract

- `dotnet test` for the RAG test project.
- `dotnet test` for the SemanticCompletion test project.
- Targeted test names recorded in `reviews/01-execution-report.md`.
- Prepared and completed bundle validation after implementation updates.

## UI Validation Strategy

- N/A. This bundle targets library contracts, providers, tests, and architecture docs only.

## Browser Validation Analytics

- N/A. `reviews/01-execution-report.md` still includes a browser analytics row recording that no browser-visible surface changed.

## Working Assumptions

- Cognitive Memory V1 can start module foundation and source ingestion using the already hardened CanDoItAll source contracts.
- Projection-backed recall should not start in production/strict mode until RAG supports provider-neutral filtering or an equally explicit safe scoping strategy.
- External Qdrant may not be available in every developer environment; mapper-level tests are mandatory and live integration proof is optional.

## Primary Risks

- Adding filters only to Qdrant would make the generic RAG abstraction leaky.
- Adding Cognitive Memory-specific payload fields directly to RAG models would couple a generic driver to one module.
- Keeping embedding profile information outside the embedding result would make projection rebuild and audit logic brittle.
