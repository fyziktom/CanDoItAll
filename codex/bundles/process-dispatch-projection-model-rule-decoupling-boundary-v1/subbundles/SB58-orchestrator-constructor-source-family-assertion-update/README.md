# SB58 - Orchestrator constructor source-family assertion update

## Status
Prepared.

## Objective
Orchestrator constructor source-family assertion update.

## Covered Inputs
- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- Requirements RQ-001 through RQ-010 as applicable.

## Prerequisites
- Previous subbundle: SB57
- Critical gate: No

## Exact Source References
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionContext.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/Process*ArtifactProjectionCoordinator.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables / Scope
- Implement only the slice described by this subbundle title.
- Update tests and proof if production source changes.
- Keep changes module-local under `CanDoItAll.Modules.Processes` unless a test-only file is updated.

## Dependency Impact
- Downstream subbundle: SB59
- If this subbundle changes projection shape or source-family order, reopen all downstream subbundles that depend on projection context/model shape.

## Validation Depth
Focused compile/test/source assertion validation appropriate to this movement slice.

## Implementation Steps
1. Re-read the exact current source before editing.
2. Make the smallest behavior-preserving movement for this subbundle.
3. Update or add focused unit/integration tests when the movement affects model conversion, matching, source-family order, candidate state, or side-effect routing.
4. Run the planned validation for this subbundle.
5. Record proof in `proof/SB58/` if implementing this bundle in-repo.

## Scope Exceptions
- Process Core extraction remains out of scope.
- Production driver APIs remain out of scope.
- UI/browser proof remains N/A unless UI files unexpectedly change.

## Do Not Do
- Do not create `CanDoItAll.Processes.Core`.
- Do not introduce production driver APIs.
- Do not touch UI/Razor/CSS/JS/TS files.
- Do not change projection source-family order.
- Do not remove or skip any existing projection family.
- Do not replace existing matching logic with weaker string-only shortcuts.

## Acceptance Checklist
- [ ] Behavior preserved.
- [ ] Projection source-family order preserved.
- [ ] No Core/driver/UI drift.
- [ ] No stubs or TODO placeholders.
- [ ] Downstream dependency impact reviewed.
- [ ] Proof recorded.

## Proof Required
- Source assertion transcript.
- Focused test transcript where applicable.
- Build transcript at critical gates.
- No-core/no-driver/no-UI scan at critical gates.

## Browser Validation Logging
N/A - runtime/service refactor only. Do not create small/medium/mobile proof artifacts.

## Progression Gate
Proceed only when local validation and source assertions pass.

## Suggested Agent Prompt
Implement SB58 from `process-dispatch-projection-model-rule-decoupling-boundary-v1`. Preserve behavior and projection order. Do not introduce Process Core, production driver APIs, UI changes, or mobile proof artifacts.
