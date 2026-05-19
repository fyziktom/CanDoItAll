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

## Live Qdrant Validation

Run this only in an environment with a configured `IRagDriver` provider and a Cognitive Memory database profile with stale or failed projection rows.

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
} | ConvertTo-Json

Invoke-RestMethod -Method Post -ContentType "application/json" -Body $body http://127.0.0.1:5289/api/cognitive-memory/v1/projections/rebuild
```

5. Verify `/cognitive-memory` health tab shows projection state and operator audit if failures occur.

Do not treat a skipped vector channel as a provider success. A live proof must show projected items or durable failed/rebuildable projection state with actionable failure text.
