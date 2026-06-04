# SB02 Proof Manifest

- Subbundle: `02-02-history-analytics-data`
- Status: `Implemented; browser screenshot captured with disposable PostgreSQL profile`
- Owned requirements: R004, R007, R008 and data support for R005/R006
- Owned raw notes: N001, N005, N006, N007, N008
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Hashes

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs` | `0d4c6e28d217553b474007a53e363da53418a1cc45613261db5a36e9f79d2f96` | `fa97b418ad9e9cf198c669c77fe04106c86539e72ed5d32c182b2908196831d4` |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | `68ee9c44272be691d31a206449f5c16e27f9cdea127b4d756b3a71951b28d325` | `31e55ac5ce6099e79c65e71b6076b5b725a2221c70d3210c089db578f8e24832` |
| `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor` | `ea5934f99af732131f1cfc51a5200ec653c5d07303b3dee3e579c8ba466d812c` | `6fcd4a1aaefd661f3ffc511801fe4ed97b1c8175421faefe3c880c0384a8cf12` |
| `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs` | `7cb4c8c91c7416d587e2c9d7de2ab71164c7f39448890cc260ca333cadf43aac` | `71290d2adaa6a63a3509bc347420cdc85716fe3a58f80cd06c8ea132f0607749` |

## Command Transcripts

- Processes module build: `bundle://proof/SB02/transcripts/processes-module-build.txt`
- Component history/scope proof: `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`
- Source assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Browser proof: `bundle://proof/SB02/browser/live-processes-one-day-graphs.png`
- Historical browser blocker: `bundle://proof/SB02/browser/browser-validation-blocker.md`

## Failing-First And Passing Proof

- Failing-first case: a completed priced run disappears from one-day live history after refresh when only active runs are selected.
- Passing proof: `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`.

## Browser Or Host Proof

- Updated web app was hosted on `http://localhost:5034` with disposable PostgreSQL database `candoitall_codex_graphs_20260601`.
- `/processes/live` was set to `1 day`, opened to `Graphs`, and rendered context/time/cost charts for the observed run; see `bundle://proof/SB02/browser/live-processes-one-day-graphs.png`.

## Anti-Stub Audit

- `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Result: no placeholder or fixture-only markers in changed SB02 production files.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessLiveObservationQuery.ProcessDefinitionId` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | created by process workspace graph load | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt` |
| `ProcessLiveObservationQuery.ProcessRunId` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | created by selected-run graph tab activation | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt` |
| Completed run price history | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor` | completed run updated inside selected history window remains observed | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt` |
