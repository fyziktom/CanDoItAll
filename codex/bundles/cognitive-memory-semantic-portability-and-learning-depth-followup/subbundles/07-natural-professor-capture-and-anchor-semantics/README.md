# SB07 - Natural Professor Capture And Anchor Semantics

## Status

- Status: `Completed`
- Criticality: `Critical`
- Execution order: `SB07`

## Objective

Make curator/professor learning robust for natural dialogue and separate professor anchors from ordinary curator captures.

## Covered Inputs

- R-09
- R-10
- R-16

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB07/semantic-invariants.*` before closure.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorExtraction.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallDataLoading.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs

## Deliverables

- Capture short corrections, explicit professor captures, Q&A teaching where curator response contains the answer, examples/counterexamples, and scope corrections.
- Create structured professor anchors with claims, target scope, misconception, source utterances, confidence, and capture type.
- Do not assign Active professor anchor state to ordinary non-professor curator captures.
- Ensure default recall excludes active professor direct quote memories unless explicitly requested for references or review.

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

- Remove the hard user-message length gate or replace it with claim-quality gating.
- Do not skip professor extraction merely because ExplicitCaptureKind is set; use explicit kind as a hint.
- Extract from curator response and previous turns when the user asks and the curator/professor answers.
- Add a separate lifecycle marker for non-professor captures or nullable/None anchor state semantics.
- Retest default recall exclusion of active anchors and explicit reference inclusion.

## Do Not Do

- Do not require words like remember, must, gate, or approval for every professor capture.
- Do not treat every trusted curator capture as a professor anchor.
- Do not apply broad corrections to multiple recalled memories without explicit target review.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics.
- Semantic invariant contract exists and is cited by the proof manifest.
- Failing-first and passing transcripts exist for targeted tests.
- Changed source files are hashed and mapped to invariant IDs.
- No economic-governance scope creep is introduced.

## Proof Required

- Failing-first/passing tests for short correction, Q&A teaching, explicit capture with professor extraction, examples/counterexamples, and non-professor capture anchor state.
- Recall tests proving active anchors are hidden by default.
- Anti-stub audit for exact phrase hard-coding.

## Browser Validation Logging

- Backend-only unless this subbundle changes UI routes/components; if UI changes, add Playwright MCP evidence and screenshots.

## Progression Gate

- Passed. `bundle://proof/SB07/manifest.md` and `bundle://proof/SB07/semantic-invariants.md` cite targeted passing tests, professor lifecycle regressions, default recall exclusion, changed-file hashes, source assertions, anti-stub audit, no-migration proof, and prepared-stage validation.

## Suggested Agent Prompt

Implement SB07 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
