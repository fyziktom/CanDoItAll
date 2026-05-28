# Assumptions And Risks

## Assumptions

- "Folder of that process in workspace" means run-level roots such as `artifacts/.../process-runs/{runId}` and top-level generated product folders such as `output/.../process-runs/{runId}/TetrisGame`, not every subdirectory that contains an artifact file.
- Date-based tool receipt folders under `artifacts/.../process-runs/20260528/...` are execution internals and should not create project-structure folder nodes for a specific run when the path does not include the run id.

## Critical Path Risks

- Broadening project-structure grounding can accidentally include unrelated output paths. The fix must prefer project-level planning branches that are ancestors or siblings of target branch ancestors and keep existing unrelated-node tests passing.
- Manager resolution changes must not silently pick an arbitrary manager when assignments are ambiguous.

## Validation Risks

- UI manager chat proof requires the app to be running with current development data. Targeted resolver tests reduce risk, but a final browser or API smoke should still confirm the tab no longer shows the connection error when possible.

## Reopen Triggers

- Reopen SB01 if the grounding summary still omits `C:\programovani\dotnet-demo\output` for the nested delivery target fixture.
- Reopen SB02 if multiple manager-like agents cause a selected run with a manager assignment to remain unresolved.
- Reopen SB03 if a run with multiple generated files still projects child folders like `Components/Layout` or `wwwroot`.
