# Permission, Audit, Redaction, And Sandbox Contract

## Permission Modes
- `VerificationOnly`: may inspect provided evidence references and return diagnostics only.
- `ManagerReadonly`: may inspect provided process summaries and return denial-aware recommendations only.
- `ExecutionCapableFuture`: denied in this bundle and must not be represented as executable.

## Capability Scopes
- RouteEvidenceRead
- CoreDescriptorRead
- ArtifactEvidenceRead
- TranscriptRead
- RuntimeVerificationRead
- DotNetRustTranscriptRead
- OfficeEvidenceRead
- BusinessAnalysisEvidenceRead

## Denied Operation Categories
- ProcessStateMutation
- ClaimOrLeaseMutation
- TransitionExecution
- FinalizerApplication
- WorkspaceWrite
- StorageWrite
- ArtifactWrite
- ShellCommandExecution
- GraphOrOfficeCall
- BusinessRecordMutation
- SecretAccess
- RuntimeDriverSelection
- RetryScheduling

## Audit Fact Requirements
Every verification response must be traceable to:
- caller identity or system actor
- requested mode
- requested lane
- process/run/step ids when available
- evidence ids and hashes
- denial reason, if denied
- redaction status
- non-sensitive diagnostic summary

## Redaction Policy
- Secret-looking values must be masked.
- Tokens, connection strings, emails where not needed, and raw unrelated user content must not be emitted in diagnostics.
- Hashes may be emitted when useful for traceability.
