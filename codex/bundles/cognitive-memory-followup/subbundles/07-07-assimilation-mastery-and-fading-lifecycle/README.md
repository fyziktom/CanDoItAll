# SB07 - Assimilation, mastery, and fading lifecycle

## Status

- Status: `Completed`

## Objective

Implement the student-professor internalization loop: compare, reinforce, assimilate, and fade direct professor quotes only after mastery.

## Covered Inputs

- Current assimilation/fade is manual and only checks direct memory vs derived memory plus basic independent support.
- Current fading changes capture state but does not necessarily demote the direct quote memory.
- User wants professor guidance remembered temporarily and forgotten after knowledge becomes internalized.

## Prerequisites

- SB06 structured anchors completed.
- SB05 precise dream source maps available.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedEntities.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryAdvancedContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamValidator.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Add assimilation evaluator with mastery criteria.
- Require independent non-descendant support, repeated successful use, and dream/cluster integration before assimilation.
- Reject support that descends from the same professor anchor through aggregate/provenance lineage.
- Add scheduled/manual assimilation scan service.
- Fade direct professor quote by demoting/retiring direct capture memory while keeping reference-on-demand lineage.

## Dependency Impact

- Feeds SB08 recall quality and SB10 end-to-end proof.
- Requires SB05 statement lineage to detect descendant-only support.

## Validation Depth

- Direct capture cannot assimilate itself or descendants-only aggregates.
- Assimilation requires at least one independent non-anchor source and repeated usage/integration count or approved review decision.
- Fading demotes direct capture from normal recall while preserving resolver lineage.
- Active anchors move through Comparing/Assimilated/Faded with persisted timestamps/reasons.

## Implementation Steps

- Implement `ICognitiveMemoryProfessorAssimilationEvaluator` or equivalent.
- Add anchor lifecycle fields for mastery count, last comparison, assimilation reason, fade reason, and retired direct memory id if needed.
- Add lineage recursion to detect support descended from the same anchor.
- Add scheduled/manual assimilation scan service with deterministic tests.
- Update reference resolver to retain faded professor lineage without default recall pollution.

## Do Not Do

- Do not allow a machine-generated aggregate derived only from the anchor to count as independent support.
- Do not fade anchors by state flag only while leaving direct capture memory active in recall.
- Do not require the user to manually click every assimilation if automatic criteria are satisfied.

## Acceptance Checklist

- Descendant-only support is rejected.
- Independent support plus repeated use allows assimilation.
- Fading demotes/retires direct capture from ordinary recall.
- Reference resolver can still explain faded professor lineage on demand.
- Lifecycle scan works without manual direct `MarkAssimilatedAsync` calls in the normal happy path.

## Proof Required

- `proof/SB07/manifest.md`.
- Targeted lifecycle tests with failing-first and passing transcripts.
- Source-level assertion for non-descendant support recursion and direct quote demotion.
- Anti-stub scan transcript.

## Browser Validation Logging

- Run UI proof if curator tab displays lifecycle state or assimilation actions.

## Progression Gate

- SB08 cannot close until faded anchors are excluded from default recall but resolvable on demand.
- If assimilation remains purely manual with no mastery criteria, SB07 remains incomplete.

## Completion Proof

- Proof manifest: `proof/SB07/manifest.md`
- Passing targeted lifecycle transcript: `proof/SB07/transcripts/passing-targeted-lifecycle-tests.txt`
- Passing professor lifecycle regression transcript: `proof/SB07/transcripts/passing-professor-lifecycle-regression-tests.txt`
- Source assertions: `proof/SB07/transcripts/source-assertions.txt`
- Anti-stub audit: `proof/SB07/transcripts/anti-stub-audit.txt`
- Closure decision: `Completed - evaluator-driven assimilation rejects descendant-only support, scan assimilation requires repeated use plus integration, and fading retires the direct quote memory while preserving reference lineage.`

## Suggested Agent Prompt

Implement SB07. Add mastery-based professor assimilation and fading with non-descendant support checks and reference-preserving demotion.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
