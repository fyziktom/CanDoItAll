# 10 Cross Project Memory

## Status

- Ready after project-scoped recall and consolidation are stable.

## Objective

- Promote safe cross-project semantic, procedural, and decision memory while preserving project boundaries and review policy.

## Covered Inputs

- Requirements FR-007, FR-013, FR-016, FR-020, FR-021, and NFR-008.
- Context-separated relatedness and cross-project recall architecture.

## Prerequisites

- `05-recall-orchestrator` must distinguish scope and project context.
- `06-consolidation-engine` must create reviewable promotion candidates.
- `08-human-review-ui` should expose promotion decisions before high-risk activation.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\architecture\03-memory-taxonomy-and-data-model.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\architecture\05-recall-orchestrator.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\architecture\10-security-governance-and-provenance.md
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll.AgentFramework.SemanticCompletion\src\CanDoItAll.AgentFramework.SemanticCompletion.Driver\Semantics\SemanticClassifier.cs

## Deliverables

- Cross-project scope model.
- Promotion and demotion rules.
- Similarity-with-separation scoring.
- Review workflow for global procedures and high-risk knowledge.

## Dependency Impact

- Project memory remains scoped by default.
- Global memory is a promoted layer with evidence and review state.
- Recall must include project/source access policy before using cross-project candidates.

## Validation Depth

- Golden datasets for similar but intentionally separated project records.
- Tests for promotion review and access-policy filtering.

## Implementation Steps

- Define cross-project scope and promotion states.
- Add similarity and separation scoring.
- Route promotion candidates to review.
- Add recall policy checks for global memory.

## Do Not Do

- Do not auto-merge project-specific knowledge into global memory.
- Do not leak restricted project details through global summaries.
- Do not activate high-risk procedures without review.

## Acceptance Checklist

- Similar records can remain separate.
- Global promotion is traceable and reversible.
- Access policy applies before context pack rendering.

## Proof Required

- Cross-project golden tests.
- Promotion review tests.
- Recall trace evidence for included and excluded global candidates.

## Browser Validation Logging

- Browser proof is required if promotion review UI changes are included.
- Record route, viewport, and screenshots in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed to final closure only after cross-project memory cannot blur source authority or access policy.

## Suggested Agent Prompt

- Implement cross-project memory promotion and recall with explicit scope, review, and access-policy checks.
