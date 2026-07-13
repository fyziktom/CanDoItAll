# B05 — Structured result persistence in AgentFramework

## Goal

Ensure every process-bound AgentFramework execution has a persisted diagnostic summary usable by process projections and manager rework.

## Required changes

### 1. Persist structured process outcome

When `AgentFrameworkProcessExecutionAdapter` executes a process step with structured output `ProcessStepOutcomeResult`, persist the final validated structured output as compact JSON in `ExecutionRunRecord.ResultSummary` or a dedicated process summary field.

The JSON should include:

```json
{
  "status": "Completed|Blocked|WaitingApproval",
  "reason": "...",
  "branchOutcomeKey": "...",
  "branchOutcomeTitle": "...",
  "evidenceRefs": ["..."],
  "nextActions": ["..."],
  "failedTools": ["..."],
  "primaryManagedArtifactRef": "..."
}
```

### 2. Preserve raw model output separately

If raw model text is needed for audit/debugging, store it as a detail/artifact. Do not make operator diagnostics depend only on raw response text.

### 3. Persist repaired/validated structured output

If the adapter repairs or normalizes structured output, persist the final version that runtime used, not just the original response.

### 4. Failure path

If the run fails before structured output exists, persist a compact failure JSON with:

- `status: "Blocked"`,
- failure reason,
- last error,
- failed tool names and diagnostic refs,
- execution run id.

## Tests

- `ProcessExecution_WhenStructuredOutputParsed_ResultSummaryContainsOutcomeJson`
- `ProcessExecution_WhenStructuredOutputValidationFails_ResultSummaryContainsFailureJson`
- `OperatorDiagnostics_CanParsePersistedStructuredResultSummary`

## Acceptance criteria

- `ProcessRuntimeOperatorActionDiagnostics.Create` receives parseable JSON for normal process-bound runs.
- Missing `ResultSummary` becomes exceptional, not routine.
