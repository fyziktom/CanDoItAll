# 06 Agent-Facing Recall Synthesis And References

## Status

- Status: `Completed`

## Objective

Make recall synthesis produce concise, useful memory briefs for agents/users while preserving exact references on demand.

## Covered Inputs

- F-05 recall synthesis is first-line grouping.
- User requirement that memory output should be formulated/combined, not just dumped, with references available when requested.
- RQ-11 and RQ-12.

## Prerequisites

- SB03 aggregate lineage must exist.
- SB05 professor anchors must preserve traceable provenance.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallServices.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallContextPackBuilder.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.Quality.cs

## Deliverables

- Intent-aware recall brief synthesizer that filters redundant/low-utility details and hides internal scores/references by default.
- Statement-level provenance mapping that can expand to memory records, aggregate claims, source items, evidence anchors, and professor anchors.
- Integration point so agent-facing recall uses the brief where appropriate, not only raw context sections.
- Reference-on-demand API/service tests and policy/redaction gates.

## Dependency Impact

- Blocks final UI/operational proof.
- Without this, the memory module still overloads agents/users with raw context.

## Validation Depth

- Unit tests for brief quality, no default references/scores, and reference expansion.
- Tests for aggregate provenance expansion through original source memories.
- Policy tests for restricted/redacted references.
- Integration test or component proof for the path that agents/users actually use.

## Implementation Steps

- Replace title grouping/first-line extraction with a synthesis service that ranks usefulness by query intent, support, recency, and scope.
- Add concise statement generation with uncertainty/conflict notes only when useful.
- Persist exact provenance maps and expand through aggregate/professor lineage.
- Integrate synthesized brief into recall orchestration, UI/API, or agent context path as appropriate.
- Keep raw details available through reference-on-demand only.

## Scope Exceptions

- Do not require live LLM synthesis for deterministic CI; rule-based or provider-optional synthesis is acceptable.
- Do not expose internal scoring by default.

## Do Not Do

- Do not pass raw selected memories as the default final answer context if synthesis is available and valid.
- Do not lose source references when summarizing.

## Acceptance Checklist

- Brief is concise and task-oriented.
- References are hidden by default.
- Reference resolver can explain which memory/claim/source supported a statement.
- Restricted/redacted content remains gated.

## Proof Required

- Targeted unit tests.
- Integration/component/browser proof if agent-facing UI path changes.
- Execution report row updated.

## Implementation Evidence

- Recall synthesis extracts concise useful statements and keeps references hidden by default.
- Reference resolution expands aggregate memories through dream aggregate source maps to original source item locators on demand.
- No visible recall/reference UI path changed; proof is deterministic unit coverage.

## Browser Validation Logging

- Route: `/cognitive-memory` if synthesized recall/reference UI is exposed.
- Large desktop proof for brief and reference expansion.
- N/A if backend-only service integration.

## Progression Gate

- SB07 may start only after reference-on-demand and default brief behavior are proven.

## Suggested Agent Prompt

Rework recall synthesis into a requester-facing memory brief with hidden references by default and exact reference-on-demand expansion through aggregate and professor-anchor provenance.
