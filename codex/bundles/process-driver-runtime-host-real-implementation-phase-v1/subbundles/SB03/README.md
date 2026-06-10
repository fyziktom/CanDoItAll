# SB03: Dry-run execution host pipeline

## Status
Prepared.

## Objective
Refactor the dry-run host from a single evaluator into a real pipeline with testable stages.

## Covered Inputs
- `inputs/00-original-request.md`
- `analysis/01-real-code-review.md`
- `analysis/02-code-vs-bundle-churn.md`
- `analysis/04-gap-analysis.md`

## Prerequisites
- Previous subbundle closure gate passed.
- If any previous gate is blocked, stop and record the blocker instead of continuing.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionCapableDriverFutureGate.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`

## Deliverables
- Create stage classes or internal components: request normalizer, authorization evaluator, sandbox evaluator, capability resolver, plan builder, audit mapper.
- Keep each file below size guard or split.
- Return structured dry-run result with denied surfaces, allowed dry-run steps, gaps, and audit reference.

## Dependency Impact
This subbundle is critical. Downstream work becomes untrustworthy if this subbundle is closed with report-only proof, source-light changes, or weak boundary tests.

## Validation Depth
- Focused unit/integration tests for the changed behavior.
- Source assertions for exact files touched.
- Anti-stub scan.
- Boundary scan for Core dependency drift, reflection discovery, fallback selector, self-registration, side-effect APIs, and bundle-path coupling.
- Code-first ratio update.

## Implementation Steps
1. Inspect the exact source references before editing.
2. Implement the coherent source/test change owned by this subbundle.
3. Keep files small and split when responsibilities diverge.
4. Run focused validation.
5. Record concise proof in `reviews/01-execution-report.md` and, if this is a critical closure, one concise manifest.

## Scope Exceptions
Execution-capable side effects remain outside scope. The only permitted execution-host behavior is dry-run planning and structured denial.

## Do Not Do
- Do not execute effects.
- Do not introduce `object` payload dispatch.
- Do not use reflection discovery.
- Do not add large proof boilerplate.
- Do not mark the subbundle complete without source/test changes.

## Acceptance Checklist
- Source/test changes are materially larger than proof edits for this subbundle.
- New or changed behavior has tests.
- Process Core stays generic and dependency-clean.
- No execution-capable side effect is introduced.
- No fallback selector, reflection discovery, or driver self-registration is introduced.

## Proof Required
- Changed-file list with SHA or git diff references.
- Test command transcript.
- Source/boundary scan transcript.
- Code-first ratio update.
- Semantic positive proof and adversarial negative proof.

## Browser Validation Logging
N/A unless Razor/UI routes/components are changed. If UI changes, use large desktop only and record route, viewport, Playwright actions, assertions, screenshot paths, and result.

## Progression Gate
Do not proceed to the next subbundle until this subbundle has real source/test changes, passing focused validation, and no architecture boundary regressions.

## Suggested Agent Prompt
Implement SB03 as a code-first production/test change. Keep proof concise. Preserve Process Core genericity, deny all execution-capable effects, and keep the runtime-host roadmap explicit.
