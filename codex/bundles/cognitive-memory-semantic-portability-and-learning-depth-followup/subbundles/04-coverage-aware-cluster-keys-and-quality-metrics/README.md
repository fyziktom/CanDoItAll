# SB04 - Coverage-aware Cluster Keys And Quality Metrics

## Status

- Status: `Ready`
- Criticality: `Important`
- Execution order: `SB04`

## Objective

Prevent cluster keys and primary keys from representing only a small subset of cluster members.

## Covered Inputs

- R-05
- R-14
- R-16

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB04/semantic-invariants.*` before closure.

## Exact Source References

- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterFormation.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityEntities.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityEntityConfigurations.cs
- /mnt/data/cogmem_review/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Add support count and coverage ratio to cluster keys or a persisted cluster-key support table.
- Require primary keys to cover a configurable majority of memory-record members or mark the cluster needs review.
- Expose warnings when a key is pair-local rather than cluster-representative.
- Add migration if persistence schema changes.

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

- Change BuildSharedClusterKeys to compute coverage metadata, not only first matching key.
- Update SelectPrimaryClusterKey to use family priority plus coverage score.
- Update quality metrics and warnings to describe coverage failures.
- Add tests for a 4-member cluster where one semantic key covers only 2 members and must not become the primary key.

## Do Not Do

- Do not hide low-coverage keys entirely if they are useful diagnostics.
- Do not make every key require 100 percent coverage.
- Do not break existing persisted cluster idempotency without migration handling.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics.
- Semantic invariant contract exists and is cited by the proof manifest.
- Failing-first and passing transcripts exist for targeted tests.
- Changed source files are hashed and mapped to invariant IDs.
- No economic-governance scope creep is introduced.

## Proof Required

- Targeted passing tests for low-coverage primary rejection and high-coverage primary acceptance.
- Migration/model snapshot proof if schema is changed.
- Anti-stub audit for cluster planner and formation files.

## Browser Validation Logging

- Backend-only unless this subbundle changes UI routes/components; if UI changes, add Playwright MCP evidence and screenshots.

## Progression Gate

- Do not proceed to the next subbundle until this subbundle's proof manifest, semantic invariant contract, targeted transcripts, anti-stub audit, and downstream dependency checks are complete.

## Suggested Agent Prompt

Implement SB04 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
