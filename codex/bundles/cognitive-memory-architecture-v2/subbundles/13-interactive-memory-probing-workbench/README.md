# 13 Interactive Memory Probing Workbench

## Status

- Ready after probing core, recall traces, human review UI, MAF context contribution, and source ingestion foundations are available.
- Recommended to implement before or alongside full Epistemic Drive rollout.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
Implement the user-facing Dialogue Workbench and workflow/tool wrapper layer for the probing core. The backend probing truth-mutation, regression, and calibration rules belong to `13a-probing-core-regression-calibration`.

## Covered Inputs

- Requirements FR-032 through FR-038 and NFR-020 through NFR-024.
- `architecture/15-interactive-memory-probing.md`.
- `architecture/16-probing-regression-and-calibration-loop.md`.
- `contracts/csharp/InteractiveMemoryProbingContracts.cs`.
- `plan/subbundles/13-interactive-memory-probing-workbench/README.md`.

## Numbering Note

This root subbundle uses number `13` to match the plan folder. Dependency order matters more than folder number: implement `13a-probing-core-regression-calibration` first, then this UI/workflow wrapper, then close Epistemic Drive consumption of probe evidence.

## Prerequisites

- `05-recall-orchestrator` persists trace evidence.
- `06-consolidation-engine` can consume evidence later.
- `07-maf-workflow-integration` can expose context/tool boundaries.
- `08-human-review-ui` can display and decide review items.
- `13a-probing-core-regression-calibration` provides durable probe sessions, feedback, regression tests, and calibration services.
- `19-metamemory-abstention-calibration` provides answer-gate decisions and warnings that the workbench must render.
- `12-epistemic-drive-engine` can either already exist or consume probing evidence after this subbundle lands.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\15-interactive-memory-probing.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\16-probing-regression-and-calibration-loop.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\InteractiveMemoryProbingContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\probing-test-matrix.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Context\AgentContextContributionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\CanDoItAll.Components.BaseLib.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\CanDoItAll.Components.CanvasLib.csproj
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj

## Deliverables

- Dialogue Workbench UI.
- Probe question queue and source/trace/finding side panels.
- Correction dialog, feedback actions, and regression-test editor wired to probing core services.
- Workflow executor keys and basic MAF/tool integration.
- Browser validation evidence for probe answer, trace, correction, and regression flows.
- Docker context-separation probe fixture.

## Dependency Impact

- The UI consumes probing core services; it does not reimplement correction, regression, calibration, or evidence publication rules.
- The Dialogue Workbench consumes application services and shared BaseLib/CanvasLib components; domain decisions stay out of Blazor components.
- Epistemic Drive may consume probe evidence after this subbundle lands, but probing must remain useful before full Epistemic Drive execution.
- MAF wrappers may trigger probe workflows, but MAF does not own probe persistence or active memory mutation policy.

## Validation Depth

- Component tests for mode selection, question queue, answer rendering, trace/source panels, feedback actions, correction dialog, and regression-test editor state.
- Integration tests for UI action -> probing core service -> persisted feedback/review/regression artifact.
- Playwright proof for dialogue, trace/source panel, feedback actions, correction dialog, and created regression-test workflow.

## Implementation Steps

1. Add Dialogue Workbench UI.
2. Add question queue and mode controls.
3. Add answer, trace, source ref, warning, and finding panels.
4. Wire feedback actions and correction dialog to probing core services.
5. Wire draft regression test creation/editor to probing core services.
6. Add workflow executor/tool wrappers where needed.
7. Add browser proof and reporting.

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

## Browser Validation Logging

- Record route, viewport, Playwright MCP actions, assertions, screenshot paths, and result in `reviews/01-execution-report.md`.
- Required routes: Dialogue Workbench, active probe session, trace/source side panel, correction dialog, regression-test editor or detail view.
- Required visual checks: answer and trace do not overlap, source refs are readable, feedback actions are visible, restricted/redacted warnings are explicit, dense viewport remains usable.
- Browser proof is mandatory because this subbundle adds user-facing probing workflow.

## Progression Gate

- Proceed to Epistemic Drive closure only after probe turns always persist recall trace ids, feedback cannot mutate active truth directly, created regression tests replay, and browser proof shows trace/source/correction/regression flows.
- If the Docker context-separation probe can conflate production, test, local, or CI Docker contexts, reopen recall/taxonomy before continuing.
- If probe evidence cannot be consumed by Epistemic Drive without stringly typed adapters or JSON-only evidence lookup, reopen the probing data model before continuing.

## Suggested Agent Prompt

Implement Interactive Memory Probing as an evidence-preserving memory interrogation loop. Use recall traces and source refs, keep user corrections review-gated, create regression tests from failures, and feed Epistemic Drive with probe evidence. Do not mutate canonical memory directly from chat.
