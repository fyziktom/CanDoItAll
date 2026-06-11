# SB01: Current-state release decision audit

## Status
- Current status: Completed

## Objective
Reclassify the latest `not merge-ready` decision into functional blockers, live-provider blockers, and advisory code/proof churn policy.

## Covered Inputs
- RN-001: Check whether processes now work like before.
- RN-004: Stabilize process functionality before further runtime extraction.

## Prerequisites
- Prepared-stage bundle validation must pass after repair.
- Previous stabilization reports must be available under `repo://codex/bundles/process-runtime-stabilization-release-closure-v1`.

## Exact Source References
- `repo://codex/bundles/process-runtime-stabilization-release-closure-v1/reviews/01-execution-report.md`
- `repo://codex/bundles/process-runtime-stabilization-release-closure-v1/reviews/02-release-decision.md`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs`

## Deliverables
- Release-blocker classification table.
- Focused guard test result or source-backed explanation that no source change is needed.
- Bundle-start commit SHA and current HEAD SHA.

## Dependency Impact
- SB02 may start only after release classification is clear.
- If code-first ratio is still treated as functional runtime failure, SB06 final decision cannot be trusted.

## Validation Depth
- Entry gate: verify source references exist and prior release decision can be read.
- Closure gate: transcript-backed release audit, source scan for production coupling to concrete bundle paths, and proof manifest.
- Semantic Adequacy Gate: record shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note closure in `bundle://proof/SB01/semantic-invariants.md`.

## Implementation Steps
- Read latest release decision.
- Add or update a small source-backed release classifier test if current code treats ratio failure as a functional runtime blocker.
- Ensure code-first ratio remains advisory/anti-churn unless source/test evidence is missing.
- Record exact current head SHA and bundle-start SHA.

## Scope Exceptions
- None planned.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- Release blocker classification exists.
- Ratio failure is not confused with runtime failure.
- No source/test coupling to transient bundle paths except intentional guard fixtures.

## Proof Required
- Focused guard test transcript under `bundle://proof/SB01/transcripts/`.
- Source scan transcript for concrete `codex/bundles/<name>` production coupling.
- Concise release-blocker classification table.
- `bundle://proof/SB01/manifest.md` with changed-file hashes and portable artifact references.
- `bundle://proof/SB01/semantic-invariants.md` with invariant IDs cited by transcripts.

## Browser Validation Logging
- N/A: SB01 has no browser-visible behavior.

## Progression Gate
- SB02 may start only after the release blocker taxonomy is recorded and SB01 closure proof exists.

## Suggested Agent Prompt
- Audit the prior release decision and current tests. Repair only if code treats advisory ratio failure as a functional blocker. Record concise transcript-backed proof.
