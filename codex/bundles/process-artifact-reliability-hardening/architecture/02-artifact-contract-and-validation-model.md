# Artifact Contract And Validation Model

## Artifact Modes

Add an explicit mode/profile to artifact expectations, or derive it deterministically from kind and validation requirement when a data model migration is too large for the first pass.

Recommended modes:

| Mode | Allowed producers | Minimum validation |
| --- | --- | --- |
| `Narrative` | assistant response, workspace text file, recovered summary | text content, required sections/keywords/schema if declared, current-run provenance |
| `Decision` | process auto-record, assistant response, workspace text file | decision status, rationale, owner/source, alternatives when required |
| `Evidence` | tool receipt, screenshot, log, test output, browser output, workspace file | source tool receipt or file, current-run provenance, format/content check |
| `Deliverable` | workspace write, external target file, package output, generated source/report | real file/output, expected path/root, current-run receipt or source evidence |
| `RuntimeProof` | build/test/browser/host tool receipts and logs | command/tool success, current-run timestamp, durable output path |
| `RecoveryDiagnostic` | finalizer/recovery coordinator | cannot satisfy required deliverable/evidence expectation by itself |

## Required Artifact Validation

A required expectation is satisfied only if all checks pass:

1. A candidate artifact record exists.
2. The artifact was produced by an allowed producer for its mode.
3. The artifact is connected to the current process run and current step or an explicitly allowed carry-forward source.
4. The managed storage path exists when the mode requires a file.
5. Text/JSON/XML/Markdown formats pass declared structure validation.
6. Evidence modes have supporting tool receipt, execution run id, screenshot/log/test output, or equivalent source reference.
7. The artifact is not a placeholder, gap marker, stale managed file, unrelated browser scratch file, or final chat summary pretending to be evidence.

## Durable Diagnostics

For each required expectation that is not satisfied, persist diagnostics with at least:

```text
ProcessRunId
StepRunId
ExpectationId
ExecutionRunId?
WorkflowRunId?
FailureKind
AttemptedProducer
AttemptedPath
SourceArtifactId?
ToolReceiptId?
Message
SuggestedAction
Fingerprint
CreatedAtUtc
```

Diagnostics must be queryable from process run detail and recovery packet construction.

## Fingerprints

Use stable fingerprints to detect repeated invariant failures:

```text
hash(ProcessRunId, StepRunId, ExpectationId, FailureKind, AttemptedPath, ExpectedMode, ExpectedSchemaKey)
```

If the same fingerprint repeats without new evidence or changed prompt/recovery context, stop blind executor retries and route to recovery/blocking.
