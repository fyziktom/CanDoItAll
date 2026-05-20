# SB05 - Claim-aware Dream Grouping And Structured Synthesis

## Status

- Status: `Ready`
- Criticality: `Critical`
- Execution order: `SB05`

## Objective

Replace mode-plus-primary-key claim grouping and string-join dream synthesis with claim-aware structured synthesis.

## Covered Inputs

- R-06
- R-07
- R-16

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB05/semantic-invariants.*` before closure.

## Exact Source References

- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamSynthesis.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Build claim signatures from normalized subject, predicate/operator, object, condition/scope, and claim kind.
- Separate unrelated claims even when they share the same cluster primary key.
- Produce structured aggregate claim text with conclusion, supporting observations, conditions, caveats, and evidence roles.
- Preserve source maps per synthesized statement/claim group.

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

- Replace BuildClaimSignature so it uses claim text/slots rather than only mode and primary key.
- Introduce deterministic claim-slot extraction for current ClaimRecord fields and fallback text parsing.
- Replace common-prefix/string-join synthesis with a structured claim synthesis result.
- Ensure canonical text is not just a copied representative line or concatenated unrelated claims.
- Add tests for unrelated claims in one cluster, complementary claims in one group, and caveated synthesis.

## Do Not Do

- Do not call an external LLM in unit-test acceptance path.
- Do not join unrelated claims with commas and call it synthesis.
- Do not drop source maps when claims are split.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics.
- Semantic invariant contract exists and is cited by the proof manifest.
- Failing-first and passing transcripts exist for targeted tests.
- Changed source files are hashed and mapped to invariant IDs.
- No economic-governance scope creep is introduced.

## Proof Required

- Failing-first/passing transcript for unrelated claim separation.
- Passing transcript for complementary claim synthesis.
- Changed-file hashes for dream synthesis/consolidation files.

## Browser Validation Logging

- Backend-only unless this subbundle changes UI routes/components; if UI changes, add Playwright MCP evidence and screenshots.

## Progression Gate

- Do not proceed to the next subbundle until this subbundle's proof manifest, semantic invariant contract, targeted transcripts, anti-stub audit, and downstream dependency checks are complete.

## Suggested Agent Prompt

Implement SB05 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
