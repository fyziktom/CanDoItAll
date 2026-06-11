# SB08: Release decision and red-team closure

## Status
- Status: `Blocked`

## Objective
Produce an honest merge/readiness decision.

## Covered Inputs
- REQ-008: Produce merge-readiness decision with red-team scans and no proof-only closure.

## Prerequisites
- SB07 matrix must be completed or reopened blockers must be explicit.
- All completed critical subbundles must have artifact-backed manifests and semantic invariant contracts.

## Exact Source References
- repo://codex/bundles/process-template-release-readiness-ui-live-host-closure-v1/plan/01-phase-plan.md
- repo://codex/bundles/process-template-release-readiness-ui-live-host-closure-v1/reviews/01-execution-report.md
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs
- repo://tests/CanDoItAll.Tests.Playwright

## Deliverables
- Build, test, focused integration, Playwright, live classification, source scan, and code-first ratio summary.
- Red-team scan artifact covering fake-proof resistance and forbidden implementation patterns.
- Final release decision: `Merge-ready for maf-processes-refactor -> development`, `Runtime-ready but UI/live blocked`, or `Not merge-ready`.

## Dependency Impact
- This is the final bundle closure gate.
- Any failed red-team or ratio result must mark the bundle not merge-ready or reopen the owning subbundle.

## Validation Depth
- `git diff --numstat <explicit-start-sha>...HEAD` grouped by `src/`, `tests/`, `docs/`, and `codex/bundles/`.
- Source scans for Core dependency drift, driver hooks, reflection/fallback discovery, mutation APIs, secret leakage, bundle path coupling, and large-file growth.
- Final completed-stage validator.

## Implementation Steps
1. Run build with 0 warnings/errors.
2. Run full unit tests.
3. Run focused integration matrix.
4. Run large desktop Playwright proof.
5. Classify live OpenAI proof honestly.
6. Run required source scans.
7. Run code-first ratio using explicit start SHA.
8. Produce final release decision with exact blockers if not merge-ready.

## Do Not Do
- Do not close if SB08 ratio fails.
- Do not claim live proof from skipped tests.
- Do not treat process-mock proof as live provider proof.

## Acceptance Checklist
- Build 0 warnings/errors.
- Full unit tests pass.
- Focused integration matrix passes.
- Large desktop Playwright proof passes or UI blocker is explicit.
- Source scans pass.
- Code-first ratio passes.
- Raw note closure is complete note by note.

## Proof Required
- `bundle://proof/SB08/manifest.md`
- `bundle://proof/SB08/semantic-invariants.md`
- Red-team verifier artifact path.
- Build/test/source-scan/code-first ratio transcripts.

## Browser Validation Logging
- Cite SB07 browser analytics or rerun large desktop proof if stale, with route, viewport, Playwright MCP evidence, screenshots, visual review result, and pass/fail.

## Progression Gate
- Bundle may close only after completed-stage validator passes and the execution report, proof manifests, raw-note closure, and root status all agree.

## Suggested Agent Prompt
Run only final red-team closure for SB08, compute the code-first ratio, audit all proof manifests, update raw-note closure, run completed-stage validation, and record the final release decision.
