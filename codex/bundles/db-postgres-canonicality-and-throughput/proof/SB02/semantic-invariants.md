# SB02 semantic invariants

## SB02-I2 runtime and pending activation are distinct

- Source raw note: pending activation must not be shown as the current runtime profile.
- Expected behavior: a running process exposes the startup canonical profile as runtime and the selected catalog profile as pending restart when they differ.
- Disallowed shallow implementation: renaming `Active` labels while still deriving runtime from the persisted selected profile.
- Failing-first transcript: `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch.txt`.
- Passing transcript: `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch-rerun.txt`.
- Changed source files and hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.
- Production assertions: runtime state is produced by `repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs` and consumed by workspace/API/UI contracts.
- Red-team negative case: `bundle://proof/SB02/transcripts/managed-files-runtime-profile-test-rerun.txt` proves managed files still use the running profile after a pending activation.
- Downstream dependency check: `bundle://proof/SB08/transcripts/semantic-invariant-index.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Runtime profile identity | `repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs` | `repo://src/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs` | `bundle://proof/SB02/transcripts/unit-runtime-switch-tests.txt` | `bundle://proof/SB02/transcripts/managed-files-runtime-profile-test-rerun.txt` |
| Pending restart identity | `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs` | `repo://src/CanDoItAll.Web/Components/Layout/MainLayoutDatabaseDialog.razor` | `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch-rerun.txt` | `bundle://proof/SB02/transcripts/playwright-runtime-pending-switch.txt` |
