# Permission, Audit, And Redaction Policy

## Required Verification Flow
1. Validate permission mode.
2. Validate capability scope.
3. Deny side-effect operations.
4. Normalize evidence references.
5. Redact sensitive diagnostics.
6. Produce audit facts.
7. Return response with `NoMutationPerformed = true`.

## Denied Operations
- ExecuteCommand
- RestorePackages
- WriteWorkspaceStorage
- WriteArtifact
- MutateProcessState
- ApplyTransition
- ApplyFinalizer
- ScheduleRetry
- CallOfficeGraph
- MutateBusinessRecord

## Audit Fact Requirements
- caller id
- timestamp
- mode
- lane
- operation
- evidence reference id/path/hash
- accepted/denied
- denial reason
- redaction status
- non-sensitive summary
