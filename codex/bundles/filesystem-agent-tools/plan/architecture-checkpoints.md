# Architecture Checkpoints

## CP-001 Boundary Extraction

- `WorkspaceFilesystemRuntimePlugin` exists as a top-level type.
- No new partial class is introduced.
- Filesystem methods are removed from `WorkspaceRuntimePlugin`.

## CP-002 Policy And Catalog

- New filesystem tool names are defined in `ToolContractCatalog`.
- `ToolCapabilityRegistry` classifies each new tool.
- Capability templates contain new rows with approval requirements for mutations.

## CP-003 Testability

- Direct tests instantiate the extracted plugin without `MafAgentRuntime`.
- Negative access test proves mutation is denied for read-only settings.

## CP-004 Closure

- Affected builds and focused tests pass.
- Architecture gate finds no fake separation or runtime partial expansion.
