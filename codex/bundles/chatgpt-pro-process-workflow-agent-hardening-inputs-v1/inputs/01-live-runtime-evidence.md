# Live Runtime Evidence

## Runtime Profile

The expected `localhost:5032` host was not initially reachable. A separate `CanDoItAll.Web.exe` instance was already running on `http://localhost:5034` against database `candoitall_codex_graphs_20260601`. That 5034 instance contained a separate failed graph-validation process run and is not the development database Tetris evidence.

For this preparation pass, a no-build `CanDoItAll.Web.exe` instance was started on `http://localhost:5032` with the PostgreSQL database override pointing at `candoitall_development`. API evidence was captured from that host.

Environment friction observed:

- A direct `dotnet run` attempt for port 5032 failed because the existing 5034 process had locked build outputs.
- The running 5034 host used a different database profile than the requested development database.
- The 5034 graph database had a failed process caused by `cognitive-memory.context` requiring a project scope. Treat this as comparative runtime friction, not as the Tetris run.

## Successful Tetris Process Run

Run id: `6724b4c8-c774-4880-becc-940a3d7bf155`

Definition id: `b1e435a7-18b7-45fb-bf04-e0b745278c99`

Definition version id: `78603ab5-053a-4aa5-8c51-e9e419f209d4`

Project id: `bd4b3eea-e18e-47b4-bcd8-d2e749243bb4`

Name: `Main App / Multi-team software delivery and release governance`

Status: `3` in API numeric enum shape, observed as completed in the UI/API context.

Operating mode: `2`

Manager: `Default process manager`

Step counts:

- Completed: 9
- Total: 16
- Blocked: 0
- Capability gaps: 0

Cost fields:

- Estimated cost: `5360`
- Actual cost: `0.082678`

Updated at: `2026-06-01T14:41:46.536168-04:00`

Health summary:

- No active executions.
- Latest attempt count: 12.
- Missing artifacts: 0.
- No blocked, failed, or waiting step state in final run health.
- No invariant diagnostics in final run health.

## Process Step Outcome Summary

Completed first-pass path:

1. `Clarify scope and release boundary`
   - Executor: `Product owner AI agent`
   - Artifact: `Scope boundary packet`
   - Allowed operations included read context, project structure, upstream artifacts, and managed artifact writing.

2. `Review architecture and canonical-model impact`
   - Executor: `.NET Solution Architect`
   - Artifacts included `Project structure context brief` and `Architecture decision record`.
   - Allowed operations included review/escalation style operations but not product mutation.

3. `Implement bounded delivery change`
   - Executor: `Blazor Application Developer`
   - Outcome: implemented a static Blazor WASM Tetris app with SVG rendering, keyboard input, automatic fall, IndexedDB best-score persistence, PWA/static-host assets, and validation evidence.
   - Allowed operations included product mutation, validation, and managed artifact writing.

4. `Complete peer review and integration readiness`
   - Executor: `Blazor Application Developer`
   - Artifact: peer review note.

5. `Run QA validation and runtime or browser proof`
   - Executor: `JavaScript QA Review Lead`
   - Final decision: quality accepted.
   - Evidence included `Regression evidence pack`.
   - Runtime evidence path included `20260601-183723045-dotnet-run-http-smoke/stdout.txt`.

6. `Perform security and data-handling review`
   - Executor: `Security Reviewer`
   - Artifact: `security-exception-assessment.md`.

7. `Approve first-pass release readiness`
   - Executor: `Delivery Manager`
   - Artifact: release approval record.

8. `Execute first-pass controlled release rollout`
   - Executor: `Release Readiness Manager`
   - Outcome: successful browser smoke against `http://127.0.0.1:64762/`.
   - Browser state showed TetrisGame.
   - Keyboard input changed state.
   - Console only had a benign favicon 404.

9. `Capture first-pass post-release learning`
   - Executor: `Delivery Manager`
   - Noted weak structured post-release telemetry/support-observation inputs and a favicon follow-up.

Skipped repair path:

- `Repair validation findings`
- `Re-run QA validation after repair`
- `Perform security review after repair`
- `Approve repaired release readiness`
- `Execute repaired controlled release rollout`
- `Capture repaired post-release learning`

## Process Timeline And Recovery Signal

The final run succeeded, but the attempt timeline is useful for hardening analysis:

- Scope execution run had 1 tool receipt.
- Architecture execution run had 8 tool receipts.
- Implementation execution run `b5ffaae6-cd8b-4e12-8a5a-ddd086250331` had 41 tool receipts and 28 artifacts.
- Initial QA execution run `f9420b70-f6e2-4042-8d7f-7ea654ed0f2c` failed with 15 tool receipts and 3 artifacts.
- Recovery/rework packets were recorded around `2026-06-01T14:33:40-04:00` and `2026-06-01T14:35:24-04:00`.
- Final QA execution run `cba5f08f-8f7b-410a-a6ea-242fbfab98d5` succeeded with 30 tool receipts and 6 artifacts.
- Security, release approval, rollout, and post-release steps each produced tool receipts and artifacts.

Hardening input:

The run proves the current process can deliver a working app, but it also shows that runtime proof, QA recovery, artifact lineage, and browser evidence policy are active complexity centers rather than settled edges.

## Implementation Artifact Shape

Implementation artifacts referenced external-target paths such as:

- `external-target/C/programovani/dotnet-demo/output/output.csproj`
- `Program.cs`
- `App.razor`
- `_Imports.razor`
- `Shared/MainLayout.razor`
- `Domain/ScoreStore.cs`
- `Domain/TetrisGameService.cs`
- `Pages/Index.razor`
- `wwwroot/index.html`
- `wwwroot/css/app.css`
- `wwwroot/js/indexed-db.js`
- `wwwroot/manifest.webmanifest`
- `wwwroot/service-worker.js`
- `wwwroot/service-worker.published.js`
- `wwwroot/icons/icon.svg`

Evidence artifacts included:

- `20260601-183019869-dotnet-build/stdout.txt`
- `20260601-183041328-dotnet-run-http-smoke/startup.json`
- runtime smoke output paths under the process artifact root

## Stale Or Alternate Lineage Signal

Some agent output lineage referenced a different process run id:

- `49fd1354-3625-45c2-b986-7e7f0c0246a7`

Direct process API lookup for that run id returned 404 and was saved as `inputs/api-captures/process-run-stale-49fd-detail.error.txt`.

This is important input for canonicity hardening. It may indicate stale run references, alternate artifact lineage, or earlier attempts that remained visible in agent outputs. The later bundle should inspect whether stale evidence can be mistaken for current-run evidence.

## Workflow Run Evidence

Workflow run id: `e58cb776-9dcd-4c99-acc4-e3fa0bddead0`

Workflow id: `ec134686-8cb2-49ac-b20f-bffcd04f8ff1`

Version id: `790a4fa5-6648-47c1-bf8b-368fdebe67a8`

State: `4` in API numeric enum shape, observed as completed.

Backend: `0`, observed as in-process.

Summary: `Workflow 'Example: Office365 Category Email Summary To Project' completed.`

Created: `2026-06-01T05:31:09.751625-04:00`

Updated: `2026-06-01T05:31:27.818519-04:00`

Workflow nodes/events:

- `start`
- `download-office365`, executor `office365.messages-by-category`
- `summarize-office365`
- `store-office365-summary`, executor `project-structure`
- `mark-office365-processed`, executor `office365.mark-message-processed`
- `end`

Workflow behavior:

- Source category: `CanDoItAllSummaryTest`
- Processed category: `CanDoItAllSummaryTestProcessed`
- Message count: 1
- Subject: `Tetris Game Development`
- Sender email was present in raw API response and redacted in saved evidence.
- Message summary requested a simple Tetris game, website first, mobile later optional, keyboard arrows or W/A/S/D, local best-score storage, no backend, static hosting, and one-week timing.

Workflow artifacts:

- `workflow-node-output-start.json`
- `workflow-node-output-download-office365.json`
- `workflow-node-output-summarize-office365.json`
- `workflow-node-output-store-office365-summary.json`
- `workflow-node-output-mark-office365-processed.json`
- `workflow-node-output-end.json`

Checkpoint state:

- Checkpoint metadata existed.
- Resume was unavailable for this completed run.

The workflow existed and completed, so no workflow rerun was performed.
