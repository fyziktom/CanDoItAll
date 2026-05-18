# Distributed Idle Compute Architecture

## Purpose

Use idle machines on the user's LAN to run safe, deterministic memory jobs such as feature extraction, clustering, embedding, projection preparation, and source summarization.

## Important Constraint

Worker devices must not mutate authoritative memory directly. They produce signed/verifiable outputs that the main node validates and accepts.

## Roles

| Role | Responsibility |
|---|---|
| Coordinator | Owns job queue, leases, validation, authoritative writes. |
| Worker | Runs assigned jobs during idle time. |
| Verifier | Checks output schema, hashes, model/algorithm version, optional duplicate execution. |
| Curator | Accepts/rejects results and writes memory changes. |

## Job Types

- `SourceHashScan`
- `TextNormalization`
- `EmbeddingBatch`
- `SpatialFeatureExtraction`
- `GraphFeatureExtraction`
- `ClusteringBatch`
- `CanonicalizationDraft`
- `ProcedureExtractionDraft`
- `ProjectionPayloadBuild`
- `ContradictionCandidateScan`

## Job Packet

```json
{
  "jobId": "...",
  "projectId": "...",
  "jobType": "EmbeddingBatch",
  "inputRefs": [],
  "inputHash": "...",
  "algorithmVersion": "mindmap-feature-v1",
  "modelProvider": "onnx",
  "modelName": "Xenova/all-MiniLM-L6-v2",
  "expectedOutputSchema": "CanDoItAll.CognitiveMemory.JobOutput/1.0",
  "leaseToken": "...",
  "deadlineUtc": "..."
}
```

## Output Packet

```json
{
  "jobId": "...",
  "workerId": "...",
  "inputHash": "...",
  "outputHash": "...",
  "algorithmVersion": "mindmap-feature-v1",
  "modelProvider": "onnx",
  "modelName": "Xenova/all-MiniLM-L6-v2",
  "resultStorageReference": {},
  "warnings": [],
  "completedAtUtc": "..."
}
```

## Validation

Coordinator must validate:

- lease is active,
- input hash matches,
- algorithm/model version matches,
- output schema valid,
- output hash matches storage bytes,
- output does not contain unauthorized source content,
- high-risk jobs may require duplicate execution or local recomputation.

## Device Safety

- Workers should only receive minimal source content required for the job.
- Sensitive sources require policy checks.
- Remote worker should not receive secrets.
- Jobs must be cancellable.
- Worker app must support battery/thermal/device-idle policy.

## V1 Recommendation

Start with local PC worker only:

- background hosted service,
- queue in relational DB,
- deterministic source hashing,
- embedding/projection batches,
- no mobile/tablet distribution yet.

V1.1:

- LAN worker API,
- mobile/tablet idle worker,
- duplicate verification for high-value outputs.

## Neuro-Cognitive Distributed Boundaries

Distributed workers may run deterministic subjobs for:

- embedding refresh,
- clustering,
- source hash checks,
- feature extraction,
- replay regression execution,
- source anchor refresh proposals,
- context-boundary drill evaluation,
- procedure validation checks that produce evidence only.

Workers must not:

- approve mutation commands,
- write claims, belief states, procedure maturity, replay results, or Qdrant points directly,
- promote simulation output,
- create learning outcomes as accepted truth,
- bypass access policy or source scope.

Coordinator acceptance must validate input hash, output hash, source scope, worker capability, algorithm/profile version, policy scope, and idempotency state before any result is submitted to mutation authority or review.
