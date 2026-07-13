# Requirement Traceability

| Requirement | Owning subbundle | Proof |
|---|---|---|
| FS-001 | SB01 | `WorkspaceFilesystemRuntimePluginTests.ListWorkspaceDirectory_delegates_to_shallow_file_service_operation` |
| FS-002 | SB01 | Existing recursive `workspace_list_files` path remains wired through `WorkspaceFilesystemRuntimePlugin`; composition tests pass. |
| FS-003 | SB01, SB02 | Existing copy/create behavior remains plugin-owned; templates/descriptions updated for common filesystem tools. |
| FS-004 | SB01, SB02 | `WorkspaceFilesystemRuntimePluginTests.HashZipAndUnzip_are_available_without_WorkspaceRuntimePlugin`; policy/catalog/template focused tests pass. |
| FS-005 | SB01 | `WorkspaceFilesystemRuntimePluginTests.Write_operations_fail_predictably_for_read_only_access`; service archive preflight regressions pass. |
| FS-006 | SB01 | Source assertion recorded in `reviews/csharp-architecture-gate.md`; `WorkspaceRuntimePlugin` no longer references `IWorkspaceFileService`. |
| FS-007 | SB02 | `ToolContractCatalog`, registry, and capability template focused tests pass. |
| FS-008 | SB03 | Focused test proof in `proof/focused-unit-test.txt`; composition proof in `proof/composition-unit-test.txt`. |
