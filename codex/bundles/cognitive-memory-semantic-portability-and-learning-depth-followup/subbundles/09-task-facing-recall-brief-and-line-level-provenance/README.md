# SB09 - Task-facing Recall Brief And Line-level Provenance

## Status

- Status: `Completed`
- Criticality: `Critical`
- Execution order: `SB09`

## Objective

Improve recall synthesis from fragment joining into task-facing answer/action/caveat planning with exact statement lineage.

## Covered Inputs

- R-13
- R-16

## Prerequisites

- Read the root README, current-state analysis, assumptions/risks, target architecture, and phase plan.
- Reopen all exact source references before changing code.
- For critical subbundles, create and maintain `proof/SB09/semantic-invariants.*` before closure.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallContextPackBuilder.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityEntities.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Introduce statement plan types: answer, action, caveat, conflict, missing-evidence, and reference-hint.
- Compose briefs from claim groups and task intent rather than first useful lines only.
- Persist line-level provenance: statement -> aggregate claim -> aggregate source maps -> original memory/source/evidence.
- Keep scores and internal diagnostics hidden by default, but available on explicit reference/debug request.
- Add omitted-detail and caveat warnings when the brief budget drops important material.

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

- Refactor RecallBriefComposer into planner, formatter, and provenance mapper.
- Use aggregate claim IDs and source refs to build statement groups; fall back to source sections only when claim IDs are absent.
- Add tests for statement lineage after aggregate expansion and after faded professor anchor reference resolution.
- Add tests proving contradictory selected memories become caveat/conflict statements, not joined answer statements.
- Ensure restricted references never expose locator/summary without explicit policy.

## Do Not Do

- Do not expose belief scores by default.
- Do not lose lineage when statements are summarized.
- Do not expand all sibling aggregate claims when only one statement is requested.

## Acceptance Checklist

- All owned requirements are implemented without downgrading semantics.
- Semantic invariant contract exists and is cited by the proof manifest.
- Failing-first and passing transcripts exist for targeted tests.
- Changed source files are hashed and mapped to invariant IDs.
- No economic-governance scope creep is introduced.

## Proof Required

- Passing tests for answer/action/caveat plan types and exact lineage.
- Reference resolver tests for aggregate and faded professor anchors.
- Anti-stub audit for recall composer/resolver.

## Browser Validation Logging

- Backend-only unless this subbundle changes UI routes/components; if UI changes, add Playwright MCP evidence and screenshots.

## Progression Gate

- Do not proceed to the next subbundle until this subbundle's proof manifest, semantic invariant contract, targeted transcripts, anti-stub audit, and downstream dependency checks are complete.

## Closure Evidence

- Semantic invariant contract: `bundle://proof/SB09/semantic-invariants.md`
- Proof manifest: `bundle://proof/SB09/manifest.md`
- Failing-first transcript: `bundle://proof/SB09/transcripts/failing-first-current.txt`
- Passing transcript: `bundle://proof/SB09/transcripts/passing-semantic-tests.txt`
- Regression transcript: `bundle://proof/SB09/transcripts/regression-tests.txt`
- Source assertions: `bundle://proof/SB09/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB09/transcripts/anti-stub-audit.txt`
- No-migration proof: `bundle://proof/SB09/transcripts/no-migration-proof.txt`
- Browser validation: N/A; backend recall synthesis, reference resolution, contracts, and unit tests only.

## Suggested Agent Prompt

Implement SB09 exactly as written. First create or update the semantic invariant contract. Then implement the smallest production changes that satisfy the invariant generally, not only the fixture. Prove with failing-first and passing transcripts, changed-file hashes, anti-stub audit, downstream checks, and red-team notes. If any invariant cannot be satisfied, mark the subbundle blocked with a precise blocker instead of weakening the requirement.
