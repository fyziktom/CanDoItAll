# SB022 - Core-readiness decision matrix

## Status
Prepared.

## Objective
Produce an evidence-backed matrix of what is ready for future Core, what must stay application/infrastructure, and what blockers remain.

## Covered Inputs
- Original request to continue incremental dispatch/process isolation.
- Preserve all behavior.
- Do not start Process Core.
- Do not introduce production driver APIs.
- Avoid micro-subbundle work; this subbundle owns a coherent work slice.

## Prerequisites
- Previous subbundles must be complete.
- If this subbundle follows a critical gate, that gate must have passed.

## Exact Source References
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlers.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchClaimLease.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs`
- Related subprocess, finalizer, materialization, and candidate hydration helper files under the same folder.

## Deliverables
- Source changes implementing the objective.
- Focused tests or characterization tests where behavior moves.
- Source scans proving no forbidden drift.
- Execution report row with concrete proof paths.

## Dependency Impact
Do not create Core. This is a decision artifact only.

## Validation Depth
Markdown matrix + source references + review signoff.

## Implementation Steps
1. Re-read the current source before changing anything.
2. Add or identify characterization tests for the behavior being moved.
3. Move behavior into the target module-local service/helper boundary.
4. Keep side effects explicit and named.
5. Run focused tests.
6. Run source scans relevant to this subbundle.
7. Record proof in the execution report.

## Scope Exceptions
- Process Core extraction is out of scope.
- Production process driver APIs are out of scope.
- UI and mobile/browser visual proof are out of scope.

## Do Not Do
- Do not remove behavior.
- Do not hide side effects inside misleadingly pure helpers.
- Do not create broad all-in-one service replacements.
- Do not collapse proof rows.
- Do not add TODO/stub/placeholder code.

## Acceptance Checklist
- [ ] Behavior preserved.
- [ ] Source movement reduces dispatcher ownership or proves why a boundary must remain.
- [ ] Focused tests pass.
- [ ] Source scans pass.
- [ ] Execution report row is complete.

## Proof Required
- Build/focused test transcript as applicable.
- Source scan transcript.
- No-Core/no-driver/no-UI proof.
- If this is a critical gate, semantic invariants and manifest.

## Browser Validation Logging
N/A. Runtime/service refactor only. Do not create small/medium/mobile proof.

## Progression Gate
Downstream subbundles may continue only after this subbundle's proof is recorded. Critical gates must block downstream work until closed.

## Suggested Agent Prompt
Implement SB022 from the bundle. Keep behavior identical, avoid Process Core and driver APIs, and produce the proof required above.
