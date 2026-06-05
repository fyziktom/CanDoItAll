# Observation / Outcome Boundary Design

## Module-local helper families

Expected new or expanded helper families:

1. `ProcessAutomationSessionObservation`
   - Parses serialized session state.
   - Produces successful tool names, tool result texts, file writes, file reads, stat paths, assistant response, assistant error summary, browser outputs.

2. `ProcessAutomationExecutionLogObservation`
   - Parses execution log tool invocations.
   - Produces successful internal/MAF tool names and browser output files.

3. `ProcessAutomationObservationSnapshot`
   - Combines run, detail, session observations, execution-log observations, browser output facts, and result summary.

4. `ProcessDeclaredStepOutcomeRules`
   - Parses `ProcessStepOutcomeResult`.
   - Maps status.
   - Resolves branch key/title/id.
   - Detects blocked outcome claiming missing tools without receipt.

5. `ProcessCompletionDecisionSnapshot`
   - Captures run state/outcome, pending approvals, declared outcome, context validation, missing tools, blockers, critical failures, disposition facts.

6. `ProcessCompletionStatusDecisionRules`
   - Moves the branch-heavy status decision from `ToolValidation.cs` into explicit rules.

7. `ProcessCompletionReasonBuilder`
   - Moves reason-text construction helpers behind stable inputs.

## Why this helps future Process Core

Core should not know MAF session JSON, execution log text formats, or provider-native browser output conventions. This bundle introduces a module-local translation layer first, so future Core can consume process-owned observations rather than raw runtime/provider artifacts.

## Why this helps future drivers

Future helper drivers should produce evidence observations that look like the normalized observation families, not raw tool logs. This bundle documents driver-readiness names only; no production driver API.
