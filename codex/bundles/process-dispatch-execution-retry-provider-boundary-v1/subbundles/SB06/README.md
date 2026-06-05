# SB06 - Response text resolver

## Status
- Completed

## Objective
Extract recovered/preferred response text resolution from Concurrency.cs.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`

## Prerequisites
- `SB05` closure gate passed.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`

## Deliverables
- ProcessExecutionResponseTextResolver
- Wrappers preserved in dispatcher

## Dependency Impact
- This subbundle affects downstream execution/retry/provider recovery proof. If it fails, reopen this subbundle before continuing.

## Validation Depth
- Focused build/source proof. Add tests if behavior is moved or branch order can drift.

## Implementation Steps
1. Implement or document: ProcessExecutionResponseTextResolver
2. Implement or document: Wrappers preserved in dispatcher

## Scope Exceptions
- Do not create Process Core or production driver APIs in this subbundle.

## Do Not Do
- Do not create `CanDoItAll.Processes.Core`.
- Do not add `IProcessDriverPack`, driver registries, or driver packages.
- Do not change retry counts, provider fallback behavior, no-progress fingerprint semantics, recovery journals, or completion decisions.
- Do not create small/medium/mobile/browser screenshots; runtime/service refactor should keep browser proof N/A.

## Acceptance Checklist
- [ ] Existing wrappers remain unless explicitly removed by a gate.
- [ ] No behavior drift is introduced.
- [ ] New helpers are module-local under `CanDoItAll.Modules.Processes`.
- [ ] Source scans show no Process Core or production driver API.

## Proof Required
- Focused tests for resultSummary vs assistant message vs serialized session selection

## Browser Validation Logging
- N/A expected. Runtime/service refactor only. If UI files unexpectedly change, stop and record only large desktop/PC proof after explicit review.

## Progression Gate
- Downstream work may continue only after proof files are committed and the subbundle row in `reviews/01-execution-report.md` is updated.

## Suggested Agent Prompt
Implement SB06 only. Keep work module-local, preserve behavior, update proof, and do not start later subbundles until this gate is closed.

