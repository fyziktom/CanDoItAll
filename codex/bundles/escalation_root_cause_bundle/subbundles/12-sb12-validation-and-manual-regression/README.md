# SB12 - Validation And Manual Regression

## Status

- `Completed`
- Critical foundation: yes

## Objective

Prove the full escalation repair with unit, integration, template, architecture, and manual/equivalent process validation. The final proof must close the blocked 5032 incident class and the broader process/artifact template scope.

## Covered Inputs

- GPTPro validation checklist.
- REQ-016, REQ-017, REQ-018, REQ-019, REQ-020.
- User requirement that the bundle solve similar trouble across templates and artifacts.

## Prerequisites

- SB01 through SB11 complete or explicitly blocked with accepted proof.
- All critical subbundle proof manifests and semantic invariant files present.
- Current source references refreshed.

## Exact Source References

- `bundle://codex/09-test-and-validation-checklist.md`
- `bundle://tests/manual-process-validation.md`
- `bundle://tests/regression-test-matrix.md`
- `bundle://reviews/csharp-architecture-gate.md`
- `repo://CanDoItAll.slnx`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeToolPreflightServiceTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProcessApiIntegrationTests.cs`

## Deliverables

- Targeted test suite transcript for all changed runtime, adapter, template, and Workbench tests.
- Full template validation transcript over all process and artifact templates.
- Manual blocked 5032 rerun evidence or equivalent local reproduction with explicit environment blocker if live rerun is impossible.
- CodeAnalytics refresh and dependency-cycle check after implementation.
- Completed `reviews/01-execution-report.md` with subbundle gate results, raw note closure, semantic adequacy evidence, and proof manifest links.
- Completed bundle validator run for `--stage completed`.

## Dependency Impact

- This is the final closure phase.
- Any failure reopens the owning subbundle and its downstream phases.

## Validation Depth

- Critical final validation with behavior, architecture, template, and manual process proof.
- Semantic proof must include shallow-pass traps and adversarial negatives.

## Implementation Steps

1. Run targeted unit tests for launch resolver, completion gates, recovery classifier, packets, artifact acceptance, subprocess bridge, tool-plan guard, schema validation, capability assignment, and executor.
2. Run targeted integration tests for project structure and process runtime scenarios.
3. Run strict full-pack template validation.
4. Run solution build and broader test suite required by changed project blast radius.
5. Reproduce the incident locally: empty `.slnx`, generated project, missing helper receipt, safe/idempotent gate failure.
6. Prove first attempt routes to `SafeRetry` / `CurrentStepRetry` with targeted packet.
7. Prove repeated identical fingerprint escalates after budget with child root cause and attempted repair proof.
8. Rerun or inspect live blocked 5032 instance if available; otherwise record explicit blocker and equivalent reproduction evidence.
9. Refresh CodeAnalytics snapshot and confirm no new cycles or boundary violations.
10. Complete every critical proof manifest and semantic invariant contract.
11. Run completed bundle validation.
12. Close every GPTPro finding and user requirement with proof links.

## Do Not Do

- Do not close with build-only proof.
- Do not close if template audit rows are missing.
- Do not close if the live 5032 instance is unavailable without equivalent reproduction proof.
- Do not mark shallow structural validation as semantic success.

## Acceptance Checklist

- [x] Unit tests pass for all new services and policies.
- [x] Integration tests pass for incident-equivalent flow.
- [x] Full template validation passes.
- [x] Manual 5032 or equivalent proof shows safe retry before escalation.
- [x] Budget-exhausted escalation includes root-cause packet.
- [x] CodeAnalytics/dependency evidence is refreshed.
- [x] Completed bundle validator passes.

## Closure Evidence

- Focused unit sweep passed: 237 passed, 0 failed in `proof/SB12/transcripts/01-focused-unit-tests.txt`.
- Strict template validation passed: 2 passed, 0 failed in `proof/SB12/transcripts/02-template-validation.txt`.
- Integration boundary validation passed: 4 passed, 0 failed in `proof/SB12/transcripts/03-integration-tests.txt`.
- Equivalent 5032 incident regression passed: 5 passed, 0 failed in `proof/SB12/transcripts/04-equivalent-incident-regression.txt`.
- Live 5032 was intentionally not mutated; equivalent proof is recorded in `proof/SB12/transcripts/05-manual-5032-equivalent-note.txt`.
- Solution build passed with 0 errors and known `NU1903` advisory warnings in `proof/SB12/transcripts/06-solution-build.txt`.
- CodeAnalytics snapshot `snap-20260708214607-6650a5f9` reported `cycles: []` for the scoped process graph.
- Anti-stub and source assertion transcripts are present in `proof/SB12/transcripts/08-anti-stub-audit.txt` and `proof/SB12/transcripts/09-source-assertions.txt`.

## Proof Required

- `proof/SB12/manifest.md`
- `proof/SB12/semantic-invariants.md`
- Test transcripts for targeted and broader validation.
- Template validation transcript.
- Manual/equivalent incident transcript.
- CodeAnalytics refresh evidence.
- Completed bundle validation transcript.

## Browser Validation Logging

- `N/A` unless implementation changes process detail UI; if UI changes, capture process detail route at desktop and narrow viewport.

## Progression Gate

- Final closure is allowed only when every critical subbundle has manifest proof and this subbundle proves the incident and broader template scope.

## C# Architecture Impact

Final architecture review across all changed process runtime, adapter, template, Workbench, and test code.

## Boundary Ownership

Validation must confirm each changed service remains in its intended boundary and no runtime/module/template dependency inversion was introduced.

## Dependency Direction

No new cycles or runtime-to-module dependencies are acceptable.

## Pattern Decision

No new patterns should be introduced in final validation; reopen owning subbundle if design changes are needed.

## Testability Contract

Proof must include positive behavior, adversarial negative behavior, and anti-stub checks.

## Partial Class Policy

Final review must flag adapter partial growth that hides new responsibilities.

## Architecture Proof Required

- Completed `reviews/csharp-architecture-gate.md`.
- CodeAnalytics refresh and dependency evidence.
- Review findings resolved or explicitly blocked.

## Suggested Agent Prompt

```text
Execute SB12 only after SB01-SB11 are complete. Run targeted tests, strict template validation, architecture checks, manual 5032 or equivalent reproduction, and completed bundle validation. Do not close on build-only proof.
```
