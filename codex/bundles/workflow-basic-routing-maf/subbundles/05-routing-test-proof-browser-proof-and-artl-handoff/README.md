# Routing Test Proof Browser Proof And ARTL Handoff

## Status

- `Ready`

## Objective

- Close the basic routing bundle with consolidated build/test/browser evidence, architecture review, and a precise ARTL handoff contract.
- Verify the implementation uses MAF routing primitives for current basic routing and does not leave cosmetic-only route UI or silent compatibility gaps.
- Produce durable execution notes so the next ARTL bundle can replace the route compiler without reworking workflow canvas and persistence foundations.

## Success Criteria

- All route domain, compiler, UI, persistence, and API tests pass or any blocker is explicitly recorded with exact failing command output.
- Browser proof covers route creation/edit/save/validate/preview-run on the workflow canvas.
- Execution report contains subbundle gate results, browser validation analytics, raw-note closure, and ARTL handoff notes.
- Final review confirms no arbitrary code evaluation and no silent fallback from invalid routes to direct edges.

## Covered Inputs

- User requirement: deliver an execution-grade bundle, not just a sketch.
- User requirement: use MAF prepared routing now and leave future replacement by ARTL.
- Cross-subbundle requirement: final proof must establish runtime behavior, UI behavior, persistence behavior, and regression safety.
- Current-state risk: previous `ConditionExpression` handling could look like routing while only serving as label metadata.

## Prerequisites

- Subbundles 01 through 04 completed or explicitly blocked with evidence.
- Latest execution report contains proof rows for each completed subbundle.
- Browser/app startup instructions are available from existing workflow canvas bundle evidence or current project run configuration.

## Exact Source References

- `/mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Unit/WorkflowFoundationTests.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Integration/ProcessWorkflowExecutorIntegrationTests.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/.codex/bundles/workflow-basic-routing-maf/reviews/01-execution-report.md`

## Deliverables

- Completed `reviews/01-execution-report.md` with final gate results.
- Browser evidence under `reviews/evidence/` for route canvas authoring and preview-run.
- Final architecture review notes covering MAF primitive usage, route contract stability, ARTL seam, and production durable-routing caveats.
- Updated shared prompt or follow-up note for the future ARTL bundle.
- Final go/no-go statement for merging the basic routing implementation.

## Dependency Impact

- This subbundle is the final closure gate; downstream ARTL work must not start from partial or unproven routing foundations.
- Process/workflow integration tests depend on the final proof to ensure basic routing does not regress existing process workflow execution.
- Future UI enhancements depend on the browser proof to avoid route-builder layout regressions.

## Validation Depth

- `End-to-end regression and closure`: unit, component, integration, browser, and architecture-review proof are required.

## Implementation Steps

1. Review execution-report rows from subbundles 01 through 04 and fill any missing command output, blocker, or proof link.
2. Run targeted unit tests for workflow foundation, executor routing, and catalog route persistence.
3. Run workflow component tests for canvas route authoring.
4. Run integration tests for workflow API and, if impacted, process workflow executor integration.
5. Start the app using the current project run pattern and perform maximized desktop browser proof for route creation/edit/save/validate/preview-run.
6. Perform narrower-width follow-up proof for the route builder and edge inspector layout.
7. Review screenshots for clipping, unreadable labels, ambiguous default branch display, and validation visibility.
8. Confirm compiler source uses `AddEdge<WorkflowNodeInput>`, `AddSwitch`, and `AddFanOutEdge<WorkflowNodeInput>` for executable routing.
9. Confirm unsupported ARTL routes are rejected now and the compiler seam is ready for a later `ArtlWorkflowRoutingCompiler`.
10. Update `reviews/01-execution-report.md`, root `README.md` validation summary, and raw-note closure.

## Scope Exceptions

- Production DurableTask/DTS proof remains out of scope unless the repo already has durable host support wired for this feature.
- ARTL implementation remains out of scope; only the handoff seam and unsupported-language behavior close here.
- Full accessibility audit is out of scope, but visible labels, keyboard-reachable fields where current UI supports them, and non-clipped controls must be checked.

## Do Not Do

- Do not mark final closure as passed with missing browser proof unless a blocker is recorded and the bundle is explicitly left blocked.
- Do not claim ARTL support beyond the compiler seam and language placeholder.
- Do not ignore failing tests that relate to workflow graph, runtime, persistence, or canvas routing.
- Do not rely only on screenshots without test proof for route execution.

## Acceptance Checklist

- Domain, compiler, UI, persistence, and API validation evidence is complete.
- Browser analytics table includes desktop and narrower-width route-builder proof.
- Runtime proof confirms real branch execution decisions.
- Final review confirms no arbitrary code evaluation and no silent route fallback.
- ARTL handoff is documented with the expected compiler interface, routing language, and UI extension point.

## Proof Required

- `dotnet test /mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~WorkflowFoundationTests|FullyQualifiedName~WorkflowExecutorTests|FullyQualifiedName~WorkflowCatalogTests" --verbosity minimal -m:1`
- `dotnet test /mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter FullyQualifiedName~WorkflowsPageTests --verbosity minimal -m:1`
- `dotnet test /mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~WorkflowApiIntegrationTests|FullyQualifiedName~ProcessWorkflowExecutorIntegrationTests" --verbosity minimal -m:1`
- Browser screenshots and screenshot-review notes under `reviews/evidence/subbundle-05/`.
- Source-review note confirming executable routes use MAF routing primitives and unsupported ARTL is rejected until implemented.

## Browser Validation Logging

- Route: `/agents/workflows` or the active workflow canvas route in the current app.
- Viewports: maximized desktop plus one narrower-width pass.
- Playwright actions/assertions: create or load routed workflow, edit route mode, validate, save, reload, run preview, assert visible selected branch/result, inspect edge summary and default branch display.
- Evidence files: `reviews/evidence/subbundle-05/workflow-routing-e2e-desktop.png`, `workflow-routing-e2e-preview.png`, `workflow-routing-e2e-reloaded.png`, and `workflow-routing-e2e-narrow.png`.
- Screenshot review questions: branch labels visible, default branch obvious, route-builder controls not clipped, validation errors actionable, and preview result tied to expected route.

## Progression Gate

- The bundle can close only when execution proof, UI/browser proof, persistence proof, and ARTL handoff notes are complete; otherwise mark the final status `Blocked` with exact missing proof.

## Suggested Agent Prompt

```text
Implement subbundle 05 only.
Close the workflow basic routing bundle by running targeted tests, browser proof, screenshot review, source review for MAF routing primitive usage, and ARTL handoff documentation. Update the execution report and do not mark final closure passed unless the evidence is complete.
```
