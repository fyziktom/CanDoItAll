# SB08 - Recall task brief and claim lineage

## Status

- Status: `Completed`

## Objective

Upgrade recall from fragment joining to task-facing synthesis with precise statement-to-claim-to-source references on demand.

## Covered Inputs

- Current recall synthesis groups selected sections and joins fragments.
- Reference resolver expands aggregate candidate maps broadly rather than statement-specific claim lineage.
- User wants concise useful information by default with detailed references only on request.

## Prerequisites

- SB05 precise aggregate claim maps completed.
- SB07 faded anchor recall behavior completed.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityContracts.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryQualityEntities.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Add recall brief composer that produces task-shaped statements: answer, caveat/conflict, action relevance, omitted detail count.
- Add statement-to-aggregate-claim/source map persistence.
- Resolve only lineage for the requested statement and claim, not all aggregate sources.
- Separate conflicting statements rather than merging them into one fragment chain.
- Keep scores/internal references hidden by default but available as diagnostics when requested.

## Dependency Impact

- Depends on SB05 statement claim maps and SB07 professor lineage.
- Feeds SB10 end-to-end user-facing quality proof.

## Validation Depth

- Many-memory input compresses to a concise brief below configured budget.
- Conflict input produces separate caveat/disputed statement.
- Reference resolver returns only requested statement lineage.
- Restricted/redacted lineage obeys policy.
- Default brief contains no internal score/reference clutter.

## Implementation Steps

- Extract `ICognitiveMemoryRecallBriefComposer` and `ICognitiveMemoryStatementLineageResolver` or equivalent.
- Persist statement-to-claim/source mappings with aggregate claim id where available.
- Update resolver to follow only mapped claim lineage.
- Add conflict-aware composition and budgeted detail omission.
- Add deterministic tests for concise brief, conflicts, on-demand references, and redaction.

## Do Not Do

- Do not merely change the prefix or punctuation of joined fragments.
- Do not resolve all aggregate sources for every statement.
- Do not expose scores/references by default.

## Acceptance Checklist

- Brief is task-shaped and concise.
- Conflict/caveat statements remain separate.
- Each statement has claim-level lineage.
- Reference-on-demand returns exact sources for the requested statement only.
- Faded professor anchor is explainable but not default clutter.

## Proof Required

- `proof/SB08/manifest.md`.
- Targeted recall/reference tests with transcripts.
- Source-level assertion for statement-to-claim map fields and resolver filters.
- Anti-stub scan transcript.

## Browser Validation Logging

- Run UI/component/browser proof if recall/reference surfaces change.

## Progression Gate

- SB10 cannot close until reference-on-demand can explain each brief sentence precisely.
- If synthesis remains fragment joining, SB08 remains incomplete.

## Completion Proof

- Proof manifest: `proof/SB08/manifest.md`
- Failing-first transcript: `proof/SB08/transcripts/failing-first-targeted-recall-reference-tests.txt`
- Passing targeted recall/reference transcript: `proof/SB08/transcripts/passing-targeted-recall-reference-tests.txt`
- Passing quality/professor regression transcript: `proof/SB08/transcripts/passing-quality-professor-regression-tests.txt`
- Passing persistence/migration smoke transcript: `proof/SB08/transcripts/passing-persistence-migration-smoke-tests.txt`
- Source assertions: `proof/SB08/transcripts/source-assertions.txt`
- Anti-stub audit: `proof/SB08/transcripts/anti-stub-audit.txt`
- Closure decision: `Completed - synthesis now creates query-shaped answer/action and conflict-caveat statements, persists aggregate claim lineage on synthesized source maps, and resolves on-demand references through the mapped claim rather than sibling aggregate sources.`

## Suggested Agent Prompt

Implement SB08. Build task-facing recall synthesis with precise claim lineage and reference-on-demand resolution.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
