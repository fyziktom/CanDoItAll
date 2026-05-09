# Assumptions And Risks

## Assumptions

- A long project title ceiling of 48 characters including `...` is acceptable for the browser title.
- Selected-node subproject transfer should include descendants by default because the prompt speaks about a selected node group and project-structure parentage must remain coherent.
- Cross-project links from moved nodes to nodes that remain in the source should be removed, matching existing descendant transfer semantics.
- The first shipped high-level tool should solve the selected-node subproject workflow; other scenarios should be inventoried in the XLSX unless they are required to close the immediate defect.

## Critical Path Risks

- `02-agent-node-catalog-and-context` is a critical foundation because the selected-node workflow depends on agents knowing actual selection IDs and typed node shapes.
- `03-selected-node-subproject-tooling` is a critical foundation because weak parent/dependency move behavior invalidates the user's complex-node-workflow requirement.
- If node catalog data is manually duplicated from the UI catalog and drifts, agents may again create wrong node types.

## Validation Risks

- Browser proof may be slower than focused component tests for PageTitle; if skipped, the execution report must mark the gap.
- MAF tool description behavior is partly model-facing and cannot be fully proven by unit tests; tests must at least prove tool presence and service payload semantics.
- XLSX generation depends on the spreadsheet artifact-tool runtime being available in the Codex workspace.

## Reopen Triggers

- Reopen subbundle 02 if tests show `WorkItem/task` is missing from the catalog or selected-node IDs are absent from contextual prompts.
- Reopen subbundle 03 if moved nodes retain parent IDs that do not exist in the target project or internal `DependsOn` links are lost.
- Reopen subbundle 04 if the workbook omits shipped tool names, dependency-specific scenarios, or the architect-provided examples.
