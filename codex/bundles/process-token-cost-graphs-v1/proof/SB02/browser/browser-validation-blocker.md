# SB02 Browser Validation Blocker

Route: `/processes/live`
Viewport target: `1600x900`
Run label: 2026-06-01 browser proof attempt

## Attempted Evidence

- Existing local app was available at `http://localhost:5032`, but it was started before these source changes and served a stale build.
- A fresh isolated build of `CanDoItAll.Web` was produced at `.codex\tmp\web-5034\` with exit code 0.
- Starting the isolated build on `http://localhost:5034` failed during database bootstrap.

## Startup Failure

The isolated app exited with:

```text
Unhandled exception. System.InvalidOperationException: PostgreSQL profile 'Local PostgreSQL' has CanDoItAll tables but does not match the merged PostgreSQL baseline. Missing schema requirements: table AgentFramework_WorkflowCheckpoints, index IX_Processes_ArtifactRecords_ProcessRunId_ProjectionIdentityHa~. Refusing to record migration 20260528182412_InitialPostgreSqlBaseline automatically.
```

## Closure Decision

- Browser screenshot proof for the updated `/processes/live` route is blocked by the local PostgreSQL baseline mismatch.
- I did not change or migrate the shared local PostgreSQL database as part of this feature validation.
- Behavioral proof for completed priced run history is covered by `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`.
