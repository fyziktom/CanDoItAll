# Accepted-use outcome event integration

## Status

- Status: `Ready`

## Objective

Connect professor accepted-use emission to real agent/user outcome events so assimilation evidence is produced naturally during use.

## Covered Inputs

- Current-state findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Execution order and gates in `plan/01-phase-plan.md`.

## Prerequisites

- Read the bundle root `README.md` and all files under `analysis/` and `requirements/`.
- Follow the execution order in `plan/01-phase-plan.md`.
- For SB03 and later, do not start until SB01 and SB02 gates are completed and active skills are synchronized.

## Exact Source References

- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAcceptedUseSignalEmitter.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAnchorService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryProfessorAssimilationEvaluator.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryRecallSynthesisService.cs
- repo://src/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs
- repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs

## Deliverables

- Add a real outcome/feedback event contract and handler that calls `ICognitiveMemoryProfessorAcceptedUseSignalEmitter` when a synthesized statement backed by professor-derived memory is accepted/used.
- Add idempotency by accepted outcome id, statement id, and derived memory id.
- Ensure the handler rejects direct professor capture memory and broad source-map matches.
- Ensure assimilation scan is triggered after accepted-use emission and also available through scheduled maintenance independent of consolidation success.
- Add tests proving accepted-use is emitted through the event handler, not by direct service invocation only.

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

- Find the existing workflow/outcome/feedback extension point or add a module-local event contract if none exists.
- Wire the handler into DI and scheduler/host services.
- Persist durable event/audit data needed to explain why accepted-use was counted.
- Add red/green tests for duplicate event idempotency and rejected direct capture memory.

## Do Not Do

- Do not manually seed `ProfessorAnchorAcceptedUse` in positive tests.
- Do not treat a direct service unit test as app-level integration.
- Do not let accepted-use count if statement lineage does not include the derived memory and exact supporting evidence.

## Acceptance Checklist

- All deliverables are implemented or an explicit blocker is recorded with evidence.
- Failing-first and passing transcripts exist for behavior changes.
- Source assertions map each semantic claim to production source code.
- Tests prove the negative shallow case fails and the intended production path passes.
- Completed proof manifest cites portable artifacts only.

## Proof Required

- Completed: `bundle://proof/SB05/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB05/semantic-invariants.md`.
- Completed: `bundle://proof/SB05/transcripts/failing-first.txt` unless a process-only exemption is justified.
- Completed: `bundle://proof/SB05/transcripts/passing.txt`.
- Completed: `bundle://proof/SB05/transcripts/source-assertions.txt`.
- Completed: `bundle://proof/SB05/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Record `N/A` with reason if no UI route/component changed.
- If any curator, review, recall, or settings UI changes, record route, viewport, user actions, screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Codex may proceed only after the acceptance checklist is satisfied and downstream dependency impact is reviewed.
- Reopen this subbundle if later source review finds a capability label that is not literally implemented.

## Suggested Agent Prompt

Implement Accepted-use outcome event integration. Start by reading this README and every exact source reference. Create failing-first proof where required, implement production behavior, update tests, record portable proof artifacts, run the required validators, and only mark this subbundle completed when all acceptance checks pass.
