# SB06 - Deep Entailment, Contradiction, And Calibrated Apply

## Status

- Status: `Completed`
- Criticality: `Critical`
- Execution order: `SB06`

## Objective

Strengthen dream validation beyond lexical overlap and prevent overconfident aggregate application.

## Covered Inputs

- R-08
- R-16

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB06/semantic-invariants.*` before closure.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateMemoryApplicator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryAggregateConfidenceCalibrator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityAlgorithmOptions.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Add rule-backed entailment checks for negation, numeric values, temporal order, conditions, actor/action roles, scope, and required/optional modality.
- Return claim-level validation issue details with support and contradiction explanations.
- Route unsupported/ambiguous claim groups to review instead of approval.
- Calibrate aggregate apply confidence based on validation depth, evidence diversity, and claim complexity.

## Dependency Impact

- Upstream invariants from earlier subbundles must remain green.
- Downstream cognitive-memory services that consume changed contracts, entities, options, or generated records must be retested.
- Persistence changes require SQLite and PostgreSQL migration/model-snapshot proof where applicable.

## Validation Depth

- Add or use failing-first semantic tests for the owned invariants.
- Add targeted passing tests and at least one adversarial negative test.
- Run anti-stub audit against changed production files.
- For backend-only changes, browser validation can be N/A with an explicit reason; UI changes require Playwright evidence.

## Implementation Steps

- Define deterministic semantic operators used by the validator.
- Add adversarial negative tests for each operator family.
- Update validator decision logic so lexical overlap alone is never sufficient for risky operator-bearing claims.
- Update apply calibration to account for validation issue count, operator-bearing claims, source independence, and review-needed states.

## Do Not Do

- Do not only add more token stopwords.
- Do not hard-code one approval-bypass phrase family.
- Do not approve an operator-bearing claim without operator support proof.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics.
- Semantic invariant contract exists and is cited by the proof manifest.
- Failing-first and passing transcripts exist for targeted tests.
- Changed source files are hashed and mapped to invariant IDs.
- No economic-governance scope creep is introduced.

## Proof Required

- Operator-level failing-first/passing tests.
- Transcript showing unsupported numeric/negated/temporal claims route to review or rejection.
- Anti-stub audit checking for exact fixture phrase branches.

## Browser Validation Logging

- Backend-only unless this subbundle changes UI routes/components; if UI changes, add Playwright MCP evidence and screenshots.

## Progression Gate

- Passed. `bundle://proof/SB06/manifest.md` records the semantic invariant contract, failing-first baseline, passing targeted tests, regression transcript, changed-file hashes, anti-stub audit, no-migration proof, and downstream dependency checks.

## Suggested Agent Prompt

Implement SB06 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
