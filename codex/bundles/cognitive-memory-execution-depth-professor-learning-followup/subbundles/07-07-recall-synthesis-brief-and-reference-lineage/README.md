# 07 Recall Synthesis Brief And Reference Lineage

## Status

- `Ready`

## Objective

- Replace title-grouped line concatenation with useful query-shaped recall briefs and reference-on-demand provenance.

## Success Criteria

- Recall synthesis selects and phrases statements based on query/intent and claim usefulness, not only section title.
- Briefs are concise and hide internal scores/references by default.
- Each statement has claim/phrase-level source maps sufficient to answer why the information appeared.
- Aggregate references expand through original claim source maps and professor anchors when relevant.
- Contradictions, stale facts, restricted evidence, and low confidence produce caveats or hidden references as appropriate.

## Covered Inputs

- Current recall synthesis groups by normalized title and joins first useful lines.
- User explicitly wants useful formulated/combined information with details/references available on request.
- Professor/curator anchors must remain explainable even after fading.

## Prerequisites

- SB05 deep aggregates completed.
- SB06 professor lifecycle completed.

## Exact Source References

- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- /mnt/data/review_current/CanDoItAll-cognitive-memory/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Query/intent-aware recall brief composer.
- Claim/phrase source mapping or equivalent statement-span provenance.
- Reference resolver support for aggregate and professor-anchor lineage.
- Tests for concise default brief, reference-on-demand expansion, restricted reference hiding, contradiction caveats, and aggregate/professor lineage.

## Dependency Impact

- Provides the agent/user-facing behavior that makes memory useful without overwhelming downstream agents.
- Feeds final end-to-end proof.

## Validation Depth

- Critical agent-facing behavior implementation with positive and adversarial tests.
- No UI proof unless a recall/reference UI surface is changed.

## Implementation Steps

1. Analyze current recall context model and source refs.
2. Design synthesized statements around normalized claims and query intent.
3. Attach source maps at the smallest practical granularity: statement, claim, or text span.
4. Keep references hidden by default but expose a deterministic resolver API for requested statement ids.
5. Add caveat handling for conflict, stale, restricted, and low-confidence conditions.
6. Run recall/reference tests including aggregate/professor lineage.

## Scope Exceptions

- Full natural-language generation can remain deterministic initially if briefs are useful, de-duplicated, and query-shaped.
- Phrase-level provenance can be approximated by claim-level provenance if the tests prove reference-on-demand usefulness.

## Do Not Do

- Do not group solely by title.
- Do not concatenate first lines as the main synthesis behavior.
- Do not show scores, locators, and references by default unless diagnostic mode is requested.
- Do not lose lineage when an aggregate came from dream source maps or professor anchors.

## Acceptance Checklist

- Brief is concise and query-shaped in tests.
- References hidden by default.
- Reference resolver explains each statement through source maps.
- Restricted references are hidden without policy.
- Contradiction/stale caveat tests pass.

## Proof Required

- Targeted recall/reference unit tests.
- Example brief and reference expansion in execution report.
- No browser proof unless UI changed.

## Browser Validation Logging

- N/A unless recall/reference UI changes.
- If UI changes, capture large-screen and relevant responsive screenshots.

## Progression Gate

- SB08 may proceed only when recall no longer relies on title-grouped concatenation and reference-on-demand lineage works for aggregate/professor sources.
- If default brief overwhelms the caller with raw metadata, this gate fails.

## Suggested Agent Prompt

```text
Implement agent-facing recall synthesis and provenance. Make the default brief useful and concise, and make references available by statement/claim on demand.
```
