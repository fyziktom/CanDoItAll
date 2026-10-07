# Next-module decision

The current execution task is **Collaboration corrective closure only**. The production draft-loss path and scenario/test discrepancies must be resolved before expanding the next implementation run into another module.

After that closure, **TestLab remains the next candidate to assess fully**. At the reviewed HEAD, `src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor` still combines the `/test-lab` rendering with injected `TestLabService`, `ProjectWriteSelectionQuery`, `IProjectPartyIntegrationBridge` and notifications. The inspected first 100 lines show the plan list, search/project/phase/result filters and new/edit actions. The module project still references Infrastructure, SharedKernel and the Projects implementation. No claim is made that this limited follow-up read is a complete TestLab implementation audit or an evaluated dependency graph.

Fresh references:

- [TestLab module project](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Modules/CanDoItAll.Modules.TestLab/CanDoItAll.Modules.TestLab.csproj).
- [TestLab page, inspected lines 1–100](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Modules/CanDoItAll.Modules.TestLab/Pages/TestLabPage.razor#L1-L100).
- [Prior wider module selection](reference/original-assignment/MODULE_SELECTION.md), with its historical scope and source limitations preserved.

A subsequent TestLab assignment should first inspect the full page/host, annotated drafts, plan/case/evidence/run writes, project-selection/admission and party bridge consumers, and the real save/projection tests. Reuse established contracts where appropriate; do not copy the Projects backend into a new UI project. Its exact extraction boundary must be based on that complete read, not the number of files or a predetermined project quota.

Processes and Workbench remain later candidates under the user's small-to-large ordering. No Processes/Workbench refactor, TestLab implementation or new module prompt is authorized by this corrective task.
