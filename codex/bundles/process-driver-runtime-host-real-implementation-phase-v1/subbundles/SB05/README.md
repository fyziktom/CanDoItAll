# SB05: Static capability provider/catalog boundary

## Status
Prepared.

## Objective
Turn current capability catalog into a controlled provider boundary without self-discovery.

## Covered Inputs
- `inputs/00-original-request.md`
- `analysis/01-real-code-review.md`
- `analysis/02-code-vs-bundle-churn.md`
- `analysis/04-gap-analysis.md`

## Prerequisites
- Previous subbundle closure gate passed.
- If any previous gate is blocked, stop and record the blocker instead of continuing.

## Exact Source References
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationHostCapabilityCatalog.cs`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions/Gateway/ProcessDriverVerificationGatewayLaneRules.cs`
- `repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/README.md`

## Deliverables
- Define explicit capability provider contracts or descriptors.
- Add static provider implementation for current verification lanes and dry-run gate.
- Prove no reflection discovery, self-registration, fallback selector, or driver package auto-registration.

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
- Do not let driver packages register themselves.
- Do not use assembly scanning to discover capabilities.
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
Implement SB05 as a code-first production/test change. Keep proof concise. Preserve Process Core genericity, deny all execution-capable effects, and keep the runtime-host roadmap explicit.
