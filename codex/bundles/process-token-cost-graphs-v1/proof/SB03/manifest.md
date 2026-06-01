# SB03 Proof Manifest

- Subbundle: `03-03-process-workspace-graph-tabs`
- Status: `Implemented; browser screenshot blocked by local database baseline`
- Owned requirements: R005, R006, R007, R008
- Owned raw notes: N006, N007, N008, N009
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Changed File Hashes

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessObservationGraphsPanel.razor` | `NEW` | `a1983109b3e996b9a704e980f9349ce707821976dbe2c127696d06588cd33cf9` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs` | `NEW` | `04fb16cd984516fd7a2eb28493bad8c58e1e43f3c3a46a436df6b2a1b853ecfe` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.GraphsPresenter.cs` | `NEW` | `1b6c83d9b297189d96619e1ceb8621eff2098a0f000e3bdee3e35617c6d8ad5e` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Loading.cs` | `bd37a985d74d8179e51a0a499c0579d25909ee5cbad4fff201a24bb03debf4fa` | `81d0824a133ded516c1aba73aee648f5eed3de3d4c61e3732cf1ad40c66cc321` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Presenters.cs` | `3efcdaa405ca39ec99630adfab3e27f7d493037730d5363ff2573fbd5ffd0e42` | `4b77ddc798817f599ab2e4f6d25b4ac73aefa0a8dbe559ef4b0dd613b77a2c44` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RunsPresenter.cs` | `92e9cfca2d941a81d3ff3d33fb9a79647fd5c78821ba49bd01757611775ffdfe` | `1aa17df234f43a5dc8f97a5c3ca8d8cd91fdaf9c88a98ca7f69c481fb8b46218` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RuntimeOperations.cs` | `ee32ca8302acda4596bc0c630e88e7621e9d9c0ed9a2804085aeb775ed0fdd8c` | `5456453bb1f425c3374e934db25b4d3a0e67511016fd7764d074661e8bfb915f` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor` | `a2511a0071c83efce98a53e8c4b7026a571c9797840e4c178153cf1960ce5abd` | `d341679eb9ff8a712c910b9f14e1e43d9c4eb06ea200ecf8e5f75c3158de579b` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs` | `809ac39583e6ae6f10498ba91df34a4a6f5b862b611b468a4345c00121341d0d` | `412b6a9eb7888a99c862d5eee7c147ebc596167d871c2ae2cd56b7cffa05a9ac` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceGraphsTab.razor` | `NEW` | `17cece00ad5d0ac2479674256846c6160d10c70628c00246155df4ae744543b0` |
| `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor` | `3968eecc7d6135a9db0f348a2e5ff2a595be77babd5f40ac5a5a07518a2f8922` | `12a44e96fef179dde67e85ef04292a2f58bb19520af10686cd2e62a2730d9cf1` |
| `repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs` | `7cb4c8c91c7416d587e2c9d7de2ab71164c7f39448890cc260ca333cadf43aac` | `71290d2adaa6a63a3509bc347420cdc85716fe3a58f80cd06c8ea132f0607749` |

## Command Transcripts

- Component build: `bundle://proof/SB03/transcripts/component-build.txt`
- Component lazy graph proof: `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Browser host attempt: `bundle://proof/SB03/transcripts/web-isolated-build-and-browser-blocker.txt`
- Browser blocker: `bundle://proof/SB03/browser/browser-validation-blocker.md`

## Failing-First And Passing Proof

- Failing-first case: selecting the process graph tab would eagerly load all-runs history.
- Passing proof: `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`.
- Failing-first case: selected-run graph tab would reuse all-runs data or load before selected.
- Passing proof: `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`.

## Browser Or Host Proof

- Updated web app compiled from isolated output with zero errors.
- Updated browser route could not be hosted because the local PostgreSQL baseline check refused startup.
- Browser screenshot proof is blocked; see `bundle://proof/SB03/browser/browser-validation-blocker.md`.

## Anti-Stub Audit

- `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Result: no placeholder or fixture-only markers in changed SB03 production files.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Process graph explicit load | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceGraphsTab.razor` | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs` | button click loads process-scoped graph snapshot | `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt` |
| Process graph range | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs` | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs` | default one-month range and explicit reset on range change | `bundle://proof/SB03/transcripts/source-assertions.txt` |
| Selected-run graph load | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor` | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Graphs.cs` | nested run graph tab activation loads selected run only | `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt` |
