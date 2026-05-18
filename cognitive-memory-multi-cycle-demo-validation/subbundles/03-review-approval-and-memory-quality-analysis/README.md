# Review approval and memory quality analysis

## Status

- `Completed`

## Objective

- Inspect review candidates after each forced cycle, approve useful memories, reject or mark duplicate/noisy candidates, resolve contradictions, and analyze stored memories backward against the source tracker.

## Success Criteria

- Every review decision is source-backed and recorded.
- Duplicate candidates are explicitly identified and not blindly approved.
- Contradiction/decision candidates prefer current accepted decisions while preserving source traceability.
- Approved memory records are checked against source rows, source locators, summaries, and chunks.

## Covered Inputs

- R6 review, approval, duplicate, and contradiction decisions.
- R7 backward memory quality analysis.
- R10 closure evidence.

## Prerequisites

- Subbundle 02 closure gate passed.
- Candidate preview evidence exists for all stages.

## Exact Source References

- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\trackers\cognitive-memory-demo-source-tracker.xlsx`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\sample-data\source-manifest.json`
- `C:\repositories\CanDoItAll\cognitive-memory-multi-cycle-demo-validation\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallServices.cs`

## Deliverables

- Review decision log.
- Duplicate and contradiction analysis.
- Memory quality analysis tied to tracker rows.
- Updated tracker evidence or exported analysis JSON.

## Dependency Impact

- Chat validation is only meaningful if the approved memory set is intentionally curated. Weak review decisions would make bad chat answers ambiguous because the memory set itself may be noisy.

## Validation Depth

- Critical quality gate.

## Implementation Steps

1. For each stage, inspect candidate previews and source excerpts.
2. Approve durable, useful, source-backed memories.
3. Reject, defer, or mark needs-changes for duplicate, vague, wrong-source, or overgeneralized candidates.
4. For contradiction candidates, record old claim, new claim, decision source, and preferred memory.
5. Run snapshot and recall probes after decisions.
6. Map approved memories to source tracker rows and score usefulness, duplication, source correctness, and cross-project isolation.
7. If implementation defects are discovered, create repair subbundles before chat validation.

## Scope Exceptions

- This subbundle does not validate AI chat behavior; that belongs to Subbundle 04.

## Do Not Do

- Do not approve all candidates automatically.
- Do not treat duplicate candidates as harmless noise if they affect recall quality.
- Do not hide wrong-source references as residual risk.
- Do not mark final closure if vector/projection quality is untested without documenting provider limitations.

## Acceptance Checklist

- Completed: Review decisions have notes and evidence.
- Completed: Duplicates and contradictions are classified.
- Completed: Approved memories map back to tracker rows.
- Completed: Wrong-source and cross-project leakage checks are complete.
- Completed: Repair subbundles exist for blocking defects.

## Proof Required

- Review decision JSON.
- Snapshot JSON before and after approvals.
- Recall JSON after approvals.
- Memory quality analysis JSON/XLSX update.
- Browser screenshot if review UI is used.

## Browser Validation Logging

- Target route: `/cognitive-memory`.
- Required viewport: desktop large-screen review queue.
- Actions: open Review queue, select candidates from at least one update stage and one contradiction/email stage, inspect proposed memory and source excerpt, apply decisions.
- Screenshots: review candidate before decision, duplicate or contradiction candidate if present, and post-decision state.
- Review question: did the UI provide enough source context to justify the decision?

## Progression Gate

- Subbundle 04 may start only after approved memories are mapped to source rows and no unresolved wrong-source/cross-project defect remains open.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Review candidates deliberately. Approve useful source-backed memories, reject or mark duplicate/noisy/wrong-source candidates, and record all decisions. Then run backward analysis against the XLSX tracker. If memory selection, chunking, duplicate detection, or source references are defective, create an on-the-fly repair subbundle before moving to chat validation.
```
