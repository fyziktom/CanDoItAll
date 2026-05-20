# SB10 - Maintainability, Options, And Final Red-team Closure

## Status

- Status: `Ready`
- Criticality: `Critical`
- Execution order: `SB10`

## Objective

Refactor service boundaries, replace static option access, and run end-to-end semantic red-team closure.

## Covered Inputs

- R-14
- R-15
- R-16
- R-17

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB10/semantic-invariants.*` before closure.

## Exact Source References

- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityAlgorithmOptions.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Refactor remaining oversized services into orchestration plus domain collaborators without behavior regression.
- Inject algorithm options through DI/config instead of static Current access in runtime services.
- Add direct collaborator tests for the new boundaries.
- Run full end-to-end red-team scenario: wrong memory, professor correction, anchor, dream comparison, independent support, accepted use, assimilation/fade, recall brief, reference resolution.
- Run scope guard proving no economic governance was implemented.

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

- Extract repository/persistence helpers where service methods are doing both DB orchestration and domain logic.
- Introduce typed options registration and test overrides.
- Add maximum-size/responsibility review to the red-team report.
- Run targeted tests, broad cognitive-memory tests, completed-stage bundle validation, fake-proof fixture validation, anti-stub audit, and economic-governance scope guard.
- Write final red-team verdict with unresolved residual risks if any remain.

## Do Not Do

- Do not perform risky big-bang rewrites without preserving tests.
- Do not remove deterministic fallback behavior.
- Do not claim completion if large service responsibilities remain mixed without an explicit accepted residual-risk entry.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics.
- Semantic invariant contract exists and is cited by the proof manifest.
- Failing-first and passing transcripts exist for targeted tests.
- Changed source files are hashed and mapped to invariant IDs.
- No economic-governance scope creep is introduced.

## Proof Required

- Completed-stage bundle validation transcript.
- Targeted and broad test transcripts.
- Fake-proof fixtures still fail transcript.
- Red-team verdict report.
- Service-size/responsibility report.
- Economic-governance scope guard transcript.

## Browser Validation Logging

- Backend-only unless this subbundle changes UI routes/components; if UI changes, add Playwright MCP evidence and screenshots.

## Progression Gate

- Do not proceed to the next subbundle until this subbundle's proof manifest, semantic invariant contract, targeted transcripts, anti-stub audit, and downstream dependency checks are complete.

## Suggested Agent Prompt

Implement SB10 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
