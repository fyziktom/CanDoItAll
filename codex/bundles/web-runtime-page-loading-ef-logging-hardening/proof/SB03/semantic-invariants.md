# SB03 Semantic Invariants

- Invariant ID: `WEB-SB03-001`
- Source raw note: `REQ-PROJ-001`.
- Expected behavior: After a Project Structure node is persisted, the visible canvas shows and selects that node by patching the current surface instead of reloading the full structure for the normal existing-surface path.
- Disallowed shallow implementation: Showing an optimistic node before persistence succeeds, skipping persisted follow-up moves, or still reloading the full surface after the create call.
- Failing-first test: N/A process because the reported failure was an observed UI latency regression; `bundle://proof/SB03/transcripts/negative-probe.md` guards against the old create-then-reload sequence returning.
- Passing test: `Quick_sibling_note_insertion_persists_downward_stack_shift` proves the inserted node appears on the canvas, affected sibling movement persists, and the create path uses the reduced DbContext count.
- Changed source files: `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` and `repo://tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`.
- Production assertions: The patch runs only after persistence and link/move writes succeed; the explicit no-current-surface case still reloads.
- Red-team negative case: A quick sibling insert cannot pass by skipping movement persistence because the test reloads persisted surface data and verifies the lower node position.
- Downstream dependency check: SB05 component regression and web startup include this page code with no startup failure.
