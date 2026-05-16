# 13 Interactive Memory Probing Workbench

## Status

- Ready after recall traces, human review UI, MAF context contribution, and source ingestion foundations are available.
- Recommended to implement before or alongside full Epistemic Drive rollout.

## Objective

Implement the user-facing and service-level probing loop that lets Cognitive Memory be questioned like a student, assessed like a memory system, corrected safely, and improved through reviewable evidence.

## Covered Inputs

- Requirements FR-032 through FR-038 and NFR-020 through NFR-024.
- `architecture/15-interactive-memory-probing.md`.
- `architecture/16-probing-regression-and-calibration-loop.md`.
- `contracts/csharp/InteractiveMemoryProbingContracts.cs`.
- `plan/subbundles/12-interactive-memory-probing-workbench/README.md`.

## Numbering Note

This root subbundle uses number `13` because the existing bundle already contains `11-validation-and-architecture-closure` and `12-epistemic-drive-engine`. Dependency order matters more than folder number: implement this before final Epistemic Drive validation closure where possible.

## Prerequisites

- `05-recall-orchestrator` persists trace evidence.
- `06-consolidation-engine` can consume evidence later.
- `07-maf-workflow-integration` can expose context/tool boundaries.
- `08-human-review-ui` can display and decide review items.
- `12-epistemic-drive-engine` can either already exist or consume probing evidence after this subbundle lands.

## Deliverables

- Probe session, turn, feedback, finding, correction, and regression-test records.
- Probe session service, question generator, assessment service, evidence publisher, and regression test service.
- Dialogue Workbench UI.
- Workflow executor keys and basic MAF/tool integration.
- Docker context-separation probe fixture.
- Tests and browser evidence.

## Implementation Steps

1. Add EF models/configurations and repositories.
2. Add probing service interfaces and implementations.
3. Wire manual question path to `IRecallOrchestrator`.
4. Persist recall trace id and context pack id per turn.
5. Implement feedback actions and review item creation.
6. Implement draft regression test creation.
7. Publish `KnowledgeGapEvidenceRef` from failed/weak probes.
8. Add question generator from coverage/gap/stale/contradiction/context-separation evidence.
9. Add Dialogue Workbench UI.
10. Add tests and reporting.

## Do Not Do

- Do not mutate active memory directly from a probe turn.
- Do not let the user correction bypass policy for high-risk procedures.
- Do not treat a generated answer as a source.
- Do not expose secret or restricted source content in probe transcripts.
- Do not require Qdrant for the MVP.
- Do not store only a final probe score; store findings, evidence refs, and calibration data.

## Acceptance Checklist

- Probe sessions are durable and scoped.
- Every probe answer has a recall trace.
- Feedback creates evidence and optional review/regression artifacts.
- Corrections are review-gated by risk.
- Probe failures can feed Epistemic Drive.
- Regression tests replay with deterministic source/trace assertions where possible.
- UI shows why the system answered as it did.

## Proof Required

- Build/test proof.
- Browser screenshots.
- Probe session report.
- Created review item proof.
- Created regression test proof.
- Docker context-separation fixture proof.

## Suggested Agent Prompt

Implement Interactive Memory Probing as an evidence-preserving memory interrogation loop. Use recall traces and source refs, keep user corrections review-gated, create regression tests from failures, and feed Epistemic Drive with probe evidence. Do not mutate canonical memory directly from chat.
