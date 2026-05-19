# 06 - Retrieval Synthesis And Reference On Demand

## Status

- Status: `Ready`

## Objective

Change memory consumption from raw retrieved sections into concise synthesized memory briefs that are useful to the caller, while preserving reference-on-demand provenance for every synthesized statement.

## Covered Inputs

- User requirement that memory should formulate/combine useful information, not just pass thoughts forward.
- User requirement that scores/references should not flood normal answers, but detailed references must be available on request.
- Current recall context pack and agent package behavior.

## Prerequisites

- Subbundle 05 validation/caveat metadata available.
- Existing recall channels and score traces preserved.
- Aggregate statement/source maps available from Subbundle 04.

## Exact Source References

- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallServices.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallEvaluation.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallContextPackBuilder.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallMappingAndTypes.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAgentContextPackage.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAnswerGateService.cs
- /mnt/data/cogmem_repo/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryRecallOrchestratorTests.cs

## Deliverables

- Fix focus selection so `SideContext` and review-worthy candidates are not automatically converted to `Selected`.
- Add `ICognitiveMemoryRecallSynthesisService` or equivalent that turns recall results into a concise brief for a consumer/task.
- Add synthesized statement records/DTOs with hidden provenance IDs.
- Add `ICognitiveMemoryReferenceResolver` or equivalent reference-on-demand API.
- Update agent context package so it can provide synthesized briefs and optionally include diagnostics only when requested.
- Tests proving concise synthesis, no score/reference flood by default, and reference expansion by statement.

## Dependency Impact

- Subbundle 07 uses synthesis and reference resolver as end-to-end proof.
- Agent workflow integration may need minor DTO changes.

## Validation Depth

- Tests must prove a multi-memory answer becomes a concise combined brief.
- Tests must prove each statement resolves to source memories/items/evidence allowed by policy.
- Tests must prove restricted references are withheld or redacted for unauthorized callers.
- Tests must prove score traces remain available diagnostically but are not included in normal text.
- Tests must cover SideContext not being promoted to Selected.

## Implementation Steps

1. Fix recall focus selection decision preservation.
2. Introduce synthesis contracts and deterministic synthesizer for tests.
3. Build an evidence graph from selected candidates and aggregate/source maps.
4. Generate concise statements with caveats and source map IDs.
5. Add reference resolver API/DTO.
6. Update agent context package to prefer synthesized brief over raw section dump.
7. Add tests and docs.

## Scope Exceptions

- Full natural-language elegance can improve later; the first pass must prioritize correctness, concision, and provenance.
- Diagnostic context pack can remain available for debugging.

## Do Not Do

- Do not remove raw recall diagnostics; separate them from normal consumer-facing output.
- Do not include score traces or raw references in the default agent-facing brief.
- Do not synthesize statements that cannot resolve references.

## Acceptance Checklist

- [ ] SideContext promotion issue is fixed and tested.
- [ ] Synthesis service produces concise multi-memory briefs.
- [ ] Reference resolver returns source maps on demand.
- [ ] Default agent package does not flood scores/references.
- [ ] Redaction/access policy is enforced during reference expansion.

## Proof Required

- Unit tests for focus selection fix.
- Unit tests for synthesis and reference resolver.
- Example synthesized brief and reference expansion payload.
- Agent package test showing concise output.


## Browser Validation Logging

- Record browser validation only when this subbundle changes UI-rendered behavior.
- If no UI changes are made, record `Not applicable - API/domain-only change` in the execution report.
- If UI changes are made, capture route, viewport, screenshots, console errors, and Playwright MCP trace/evidence.

## Progression Gate

- Do not proceed to the next subbundle until all acceptance checks pass or a blocker is documented with a safe rollback/repair plan.

## Suggested Agent Prompt

Use the shared implementation prompt, then execute this subbundle only. Read the exact source references first, implement the deliverables, run the required tests, and update the execution report with proof before moving on.
