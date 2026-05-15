# Test and Quality Plan

## Test Taxonomy

| Test type | Purpose |
|---|---|
| Unit tests | scoring, hashing, relation detection, activation, mapping. |
| Integration tests | DB + source ingest + projection + recall. |
| Contract tests | RAG adapter, embedding adapter, MAF executor contracts. |
| Golden dataset tests | ensure stable recall/clustering on known mindmaps. |
| Negative tests | stale data, contradictions, secret redaction, bad merges. |
| UI tests | review queue, recall trace viewer, memory detail. |
| Workflow tests | recall/consolidation executors inside workflow runtime. |
| Distributed worker tests | job claim, output validation, tampering rejection. |

## Golden Mindmap Test

Create a small fixed project mindmap with:

- production Docker deployment node,
- testing Docker simulation node far away spatially,
- CI Docker test node,
- local development Docker Compose node,
- unrelated UI testing node,
- explicit links and parent branches.

Expected behavior:

- Docker nodes are semantically related.
- Production and testing Docker are context-separated.
- Deployment project summary includes both production and testing options.
- Procedure search for "test simulation" prioritizes test Docker.
- Production deployment recall does not use test-only configuration as authoritative.

## Source Hashing Tests

- Same source ingested twice does not create duplicates.
- Changed notes update source hash and enqueue reprocessing.
- Metadata-only change updates metadata hash if configured.
- Deleted source produces retire/supersede candidate, not immediate raw deletion.

## Activation Tests

Test score effects:

- human approval boosts retrieval,
- stale state penalizes retrieval,
- recent successful use boosts activation,
- contradiction penalizes context-pack selection,
- high semantic score alone cannot override access policy.

## Projection Tests

- Projection payload includes all required fields.
- Projection rebuild deletes old points for same memory item/profile.
- Qdrant outage falls back to lexical/graph recall.
- Filter by project/type/scope/tags works.
- Records with secret classification are not embedded.

## Recall Tests

- Recall trace contains all stages.
- Selected candidates include selection reasons.
- Context pack is token-limited.
- Context pack keeps source references.
- Recall for procedure intent prefers procedural memory over generic semantic topics.
- Recall for decision history includes episodic/decision records.

## Consolidation Tests

- Changed sources are detected.
- New canonical records are generated.
- Ambiguous merges create review tasks.
- Contradictions create contradiction candidates.
- Supersession updates stale records.
- Run report is persisted.

## Security Tests

- Secret-like values are redacted before embedding.
- Memory access policy denies unauthorized agent/user roles.
- External model context policy blocks restricted records.
- Prompt-injection-like source text is marked as untrusted source text, not instruction.

## Distributed Compute Tests

- Worker can claim only compatible jobs.
- Worker output with wrong input hash is rejected.
- Worker output with wrong algorithm version is rejected.
- Duplicate result submission is idempotent.
- Worker cannot directly apply memory mutations.

## Review Checklist Before Merge

- Build passes.
- Unit tests pass.
- Integration tests pass where environment is available.
- All new EF entities have configurations.
- All public contracts have stable names.
- No source comments are non-English.
- No generated memory item lacks source refs.
- No Qdrant point lacks payload metadata.
- No secret-like data is projected.
