# Retention Cleanup

Retention cleanup is explicit operator/API work. It is not a background worker.

## API

Routes:

- `POST /api/cognitive-memory/retention/cleanup`
- `POST /api/cognitive-memory/v1/retention/cleanup`

Default behavior is dry-run.

```json
{
  "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "deleteBeforeUtc": "2026-04-19T00:00:00Z",
  "dryRun": true,
  "scopes": [
    "RecallTraces",
    "ConsolidationCandidates",
    "ProbeSessions",
    "DistributedJobs"
  ],
  "actorId": "operator:retention"
}
```

## Scope

| Scope | Deletes | Does not delete |
| --- | --- | --- |
| `RecallTraces` | Trace rows, stages, candidates, context packs, context sections, source refs older than the cutoff. | Memory records, claims, source items, evidence anchors. |
| `ConsolidationCandidates` | Rejected and skipped-duplicate candidates older than the cutoff. | Draft, review-required, or mutation-submitted candidates. |
| `ProbeSessions` | Closed or abandoned sessions and dependent turns, feedback, findings, regression cases, regression runs. | Active sessions. |
| `DistributedJobs` | Completed, rejected, or expired jobs and worker results older than the cutoff. | Queued or leased jobs. |

The cutoff must be earlier than current UTC time. Use dry-run first, inspect counts, then execute with `"dryRun": false`.

Each cleanup call records a `CognitiveMemoryRunRecord` with `RunKind = RetentionCleanup`. Dry-runs use `OperationMode = Observe`; deleting runs use `OperationMode = Maintenance`. The health-tab operator audit surface reads these run records so cleanup activity is visible after execution.
