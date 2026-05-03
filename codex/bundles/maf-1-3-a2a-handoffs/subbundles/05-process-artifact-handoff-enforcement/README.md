# Process Artifact Handoff Enforcement

## Status

- `Completed`

## Objective

Strengthen governed process artifact handoff so implementation agents cannot complete software-delivery work without QA-consumable evidence and downstream review agents must inspect those artifacts.

## Covered Inputs

- `NOTE-05`
- `REQ-08`

## Prerequisites

- Current process artifact validation behavior is understood.
- Handoff workflow direction from subbundle 04 is available or explicitly deferred.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.OutputValidation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ImplementationProof.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockThreeAgentArtifactHandoffFixture.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\steps\qa-validation.md`
- `C:\repositories\CanDoItAll\Templates\Processes\shared\checklists\qa-evidence-checklist.md`

## Deliverables

- Clear artifact expectations for implementation-to-QA handoff.
- Runtime validation that blocks completion when required implementation artifacts are missing.
- Review prompt/rules requiring direct read/stat of inherited implementation artifacts.
- Tests proving QA/review cannot approve without upstream implementation evidence.

## Dependency Impact

- Process flow integration must build on these gates so A2A/handoff improves real delivery instead of allowing shallow completions.

## Validation Depth

- Process-critical closure.

## Implementation Steps

1. Identify missing artifact/evidence gaps in current dispatcher rules.
2. Strengthen required artifact response and inspection rules only where current validation is too permissive.
3. Update process template prompts/checklists if they lack implementation-to-QA evidence.
4. Add or extend deterministic process mock tests for missing and present artifact paths.

## Scope Exceptions

- Do not require browser screenshots for non-UI process steps.
- Do not make every process step a software-delivery step.

## Do Not Do

- Do not relax `ProcessStepOutcomeResult` validation.
- Do not accept markdown summaries as a substitute for required files/artifacts.
- Do not fabricate artifact records in QA steps.

## Acceptance Checklist

- [x] Implementation steps state required artifacts clearly.
- [x] Missing required implementation artifacts prevent downstream QA approval.
- [x] QA/review steps inspect inherited artifacts before approving.
- [x] Tests cover both success and failure paths.

## Completion Notes

- Added path-specific upstream artifact inspection validation for governed review completion.
- Completion status, completion reason, structured outcome context validation, and retry reasons now distinguish missing upstream artifact inputs from missing direct stat/read inspection.
- Updated software-delivery QA template and shared QA checklist to require direct inherited implementation artifact inspection.
- Updated deterministic process mock QA runs to serialize successful `workspace_stat_path` and `workspace_read_file` calls for inherited artifact paths.
- Strengthened process mock integration tests to prove downstream review sees implementation artifact paths and dispatch drains to terminal outbox state.

## Proof Required

- [x] `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests" --no-restore -m:1`
- [x] `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests" --no-restore -m:1`
- [x] Direct assertion that missing implementation artifacts block QA/review progression.
- [x] Direct assertion that present artifacts are visible to downstream review.

## Browser Validation Logging

- N/A unless process UI artifact display changes.

## Progression Gate

- Process-flow integration may continue only after deterministic tests prove artifact handoff gates.

## Suggested Agent Prompt

```text
Implement subbundle 05 only: strengthen process artifact handoff enforcement and deterministic tests. Do not weaken structured output or finalizer validation.
```
