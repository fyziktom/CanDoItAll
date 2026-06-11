# process-runtime-stabilization-release-closure-v1

## Status
Prepared for Codex implementation.

## Purpose
Stabilize `maf-processes-refactor` after the Process Core / Process Module / MAF / runtime-host refactor so representative process templates can again be launched and completed from the product surfaces with production-path dispatch, finalizer, artifacts, and operator readback.

This bundle deliberately does **not** continue broad Process Core extraction. The branch first needs a stable runtime baseline where processes behave like before. Further extraction into standalone process runtime/core libraries should resume only after this closure is green.

## Current diagnosis
The previous bundle made a significant improvement:
- process-mock automation support now exercises launch plan creation, assignment selection, launch approval, `ExecuteLaunchPlanAsync`, outbox drain, AgentFramework execution runs, finalizer summaries, artifact readback, and completed runs;
- Blazor/.NET, canonical multi-team/software-delivery, and business-analysis paths have representative backend automation proof;
- large-screen Playwright proof starts a process from project/project-structure and reads back run details;
- scheduler/workflow-origin starts and read-only verification jobs have integration proof.

Remaining release blockers:
- the previous SB08 final closure is blocked by the code-first ratio gate;
- the UI proof currently proves launch and run detail readback, but not a full user-visible launch-to-completed-run flow;
- runtime-host readback/dry-run denial details still have an explicit run-detail UI gap;
- live OpenAI template proof is not part of the latest bundle because live opt-in variables were absent;
- old manual-transition tests still exist and must stay classified as contract/state tests, not automation proof.

## Hard constraints
- Do not split more Process Core pieces now.
- Do not add execution-capable process drivers.
- Do not add reflection discovery, fallback selectors, driver self-registration, or generic object dispatch.
- Do not move Blazor/.NET/business/template vocabulary into `CanDoItAll.Processes.Core`.
- Do not use `SuppressAutomationDispatch = true` as representative automation proof.
- Do not count docs as implementation in the code-first ratio.
- Do not generate another large proof tree. Update this bundle minimally and keep most changes in `src` and `tests`.

## Target outcome
At the end of this bundle, the branch should have a clear release decision:
- `Merge-ready for process runtime stabilization`, or
- `Runtime backend ready, UI/live proof blocked`, or
- `Not merge-ready`, with a short list of concrete remaining blockers.
