# SB01 Semantic Invariants

- Invariant ID: `SB01-output-grounding`
- Source raw note: N001 external output folder not respected.
- Expected behavior: A process launched from a nested delivery node can still ground a relevant external output folder declared in project-level planning context, and completion requires final proof from that external product root.
- Disallowed shallow implementation: Do not hard-code a specific project name, local machine path, process id, or run id; do not merely mention the managed workspace as acceptable final product output.
- Failing-first test: `BuildProjectStructureGroundingSummary_includes_output_folder_from_top_level_architecture_branch_for_nested_delivery_target` fails when ancestor planning branches are not scanned.
- Passing test: `BuildExecutionPromptCore_requires_external_target_final_delivery_proof_when_grounded` passes with the stricter prompt contract.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ProjectPaths.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`.
- Production assertions: The prompt tells agents that workspace-only proof is insufficient when an external target is grounded.
- Red-team negative case: Unrelated branches remain excluded by bounded planning-context scoring and existing unrelated-output regression coverage.
- Downstream dependency check: SB03 projection remains independent because this change only affects process prompt grounding and not persisted artifact records.
