# Process Role Workflow Integration

## Status

- `Ready`

## Objective

- Integrate workflows as a process role executor option beside AI agents while keeping processes as the higher-level orchestrator.
- Replace or wrap stringly executor-kind handling with typed executor selection so workflow execution does not spread fragile branching.

## Success Criteria

- Process role definitions and launch/assignment flows can select workflow executors.
- Process runtime can start and observe a workflow run for a role without becoming the workflow runtime.
- Workflow artifacts, durable RequestPort/human-in-loop requests, and run status can be related back to the process run through explicit references.
- Architecture review verifies process ownership remains intact.

## Covered Inputs

- RQ-003, RQ-004, RQ-011, RQ-012, RQ-019, RQ-020, RQ-021.
- RN-002, RN-003, RN-004, RN-008, RN-011.

## Prerequisites

- Subbundle 01 completed for typed workflow executor/model boundary.
- Subbundle 02 completed for workflow runtime manager.
- Subbundle 03 completed for runnable workflow definitions.
- Process module launch/assignment current behavior has been reviewed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Definitions\ProcessDefinitionEditorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Definitions\ProcessDefinitionEnums.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Entities\ProcessDefinitionEntities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Configurations\ProcessDefinitionEntityConfigurations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Persistence\Configurations\ProcessRuntimeEntityConfigurations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeViewModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.Reads.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Launch.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RuntimeOperations.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsAssignmentsSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsExecutionSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`

## Deliverables

- Typed process executor kind or executor selection model supporting human, AI agent, and workflow without magic strings.
- Process role definition/editor model changes for selecting workflow definition/version or workflow launch policy.
- Process launch candidate changes so workflow executors appear alongside AI agents where appropriate.
- Process runtime adapter that starts workflow runs through the workflow runtime manager and records process assignment references.
- Process read models/projections that show workflow-backed assignments, workflow run status, workflow artifacts, and pending workflow human-in-loop requests.
- Process runtime uses CanDoItAll workflow runtime contracts and does not care whether the selected backend is in-process or DurableTask/DTS.
- Tests covering existing agent/human assignment behavior plus new workflow assignment behavior.

## Dependency Impact

- Subbundle 07 depends on process integration for app-level route/API coherence.
- Subbundle 08 depends on workflow-backed process run proof.
- Any future process executor kind depends on the typed executor model introduced here.

## Validation Depth

- Process-critical closure.
- Requires service/runtime tests, migration/persistence compatibility review if schema changes occur, browser proof if process UI changes, and architecture review.

## Implementation Steps

1. Review process role, launch, assignment, runtime, and persistence models.
2. Introduce or extend a typed executor selection model, preserving migration compatibility for existing persisted strings if needed through explicit conversion.
3. Add workflow selection fields and validation to process role definition/launch models.
4. Add workflow candidates to process launch flow.
5. Implement process-to-workflow runtime adapter using workflow runtime manager from subbundle 02.
6. Record process assignment references on workflow runs or workflow run references on process assignments according to approved architecture.
7. Surface workflow status, artifacts, and pending requests in process read models/UI where relevant.
8. Add tests for backward compatibility, agent assignment, workflow assignment, workflow runtime failure, and process status projection.
9. Run build/tests.
10. Run browser proof if process UI changed.
11. Run architecture review focused on process ownership and typed executor model.
12. Update execution report.

## Scope Exceptions

- Do not replace process runtime with MAF workflows.
- Do not build a new process canvas.
- Do not implement workflow authoring UI here.

## Do Not Do

- Do not add workflow support through ad hoc string comparisons such as `"workflow"` scattered through process code.
- Do not make process assignments depend directly on MAF runtime types.
- Do not duplicate workflow run event storage inside process tables.
- Do not let workflow human-in-loop requests bypass existing process visibility/governance.

## Acceptance Checklist

- Process role can select workflow executor through typed model.
- Existing human/agent executor behavior still works.
- Process launch can choose a workflow definition/version.
- Workflow-backed process assignment starts a workflow run and tracks status.
- Workflow artifacts/requests are visible or linked from process context.
- Durable workflow run ids/statuses are linked to process assignments without making process runtime own DurableTask orchestration.
- Tests prove backward compatibility and workflow assignment.
- Architecture review confirms process remains the orchestrator.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Process service/runtime tests covering workflow role assignment and existing executor behavior.
- Migration/compatibility proof if persisted executor-kind shape changes.
- Browser screenshots for process launch/assignment UI if changed.
- Execution report architecture review notes.

## Browser Validation Logging

- Route: process workspace launch/assignment route if UI changed.
- Viewports: maximized desktop and narrower-width when process UI changes.
- Playwright evidence: open process launch/assignment flow, select workflow executor, verify candidate details, inspect workflow-backed run status.
- Screenshots: process role workflow selection and workflow-backed assignment status.
- Use `N/A` only if implementation is service/API-only and no browser-visible process surface changes.

## Progression Gate

- Web app integration and final closure may proceed only after workflow-backed process role assignment is proven without weakening existing human/agent assignment behavior.

## Suggested Agent Prompt

```text
Implement subbundle 06 only.
Add workflow as a typed process role executor option while preserving process orchestration ownership.
Do not replace process runtime or leak MAF runtime types into process models.
Run process-focused tests and update reviews/01-execution-report.md.
```
