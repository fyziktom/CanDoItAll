# Subbundle 12-interactive-memory-probing-workbench

## Objective

Add the Interactive Memory Probing vertical slice: probe sessions, probe turns, recall-backed answers with trace, user feedback/corrections, question generation, regression test creation, and probe evidence integration with Epistemic Drive.

## Placement In Execution Order

This plan subbundle should run after recall traces, consolidation basics, MAF context contribution, and human review UI exist. It should run before full Epistemic Drive closure if possible, because Epistemic Drive becomes much more useful when probing evidence is available.

Root execution bundle naming uses `subbundles/13-interactive-memory-probing-workbench` to avoid renumbering existing closure and Epistemic Drive folders.

## Inputs

- `architecture/15-interactive-memory-probing.md`
- `architecture/16-probing-regression-and-calibration-loop.md`
- `contracts/csharp/InteractiveMemoryProbingContracts.cs`
- `diagrams/11-interactive-memory-probing-flow.mmd`
- `diagrams/12-probing-session-sequence.mmd`
- `diagrams/13-probing-to-epistemic-drive-loop.mmd`
- Recall traces, context packs, source refs, review queue, and Epistemic Drive coverage/gap records.

## Required Code Areas

- Cognitive Memory EF models and configurations.
- Recall orchestrator and trace store.
- Human review queue.
- Epistemic Drive evidence reader.
- MAF/workflow executor registration.
- Blazor Cognitive Memory Dialogue Workbench.

## Implementation Rules

- Probe feedback is evidence, not direct truth mutation.
- User corrections must create correction evidence and review candidates according to risk.
- Every answer must link to a recall trace.
- Probe sessions must obey access/redaction policy.
- Secret-like data must not be embedded or sent to external providers.
- Regression tests must be durable and replayable.
- Use source code comments in English.

## Suggested Vertical Slice

1. Add probe session/turn/feedback/regression EF records.
2. Add `IMemoryProbeSessionService` and minimal manual question flow.
3. Use recall orchestrator with `IncludeRecallTrace=true`.
4. Render answer + trace/source/confidence in UI.
5. Persist feedback actions: confirm, correct, missing, wrong scope, request source.
6. Create review items from corrections.
7. Create draft regression tests from failed turns.
8. Publish probe evidence refs for Epistemic Drive.
9. Add Docker context-separation fixture and tests.

## Tests

- Unit tests for probe outcome classification.
- Unit tests for correction risk classification.
- Unit tests for regression test creation from a probe turn.
- Integration test for probe question -> recall trace -> feedback -> evidence refs.
- Negative test: correction does not directly modify active memory.
- Negative test: wrong-scope answer creates context-separation evidence.
- Negative test: secret-like probe text is redacted before external context.
- Browser test for Dialogue Workbench answer + trace + correction actions.

## Acceptance Criteria

- Probe session can be started for a project.
- A manual user question creates a probe turn and recall trace.
- The UI shows answer, trace id, source refs, confidence, warnings, and suggested actions.
- User feedback creates durable evidence.
- Corrections create review items rather than active truth changes.
- A failed probe can create a draft regression test.
- Epistemic Drive can consume probe outcomes as gap evidence.
- Docker context-separation probe catches production/test conflation.

## Evidence Required

- Build and targeted test output.
- EF migration/model proof.
- Browser screenshots of the Dialogue Workbench.
- Sample probe session JSON/report.
- Sample review item created from user correction.
- Sample regression test created from wrong-scope Docker answer.
- Implementation report with deviations.
