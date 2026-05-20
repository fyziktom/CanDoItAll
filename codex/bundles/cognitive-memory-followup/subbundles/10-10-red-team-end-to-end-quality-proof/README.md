# SB10 - Red-team end-to-end quality proof

## Status

- Status: `Completed`

## Objective

Run a final adversarial proof that the complete loop works and the bundle process cannot be gamed by shallow evidence.

## Covered Inputs

- User expects the remaining problems to be fully solved and Codex to be forced to verify the work.
- Need final proof that process and cognitive behavior both improved.

## Prerequisites

- SB01-SB09 completed with validated proof manifests.

## Exact Source References

- C:/repositories/CanDoItAll/codex/bundles/cognitive-memory-execution-depth-professor-learning-followup/reviews/01-execution-report.md
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryQualityFoundationTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/CognitiveMemoryAdvancedServicesTests.cs
- C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/CognitiveMemoryPageTests.cs
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py
- C:/repositories/CanDoItAll/codex/skills/bundles/candoitall-bundle-workflow/SKILL.md

## Deliverables

- Add one deterministic E2E scenario: wrong memory -> natural professor teaching -> structured anchor -> comparison dream -> independent support -> assimilation/fade -> aggregate -> recall brief -> exact reference-on-demand.
- Run red-team verifier against SB01-SB09 proof manifests.
- Run completed-stage validator and proof-depth auditor.
- Run targeted and broad cognitive-memory tests.
- Update execution report with raw-note closure and residual risks.

## Dependency Impact

- Final closure for the bundle.
- If any upstream proof is weak, reopen the responsible subbundle.

## Validation Depth

- E2E test asserts real state transitions and lineage, not just counts.
- Red-team report tries shallow-pass attacks against workflow, clustering, dreaming, professor, and recall.
- Completed validator must check proof manifests and fail if artifacts are missing.
- Final report must honestly mark partial/blocked items if any remain.

## Implementation Steps

- Create or update final E2E test.
- Run proof manifest validator for all critical subbundles.
- Run targeted SB04-SB08 tests and broad cognitive-memory unit tests.
- Run component/browser tests if any UI-visible changes occurred.
- Produce red-team verdict using template.
- Update root README, subbundle statuses, execution report, and raw-note closure.

## Do Not Do

- Do not close if any critical subbundle has prose-only proof.
- Do not skip UI proof if UI bindings changed.
- Do not claim all raw notes solved if professor learning or recall lineage remains partial.
- Do not include economic governance.

## Acceptance Checklist

- All critical proof manifests validate.
- Fake-proof fixtures still fail.
- Final E2E professor learning scenario passes.
- Clustering, dreaming, curator/professor, recall/reference targeted tests pass.
- Broad cognitive-memory unit tests pass.
- Final completed-stage validator passes.
- Raw-note closure table is honest and artifact-backed.

## Proof Required

- `proof/SB10/manifest.md`.
- Red-team verdict report.
- Completed-stage validator transcript.
- Targeted and broad test transcripts.
- UI/browser proof if applicable.
- Final raw-note closure table with artifact paths.

## Browser Validation Logging

- Run Playwright/component proof if any curator, recall, reference, or review UI surface changed in SB06-SB08.

## Progression Gate

- Bundle can be marked completed because all critical subbundles have artifact-backed proof, red-team passes, targeted and broad cognitive-memory tests pass, and completed-stage validation passes.

## Completion Evidence

- Proof manifest: `../../proof/SB10/manifest.md`
- Red-team verdict report: `../../reviews/02-red-team-verdict.md`
- Passing targeted transcript: `../../proof/SB10/transcripts/passing-targeted-end-to-end-quality-tests.txt`
- Passing broad transcript: `../../proof/SB10/transcripts/passing-broad-cognitive-memory-unit-tests.txt`
- Fake-proof rejection transcript: `../../proof/SB10/transcripts/fake-proof-fixtures-still-fail.txt`
- Economic-governance scope guard: `../../proof/SB10/transcripts/economic-governance-scope-guard.txt`
- Completed-stage validator transcript: `../../proof/SB10/transcripts/completed-validator.txt`

## Suggested Agent Prompt

Implement SB10. Red-team the complete process and cognitive-memory loop. Do not close the bundle unless artifacts, tests, source code, and report all agree.


Remember: a subbundle is not complete because the report says it is complete. It is complete only when source code, tests, proof manifest, transcripts, and validator/red-team gates agree.
