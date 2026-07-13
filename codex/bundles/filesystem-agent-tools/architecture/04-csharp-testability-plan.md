# C# Testability Plan

## Isolated Unit Tests

- Instantiate `WorkspaceFilesystemRuntimePlugin` with `WorkspaceFileService`.
- Verify non-recursive directory listing returns only immediate children.
- Verify recursive listing returns nested children.
- Verify hash, zip, and unzip call through workspace file service and produce expected results.
- Verify read-only access blocks mutation/archive write operations predictably.

## Policy And Catalog Tests

- Assert new tool names exist in `ToolContractCatalog.WorkspaceToolNames`.
- Assert `ToolCapabilityRegistry` classifies hash/list as read and zip/unzip as workspace mutations.
- Assert capability templates seed the new tool capabilities.

## Composition Smoke Tests

- Verify configured workspace tools can include new filesystem tools when the agent has the corresponding capabilities.
- Verify mutation tools are approval wrapped unless suppression is active.

## Negative Tests

- Try `workspace_unzip_archive` with read-only settings and assert an explicit exception.
- Try a path outside allowed roots and assert the existing policy denial path remains active.

## Integration-Only

- Live 5032 testing is not required for this backend-only phase unless focused runtime composition tests uncover a registration bug.
