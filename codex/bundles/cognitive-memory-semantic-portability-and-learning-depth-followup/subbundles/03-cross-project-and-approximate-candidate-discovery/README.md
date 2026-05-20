# SB03 - Cross-project And Approximate Candidate Discovery

## Status

- Status: `Ready`
- Criticality: `Critical`
- Execution order: `SB03`

## Objective

Make cross-project weekly clustering real and add bounded approximate candidate discovery beyond exact shared keys.

## Covered Inputs

- R-03
- R-04
- R-16

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB03/semantic-invariants.*` before closure.

## Exact Source References

- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityAlgorithmOptions.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Add explicit cluster planning scope semantics: project-only, global, cross-project, and policy-constrained cross-project.
- Allow cross-project candidate pairs only for modes/scopes that opt in and only when access policy permits both sides.
- Add bounded approximate neighbor generation for records with no exact shared strong keys.
- Expose candidate discovery metrics: exact pairs, approximate pairs, skipped pairs, budget reached, policy-blocked pairs.

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

- Extend request/options/contracts to represent cross-project scope safely.
- Refactor AddPair so project mismatch is not a hard-coded global rejection; move it to a policy-aware scope rule.
- Add approximate semantic neighbor generation over all eligible records, bounded per record and globally.
- Keep deterministic fallback if embeddings are unavailable; optional embedding provider may be injected but not required.
- Retest ProjectNightly, CrossProjectWeekly, restricted-source, and budget scenarios.

## Do Not Do

- Do not make all clustering cross-project by default.
- Do not remove access/redaction guards.
- Do not rely only on over-fanout exact-key fallback.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics.
- Semantic invariant contract exists and is cited by the proof manifest.
- Failing-first and passing transcripts exist for targeted tests.
- Changed source files are hashed and mapped to invariant IDs.
- No economic-governance scope creep is introduced.

## Proof Required

- Failing-first and passing transcripts for cross-project weekly and no-exact-key paraphrase tests.
- Metrics/assertions showing approximate pairs are generated for paraphrases.
- Negative transcript showing restricted cross-project pair is blocked.

## Browser Validation Logging

- Backend-only unless this subbundle changes UI routes/components; if UI changes, add Playwright MCP evidence and screenshots.

## Progression Gate

- Do not proceed to the next subbundle until this subbundle's proof manifest, semantic invariant contract, targeted transcripts, anti-stub audit, and downstream dependency checks are complete.

## Suggested Agent Prompt

Implement SB03 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
