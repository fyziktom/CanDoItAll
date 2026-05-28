# SB03 Semantic Invariants

- Invariant ID: `SB03-run-folder-projection`
- Source raw note: N003 too many artifact nodes.
- Expected behavior: Project structure projection creates folder nodes for the current process run's managed artifact root and generated product root, not one node per artifact subdirectory.
- Disallowed shallow implementation: Do not delete artifact records, hide all artifacts, or group unrelated receipt folders that do not contain the current run id.
- Failing-first test: `GetStructureAsync_projects_process_run_output_folders_into_the_structure_surface` fails when immediate artifact directories are projected.
- Passing test: The same integration test passes with exactly the expected managed proof, product output, and run artifact folder nodes.
- Changed source files: `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs`.
- Production assertions: Storage references still point at managed workspace folders that the UI can open from the process run node.
- Red-team negative case: A date-based receipt path without the process run id does not create a projected run folder node.
- Downstream dependency check: Existing artifact records and process run links remain unchanged; only the projection folder resolution changes.
