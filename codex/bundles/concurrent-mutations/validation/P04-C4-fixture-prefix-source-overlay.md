# C4 fixture prefix source overlay

Source-only, parent-authorized six literal correction. The existing environment factory
already calls TestFileSystem.CreateTemporaryRoot(prefix), which creates temp/prefix/newGuid.
Environment.DisposeAsync deletes only RootPath. Supplying a second GUID in prefix therefore
leaves an empty, unique outer directory after normal inner disposal. Stable scenario prefixes
retain per-fixture isolation through the factory's own GUID and avoid that abandoned outer level.

Only the following interpolation literals changed; assertions, methods, categories and row
counts are unchanged. MAIN remains untouched. Original248A inventory and38A3 selection are
retained verbatim, with each original source image in the owned ignored checkpoint.

| Source | Original literal line | Stable prefix | Selected rows |
| --- | ---: | --- | ---: |
| tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceAdmissionReadScalingIntegrationTests.cs | 380 | workspace-admission-read-scaling | 15 |
| tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests.cs | 422 | run-detail-update-recovery | 15 |
| tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceGenericNewRunCommitRecoveryIntegrationTests.cs | 186 | generic-new-run-recovery | 6 |
| tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceChatRunCommitRecoveryIntegrationTests.cs | 389 | chat-run-commit-recovery | 12 |
| tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspacePreparedCommitReadIntegrationTests.cs | 213 | prepared-commit-reads | 19 |
| tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspacePreparedRunWitnessTests.cs | 205 | prepared-run-witness | 11 |

The frozen 703 selection remains 247 Unit +456 Integration, ten lanes/487 methods/75 files.
The runner/source guards must receive an explicit successor binding for these six hashes
before use; the original runner correctly refuses these new bytes. Do not overwrite old
inventories, old MAIN source or validation outputs. Parent performs guarded transfer, builds,
static enforcement, exact fresh discovery/execution and independent resource reconciliation.

This correction does not turn a baseline directory delta into ownership. Future observation
records the exact known scenario roots before/after; any delta still requires parent inspection.
No directory cleanup, build, test, database or process execution occurred during authoring.
