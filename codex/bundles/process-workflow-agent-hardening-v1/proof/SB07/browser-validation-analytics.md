# SB07 Browser Validation Analytics

| Route/host | Viewport | Actions | Screenshot paths | Console evidence | Result |
| --- | --- | --- | --- | --- | --- |
| `/agents/workflows` on `http://127.0.0.1:5033` | `1440x1000` | Navigate, open Editor tab, select `Request external action approval`, open `Node setup`, capture snapshot and full-page screenshot | `proof/SB07/screenshots/sb07-workflows-node-setup-desktop.png`; `proof/SB07/browser/sb07-workflows-node-setup-desktop.md` | `proof/SB07/transcripts/browser-console/console-2026-06-02T03-51-06-638Z.log` | Passed after CSS correction; executor badges and details are readable |
| `/agents/workflows` on `http://127.0.0.1:5033` | `390x844` | Resize, keep selected node setup panel, capture snapshot and full-page screenshot | `proof/SB07/screenshots/sb07-workflows-node-setup-mobile.png`; `proof/SB07/browser/sb07-workflows-node-setup-mobile.md` | Same workflow console log as desktop capture | Passed; stacked layout remains readable |
| `/processes/live` on `http://127.0.0.1:5033` | `1440x1000` | Navigate, inspect Activity tab, capture snapshot and full-page screenshot | `proof/SB07/screenshots/sb07-processes-live-desktop.png`; `proof/SB07/browser/sb07-processes-live-desktop.md` | `proof/SB07/transcripts/browser-console/console-2026-06-02T03-52-04-630Z.log` | Passed; empty state and `$0 cost` are visible for isolated no-usage run |
| `/processes/live` on `http://127.0.0.1:5033` | `1440x1000` | Open Graphs tab, capture snapshot and full-page screenshot | `proof/SB07/screenshots/sb07-processes-live-graphs-desktop.png`; `proof/SB07/browser/sb07-processes-live-graphs-desktop.md` | Same live-process console log as desktop capture | Passed; graph panel reports no process cost data and does not render actual-cost data |
| `/processes/live` on `http://127.0.0.1:5033` | `390x844` | Resize, inspect responsive layout, capture snapshot and full-page screenshot | `proof/SB07/screenshots/sb07-processes-live-mobile.png`; `proof/SB07/browser/sb07-processes-live-mobile.md` | Same live-process console log as desktop capture | Passed; cost chip and metrics remain visible without overlap |

## Host Configuration

- `ASPNETCORE_ENVIRONMENT=Development`
- `Database__Provider=InMemory`
- `Database__ConnectionString=sb07-ui-validation`
- `Storage__WorkspaceRoot=.artifacts/sb07-ui-validation/workspace`
- `ControlPlane__RootPath=.artifacts/sb07-ui-validation/control-plane`
- `Processes__Runtime__RequirePostgreSqlForAgentAutomation=false`
- `Workflows__ExampleSeed__Enabled=true`
- `Workflows__ExampleSeed__SeedSampleWorkspaceFiles=true`

Host logs and cleanup proof:

- `proof/SB07/transcripts/browser-validation-host-stdout.log`
- `proof/SB07/transcripts/browser-validation-host-stderr.log`
- `proof/SB07/transcripts/browser-validation-host-cleanup.txt`

## Visual Review

The first node setup capture exposed a collapsed executor summary description. The final inspected screenshot is `proof/SB07/screenshots/sb07-workflows-node-setup-desktop.png`, which shows the corrected one-column summary layout and readable badge/detail text.

The live-process console log contains Blazor reconnect errors after the validation host was intentionally stopped. `proof/SB07/transcripts/browser-validation-host-cleanup.txt` records that the host was stopped and no process/listener remained on port `5033`.
