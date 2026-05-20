# 09 End-To-End Neurocognitive Quality Proof

## Status

- `Ready`

## Objective

- Prove the complete cognitive loop from professor correction through clustering, dreaming, assimilation, recall brief, and reference-on-demand lineage.

## Success Criteria

- End-to-end test scenario covers professor correction, anchor comparison, dream aggregation, assimilation, fading eligibility, concise recall, and reference expansion.
- Negative scenarios prove unrelated/contradictory/restricted memories are handled safely.
- Full targeted test suite, build, structural bundle validation, and proof-depth auditor pass.
- Execution report closes raw notes with specific proof paths and commands.

## Covered Inputs

- User wants the foundational memory behavior to work before economic governance is added.
- Previous isolated tests allowed shallow implementation to pass.
- Final proof must show the loop behaves like a student learning from a professor and then internalizing knowledge.

## Prerequisites

- SB01-SB08 completed.
- No open semantic proof gate failures.
- No skipped tests for critical behavior.

## Exact Source References

- /mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs

## Deliverables

- End-to-end integration-style test or small scenario suite.
- Final execution report with raw-note closure and semantic proof mappings.
- Completed-stage structural bundle validation output.
- Proof-depth auditor output.
- Browser proof if UI-visible curator/quality/cluster surfaces changed.

## Dependency Impact

- Closes the bundle and determines whether the system is ready for later economic memory governance work.
- Prevents a second shallow completion claim.

## Validation Depth

- End-to-end regression and closure.
- Full proof across backend and UI-visible surfaces where changed.

## Implementation Steps

1. Create a scenario where an old memory is partially wrong and appears in recall.
2. Run a curator/professor conversation correction with explicit target or ambiguous review as appropriate.
3. Verify the professor anchor enters the correct state and does not self-assimilate.
4. Create or simulate independent derived support, run dreaming, validate aggregate, apply cautiously, and assimilate/fade only when allowed.
5. Run recall and verify the default brief is concise and references are hidden.
6. Resolve references for the synthesized statement and verify original sources plus professor anchor lineage are available according to policy.
7. Run negative checks for unrelated clustering, contradiction handling, and restricted reference hiding.
8. Run final validation commands and update all report rows.

## Scope Exceptions

- Live LLM calls are out of scope; deterministic providers/mocks should be used for proof.
- Large browser walkthrough is required only for changed UI surfaces.

## Do Not Do

- Do not close the bundle with only unit test counts and a build.
- Do not mark raw notes solved without mapping them to source/test proof.
- Do not skip the proof-depth auditor.
- Do not introduce economic governance work.

## Acceptance Checklist

- End-to-end professor learning scenario passes.
- All critical negative cases pass.
- Full targeted cognitive-memory tests pass.
- Build passes.
- Structural validator and proof-depth auditor pass at completed stage.
- Execution report raw note closure table is complete and evidence-backed.

## Proof Required

- Targeted unit/integration test commands.
- Build command.
- Completed-stage `validate_bundle.py` command.
- Proof-depth auditor command.
- Browser evidence if UI changed.
- Final execution report paths.

## Browser Validation Logging

- If UI-visible changes happened: `/cognitive-memory` Curator, Quality, and Cluster tabs large desktop pass.
- Narrow pass for changed curator controls if applicable.
- Screenshot review must answer readability, clipping, and status clarity questions.

## Progression Gate

- The bundle can be marked completed only if all semantic proof gates and final validators pass.
- If any core behavior remains shallow, create a new follow-up subbundle instead of marking raw notes solved.

## Suggested Agent Prompt

```text
Prove the full cognitive loop end to end. Close raw notes only with source/test evidence, run final validators, and do not claim completion if any shallow behavior remains.
```
