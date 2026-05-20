# SB02 - Failing-first Adversarial Corpus For Remaining Gaps

## Status

- Status: `Ready`
- Criticality: `Critical`
- Execution order: `SB02`

## Objective

Create failing-first tests for all remaining cognitive-memory gaps before modifying production code.

## Covered Inputs

- R-03
- R-04
- R-05
- R-06
- R-07
- R-08
- R-09
- R-11
- R-13

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB02/semantic-invariants.*` before closure.

## Exact Source References

- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs

## Deliverables

- Add targeted failing-first tests for cross-project weekly clustering, paraphrase candidate discovery without exact keys, cluster key coverage, unrelated dream claim separation, deep entailment negatives, natural professor Q&A capture, event-backed mastery, and recall line-level lineage.
- Create semantic invariant contract files for SB03-SB09 that cite these tests.
- Capture one failing-first transcript with non-zero exit code before production changes.

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

- Write tests only; do not change production code in this subbundle.
- Use adversarial examples that are not the same as existing fixture strings.
- Ensure tests fail for the current implementation for the right behavioral reason.
- Record failing-first transcript and anti-stub baseline transcript.

## Do Not Do

- Do not weaken assertions to match current behavior.
- Do not use tests that only verify object counts.
- Do not make production changes in SB02.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics.
- Semantic invariant contract exists and is cited by the proof manifest.
- Failing-first and passing transcripts exist for targeted tests.
- Changed source files are hashed and mapped to invariant IDs.
- No economic-governance scope creep is introduced.

## Proof Required

- Failing-first transcript with non-zero exit code.
- Semantic invariant contracts mapped to test names.
- Production diff proof showing no production code changed in SB02.

## Browser Validation Logging

- Backend-only unless this subbundle changes UI routes/components; if UI changes, add Playwright MCP evidence and screenshots.

## Progression Gate

- Do not proceed to the next subbundle until this subbundle's proof manifest, semantic invariant contract, targeted transcripts, anti-stub audit, and downstream dependency checks are complete.

## Suggested Agent Prompt

Implement SB02 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
