# 03 Regression-First Gap Proof Corpus

## Status

- `Completed`

## Execution Proof

- Entry gate: passed. SB01 and SB02 were completed, active skills and proof-depth auditor were reopened/cited, and cognitive-memory feature code was still untouched before SB03.
- Tests added:
  - `ClusterPlanner_MergesRelatedMemoriesAcrossDifferentTitlesAndTopicKeys` maps to SB04.
  - `DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate` maps to SB05.
  - `ProfessorAnchor_DirectCaptureMemoryCannotAssimilateItsOwnAnchor` maps to SB06.
  - `RecallSynthesis_BuildsQueryShapedBriefInsteadOfTitleGroupedConcatenation` maps to SB07.
- Baseline command: `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ClusterPlanner_MergesRelatedMemoriesAcrossDifferentTitlesAndTopicKeys|FullyQualifiedName~DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate|FullyQualifiedName~RecallSynthesis_BuildsQueryShapedBriefInsteadOfTitleGroupedConcatenation|FullyQualifiedName~ProfessorAnchor_DirectCaptureMemoryCannotAssimilateItsOwnAnchor" --logger "console;verbosity=normal"`.
- Baseline result: exited `1`; total tests `4`, failed `4`.
- Failure evidence:
  - clustering: `Assert.Single() Failure: The collection did not contain any matching items`.
  - dreaming: `Assert.DoesNotContain() Failure` because `Synthesized aggregate:` was present.
  - professor: `Assert.Throws() Failure: No exception was thrown`.
  - recall: `Assert.StartsWith() Failure`; actual brief began with `- Use rollback runbook...` instead of `Production rollback`.
- Production source guard: `git diff --name-only -- src/**` returned no paths after SB03.

## Objective

- Create tests that prove the current implementation is still too shallow before production cognitive-memory fixes are made.

## Success Criteria

- At least one failing-first test exists for clustering, dreaming, professor assimilation, and recall synthesis.
- Tests describe the shallow behavior they are designed to reject.
- Initial failure evidence is captured before fixes when feasible.
- No production behavior is changed except minimal test harness utilities.

## Covered Inputs

- Current cluster planner groups by one primary key.
- Current dream candidate text contains diagnostic boilerplate and first-line fragments.
- Current professor anchor assimilation can use the direct curator-applied memory as derived proof.
- Current recall synthesis groups by title and concatenates useful lines.

## Prerequisites

- SB01 and SB02 completed.
- Updated skills and proof-depth auditor were reopened/cited in the execution report.

## Exact Source References

- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryClusterPlanner.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryCuratorConversationService.cs
- C:/repositories/CanDoItAll/src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs

## Deliverables

- New/updated unit tests for composite clustering merge and split behavior.
- New/updated unit tests rejecting template dream canonical text as domain memory.
- New/updated unit tests requiring professor assimilation proof to be distinct from the direct capture memory.
- New/updated unit tests proving recall synthesis is query-shaped and not title-concatenation.
- Execution report initial failure summary.

## Dependency Impact

- Blocks SB04-SB07 because later implementation must close these tests rather than reinterpreting the requirements.
- Gives the QA auditor concrete behavior evidence instead of report-only proof.

## Validation Depth

- Critical foundation regression proof.
- Unit tests are required; component/browser proof is not required unless UI-visible behavior is changed.

## Implementation Steps

1. Add clustering tests using the adversarial corpus template: unrelated same project/source/month must not aggregate, related different title/topic should merge through composite evidence, contradiction must split or review.
2. Add dream tests that fail if canonical memory contains diagnostic boilerplate such as `Cluster quality:` or if the aggregate claim is not source-supported.
3. Add professor tests that fail if `AppliedMemoryRecordId` can be used as the assimilation derived record for the same capture.
4. Add recall tests that fail if statements are produced only by grouping same-title selected sections and concatenating first lines.
5. Run targeted tests and record failures before production fixes when feasible.

## Scope Exceptions

- Do not implement the production fixes in this subbundle except minor test fixture helpers.
- Do not skip initial-failure proof unless the execution report explains exactly why baseline failure could not be captured.

## Do Not Do

- Do not mark tests as skipped to make the suite green.
- Do not assert internal template strings as success criteria.
- Do not add tests that only check record counts or non-null ids.

## Acceptance Checklist

- Each critical behavior has at least one negative/adversarial test.
- Tests fail on the current shallow path or the report records source-level proof that they would fail.
- Test names clearly describe cognitive behavior, not implementation plumbing.
- Execution report maps each failing-first test to a later subbundle.

## Proof Required

- Targeted `dotnet test` command for cognitive-memory unit tests.
- Failure output or source-level equivalent if baseline run cannot be captured.
- Updated execution report semantic proof section.

## Browser Validation Logging

- N/A; backend regression corpus.
- No browser screenshots required.

## Progression Gate

- SB04-SB07 may start only after regression tests exist and are mapped to their owning fix subbundles.
- No test may be skipped or weakened to close this gate.

## Suggested Agent Prompt

```text
Create the regression-first corpus only. Capture failing-first evidence for clustering, dreaming, professor assimilation, and recall synthesis. Do not fix production behavior yet.
```
