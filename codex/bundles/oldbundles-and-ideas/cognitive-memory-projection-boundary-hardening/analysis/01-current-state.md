# Current State

## Boundary Hardening Result

The implemented `cognitive-memory-boundary-hardening` bundle closed the high-risk CanDoItAll-side prerequisites:

- `MemorySourceSnapshotCursor` is source, scope, provider-version, position, and item-anchor aware.
- Workbench, Process runtime, and Workflow runtime source snapshots expose manifests, hashes, provenance, layout, references, links, permissions, and hash policies through common contracts.
- Process and Workflow runtime source providers page through ordered query-backed slices instead of mapping the whole source first.
- Workbench remains a bounded-source exception because `ProjectWorkbenchService.GetStructureAsync` still returns a complete project structure surface before provider paging.
- Workbench and runtime payloads now mark sensitive/restricted hash policy where raw or redacted payloads are involved.
- MAF context contribution has a generic contributor contract and retained trace collector.

Validation rerun during this review:

- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter AgentContextContributionTests --no-restore` passed: 7 tests.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~WorkbenchSourceSnapshotIntegrationTests|FullyQualifiedName~RuntimeEvidenceSourceIntegrationTests" --no-restore` passed: 3 tests.
- `python .\codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py .\codex\bundles\cognitive-memory-boundary-hardening --profile initiative --stage completed` passed.
- `python .\codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py .\codex\bundles\cognitive-memory-architecture --profile initiative --stage prepared` passed.

## Original Projection Gap

At preparation time, the remaining prerequisite was projection-side, not source-ingestion-side. The implementation closed this gap through typed RAG filters, payload indexes, delete-by-filter cleanup, and SemanticCompletion embedding profiles; see `reviews/01-execution-report.md`.

Preparation-time RAG driver state:

- `IRagDriver` supports ensure collection, upsert, delete by explicit ids, and search.
- `RagSearchRequest` supports collection, query text, optional vector, limit, and minimum score.
- `RagKnowledgeEntry` supports id, text, untyped metadata dictionary, tags, and optional vector.
- `RagDeleteRequest` deletes explicit knowledge ids only.
- `QdrantRagDriver.SearchAsync` calls Qdrant search without a filter.

Impact:

- Cognitive Memory cannot safely rely on projection-backed recall for project/user/security scoped searches without either driver-level filtering or collection partitioning.
- Post-filtering unscoped vector results in Cognitive Memory would be inefficient and can miss the best in-scope hits because Qdrant would rank across out-of-scope records first.
- Stale projection cleanup would require Cognitive Memory to track every projected point id and delete individually, instead of deleting by source scope, projection version, embedding profile, or source item key.
- Payload indexes are not modeled, so high-volume filtered search would be accidental and provider-specific.

Preparation-time SemanticCompletion state:

- `IAgentTextEmbeddingGenerator.GenerateAsync` returns `AgentTextEmbedding`.
- `AgentTextEmbedding` contains source text and vector only.
- `OnnxAgentTextEmbeddingOptions` contains model path, tokenizer path, token limits, threading, and normalization behavior, but no explicit stable profile id.

Impact:

- Cognitive Memory projection records need embedding model/profile/version in hashes, rebuild decisions, and recall traces.
- Keeping this metadata outside embedding results would force every adapter to re-derive profile identity differently.

## Decision

This follow-up bundle was the correct prerequisite before Cognitive Memory starts vector projection, projection-backed recall, cross-project recall, or strict-mode memory context injection.

Do not block Cognitive Memory module foundation or source snapshot ingestion on this bundle. Those phases can proceed against the hardened source contracts.
