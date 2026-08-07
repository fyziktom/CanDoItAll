# Target workspace scope and lifetime

## One owned aggregate

```text
ProfileWorkspace
  owns WorkspaceRuntimeServices
    owns WorkspaceExecutionScope identity
    owns LocalWorkspaceProcessHost
    owns FileService
    owns PathResolutionService
    owns CommandExecutionService
    owns ArtifactToolService
    owns ImageOperationService
```

Run-owned runtime builds borrow or create a child view tied to the exact admitted execution identity. They do not create independent file/path/process services and do not dispose the profile aggregate from multiple participant builds.

## Identity fields

- canonical workspace root
- logical scope kind/key
- database profile ID
- database profile generation
- authority ID/fingerprint
- execution run ID when run-owned
- optional correlation/operation ID for diagnostics

## Cross-platform path semantics

- Normalize with `Path.GetFullPath`.
- On Windows, compare roots case-insensitively.
- On Linux, compare case-sensitively unless an explicitly detected filesystem policy says otherwise.
- Never persist physical root as cross-host authority.
- Logical scope/profile/authority identity must detect project GUID reuse across profiles.

## Recovery/policy rule

Every helper that reads files for recovery, validation, script inspection, artifact hashing, or model context must receive the same scope-bound service bundle as the tool that produced the evidence.
