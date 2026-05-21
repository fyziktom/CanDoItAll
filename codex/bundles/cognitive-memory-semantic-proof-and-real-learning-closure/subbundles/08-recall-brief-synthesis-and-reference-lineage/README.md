# Recall brief synthesis and reference lineage

## Status

- Status: `Completed`

## Objective

Move recall output from selected-fragment composition toward task-facing briefs with exact statement-to-claim-to-source lineage.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallBriefComposition.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryReferenceResolver.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallDataLoading.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs

## Deliverables

- Add a task-brief planner that uses query, intent, selected memory, conflicts, caveats, and requested detail level.
- Create synthesized statements as answer/action/caveat/open-question/reference-hint objects with exact lineage.
- Persist line-level or statement-level lineage only for claims/sources actually used in that statement.
- Update reference resolver to answer why a specific sentence is in the brief, including aggregate claim, source claim, source item, and evidence anchor where available.
- Add tests where two selected memories share a source but only one supports a specific sentence.

## Dependency Impact

- Update downstream subbundles, tests, traceability, and proof artifacts if this subbundle changes contracts or service boundaries.
- Re-run prepared-stage validation if this README, requirements, or phase gates are edited.
- Preserve compatibility with existing persistence unless this subbundle explicitly requires schema changes.

## Validation Depth

- Add failing-first proof before production behavior changes.
- Add focused passing tests for the behavior and affected regression tests.
- Include source assertions that prove production behavior, not only tests.
- Include anti-stub audit and red-team negative cases.
- Use portable `repo://` and `bundle://` references only in proof artifacts.

## Implementation Steps

- Separate fragment extraction, planning, composition, and lineage building into small components.
- Ensure default brief hides scores and references but stores lineage for on-demand resolution.
- Add explicit reference-request behavior for Czech and English queries.
- Verify restricted/redacted sources are not exposed in reference-on-demand output.

## Do Not Do

- Do not concatenate the first three useful lines and call it synthesis.
- Do not persist aggregate claim ids on a statement if the statement did not use that claim.
- Do not show detailed internal scores by default.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB08/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB08/semantic-invariants.md`.
- Completed: `bundle://proof/SB08/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB08/transcripts/passing.txt`.
- Completed: `bundle://proof/SB08/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB08/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Recall brief synthesis and reference lineage. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.


