# 04 - Aggregate Memory Claim Provenance

## Status

- Status: `Completed`

## Objective

Persist aggregate memories and aggregate candidates with claim-level provenance so generated synthesis can always be traced back to concrete memories, source items, claims, and evidence anchors.

## Covered Inputs

- User requirement that a synthesized statement can later explain which thought/reference produced it.
- Current generic `is-grounded-by` claim creation.
- Existing evidence/source link records.

## Prerequisites

- Subbundle 03 aggregate candidates exist.
- Existing memory/claim/source/evidence records reviewed.
- Access/redaction policy is carried through from source to aggregate.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationCandidateApplicator.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Foundation\CognitiveMemoryEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Neuro\CognitiveMemoryNeuroFoundationEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Neuro\CognitiveMemoryMutationAuthority.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiService.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryConsolidationEngineTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CognitiveMemoryPersistenceModelTests.cs

## Deliverables

- Aggregate candidate payload with statements/claims and per-claim source maps.
- Aggregate memory application path that creates memory records, claim records, relation records, source links, and evidence links at statement granularity where possible.
- Provenance DTOs for later reference resolution.
- Generated synthesis marked with appropriate origin/mode/algorithm/profile version.
- Tests proving every aggregate statement has an allowed provenance path.

## Dependency Impact

- Subbundle 05 validates aggregate claims and source maps.
- Subbundle 06 uses statement/source maps for reference-on-demand.
- Subbundle 07 verifies end-to-end provenance.

## Validation Depth

- Tests must fail if an aggregate statement has no source map.
- Tests must cover a single-source aggregate, multi-source aggregate, contradiction aggregate, and restricted-source aggregate.
- Tests must prove source maps survive mutation application and review approval.

## Implementation Steps

1. Design aggregate candidate payload schema with statement IDs and source maps.
2. Extend applicator or add aggregate-specific applicator.
3. Create real claim records with meaningful subject/predicate/object where feasible.
4. Link claims to source items/evidence anchors.
5. Add relation records for supports/refines/supersedes/contradicts when detected.
6. Add tests and docs.

## Scope Exceptions

- Perfect natural-language claim extraction is not required in the first pass; deterministic extraction is acceptable if provenance is reliable.
- Advanced ontology modeling can be deferred if source maps are complete.

## Do Not Do

- Do not create aggregate memories that only point to a source hash without statement-level mapping.
- Do not treat generated synthesis as original source text.
- Do not leak restricted source content into unrestricted aggregate text.

## Acceptance Checklist

- [x] Aggregate payload has statement IDs and source maps.
- [x] Activated aggregate memories create claim/evidence/source links.
- [x] Generated synthesis origin/version is recorded.
- [x] Provenance survives review approval.
- [x] Tests cover missing-source-map rejection.

## Proof Required

- Unit tests for aggregate applicator.
- Persistence tests for aggregate claim/source map records.
- Example aggregate memory with at least two statements and references.


## Browser Validation Logging

- Record browser validation only when this subbundle changes UI-rendered behavior.
- If no UI changes are made, record `Not applicable - API/domain-only change` in the execution report.
- If UI changes are made, capture route, viewport, screenshots, console errors, and Playwright MCP trace/evidence.

## Progression Gate

- Do not proceed to the next subbundle until all acceptance checks pass or a blocker is documented with a safe rollback/repair plan.

## Suggested Agent Prompt

Use the shared implementation prompt, then execute this subbundle only. Read the exact source references first, implement the deliverables, run the required tests, and update the execution report with proof before moving on.
