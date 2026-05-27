# SB12 Semantic Invariants

- Invariant ID: `SB12-INV-001`
- Source raw note: `RQ07` requires API/UI/operator surfaces to expose recorded-but-invalid artifact diagnostics.
- Expected behavior: process operator views show finalizer status, attempted path, suggested action, failure owner, diagnostic text, and danger tone for rejected artifact outcomes.
- Disallowed shallow implementation: changing only backend status without rendering actionable operator metadata, or letting rejected outcomes appear neutral or successful.
- Failing-first test: bundle://proof/SB12/transcripts/failing-first.txt demonstrates repository HEAD lacked finalizer status/action/owner rendering.
- Passing test: bundle://proof/SB18/transcripts/component-process-tests.txt and bundle://proof/SB18/transcripts/process-runtime-operator-readmodel-tests.txt passed after the UI/read-model change.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsArtifactsSection.razor, repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor, repo://src/CanDoItAll.Modules.Processes/Components/ProcessCanvasSelectionPanel.razor, and repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs.
- Production assertions: bundle://proof/SB12/transcripts/source-assertions.txt shows danger-tone mapping and rendered diagnostic metadata for rejected statuses.
- Red-team negative case: bundle://proof/SB12/transcripts/failing-first.txt rejects the prior operator surface that omitted finalizer status/action/owner.
- Downstream dependency check: bundle://proof/SB18/transcripts/component-process-tests.txt proves process component tests still pass; bundle://proof/SB12/browser-live-processes-route.png documents the live route smoke limitation.
