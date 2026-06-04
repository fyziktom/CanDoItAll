# SB03 Browser Validation Blocker

Route: `/processes`
Viewport target: `1600x900`
Run label: 2026-06-01 browser proof attempt

## Attempted Evidence

- Opened `http://localhost:5032/processes` with Playwright at `1600x900`.
- Captured stale-server artifacts:
  - `bundle://proof/SB03/browser/stale-server-processes-desktop.png`
  - `bundle://proof/SB03/browser/stale-server-startup-dialog-snapshot.md`
  - `bundle://proof/SB03/browser/stale-server-after-continue-snapshot.md`
  - `bundle://proof/SB03/browser/stale-server-main-depth8.md`
- The existing server was a stale build: the new `Graphs` tab was absent from the snapshot.
- Built the updated web app into isolated output `.codex\tmp\web-5034\` to avoid stopping the existing app.
- Starting that isolated build on `http://localhost:5034` failed during local PostgreSQL bootstrap.

## Startup Failure

```text
System.InvalidOperationException: PostgreSQL profile 'Local PostgreSQL' has CanDoItAll tables but does not match the merged PostgreSQL baseline. Missing schema requirements: table AgentFramework_WorkflowCheckpoints, index IX_Processes_ArtifactRecords_ProcessRunId_ProjectionIdentityHa~. Refusing to record migration 20260528182412_InitialPostgreSqlBaseline automatically.
```

## Closure Decision

- Browser screenshot proof for the updated process graph tabs is blocked by the local database baseline mismatch.
- Component proof covers the lazy-load behavior and scoped graph loading.
- No shared database migration or destructive reset was performed.
