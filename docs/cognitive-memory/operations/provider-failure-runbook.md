# Provider Failure Runbook

## Deterministic Local Proof

P1 adds unit proof for projection-provider failure without requiring Qdrant locally:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryOperationalServicesTests.ProjectionRebuildService_RecordsProviderFailureAndKeepsProjectionRebuildable" --logger "console;verbosity=minimal" -m:1
```

Expected result:

- projection rebuild returns `Blocked`;
- the selected projection is marked `Failed`;
- `RebuildRequired` remains true;
- `StaleReason` is `PreviousFailure`;
- the run stores `ProjectionRebuildFailures`.

## Live Docker Qdrant Validation

P1 beta was closed against the local Docker profile on 2026-05-19. The proof is stored at `codex/bundles/cognitive-memory-beta-qdrant-validation/reviews/runtime-proof/qdrant-beta-live-proof.json`.

Observed beta proof:

- Docker containers: `candoitall-qdrant` healthy on ports `6333-6334`, `candoitall-postgres` healthy on `5432`.
- API profile: PostgreSQL profile `127.0.0.1:5432/candoitall_cognitive_memory_multicycle_20260517_03`.
- External source upload: 2 source chunks and evidence anchors ingested through `/api/cognitive-memory/v1/external-sources/files`.
- Consolidation: 2 source items scanned, 2 candidates created, 2 mutation commands submitted, 0 review backlog.
- Projection rebuild: `selectedCount=2`, `projectedCount=2`, `failedCount=0`, `skippedCount=0`.
- Qdrant collection: `candoitall-knowledge`, status `green`, vector size `384`, distance `Cosine`.
- Public recall: selected 2 source-backed candidates and completed vector stage `rag:qdrant:search:2`.

Run this in an environment with Docker Qdrant mapped on `127.0.0.1:6333/6334`, a configured PostgreSQL Cognitive Memory profile, and the runtime Qdrant settings enabled.

1. Start the web app with the intended profile active.
2. Confirm API access:

```powershell
Invoke-RestMethod http://127.0.0.1:5289/api/access/status
```

3. Inspect contract and active profile:

```powershell
Invoke-RestMethod http://127.0.0.1:5289/api/cognitive-memory/v1/status
Invoke-RestMethod http://127.0.0.1:5289/api/cognitive-memory/v1/contract
```

4. Trigger rebuild:

```powershell
$body = @{
  projectId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
  take = 50
  actorId = "operator:projection-rebuild"
  collectionName = "candoitall-knowledge"
  projectMissingRecords = $true
  projectionProfileId = "qdrant-default-v1"
  embeddingProfileId = "local-hashing-v1:dimension=384"
  targetProviderName = "qdrant"
  projectionStoreKind = "Qdrant"
  vectorDimensions = 384
} | ConvertTo-Json

Invoke-RestMethod -Method Post -ContentType "application/json" -Body $body http://127.0.0.1:5289/api/cognitive-memory/v1/projections/rebuild
```

5. Verify Qdrant directly:

```powershell
Invoke-RestMethod http://127.0.0.1:6333/collections/candoitall-knowledge
```

6. Verify recall uses vector projection:

```powershell
$body = @{
  projectId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
  query = "Which source-backed facts must be considered?"
  intent = "SourceLookup"
  mode = "DeepSourceGrounded"
  projectionCollectionName = "candoitall-knowledge"
  projectionProfileId = "qdrant-default-v1"
  embeddingProfileId = "local-hashing-v1:dimension=384"
  budget = @{
    coarseCandidateLimit = 24
    vectorResultLimit = 12
    focusLimit = 8
    detailItemLimit = 8
    contextCharacterBudget = 12000
    maxSourceBytes = 24000
  }
} | ConvertTo-Json -Depth 8

Invoke-RestMethod -Method Post -ContentType "application/json" -Body $body http://127.0.0.1:5289/api/cognitive-memory/v1/recall
```

7. Verify `/cognitive-memory` health tab shows projection state and operator audit if failures occur.

Do not treat a skipped vector channel as a provider success. A live proof must show projected items or durable failed/rebuildable projection state with actionable failure text.
