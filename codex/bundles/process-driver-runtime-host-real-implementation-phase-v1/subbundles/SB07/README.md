# SB07: Manager/operator runtime-host readback

## Status
Prepared.

## Objective
Expose runtime-host status, dry-run plans, audit records, and denials through operator-ready readback.

## Covered Inputs
- `inputs/00-original-request.md`
- `analysis/01-real-code-review.md`
- `analysis/02-code-vs-bundle-churn.md`
- `analysis/04-gap-analysis.md`

## Prerequisites
- Previous subbundle closure gate passed.
- If any previous gate is blocked, stop and record the blocker instead of continuing.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationCommandService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHostStatus.cs`
- `repo://src/CanDoItAll.Modules.Processes/README.md`

## Deliverables
- Add API/service DTOs for status, capability catalog, audit query, dry-run plan readback, denial category/code, evidence counts, no-mutation flags.
- If UI route changes, add large desktop Playwright proof. If not, add API/JSON proof and explicit UI gap.
- Add authorization/read-only identity guards.

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
- Do not expose mutation commands.
- Do not use UI label-only proof.
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
Implement SB07 as a code-first production/test change. Keep proof concise. Preserve Process Core genericity, deny all execution-capable effects, and keep the runtime-host roadmap explicit.
