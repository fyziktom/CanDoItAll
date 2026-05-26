# CanDoItAll process template pack

This pack is the current-architecture-aligned replacement for the original execution bundle.

## Goals
- Keep process templates file-driven and outside compiled C# code.
- Preserve shared and local resource sidecars for roles, artifacts, checklists, validations, prompts, and step documents.
- Project current-module import envelopes with first-class dependencies, artifact inputs, decision roles, and branch outcomes.
- Provide Mermaid exports plus supporting markdown files for human inspection and downstream tooling.

## Current architecture adjustments
- Added explicit process-level role usages.
- Added first-class step dependencies and artifact-input definitions.
- Added a new branching-code-review template aligned to the current baseline scenarios.
- Realigned the baseline scenario catalog to five scenarios matching the current repository expectations.
- Added corrective guidance for the remaining hardcoded authoring chrome in ProcessCanvasSurfaceFactory.
- Added typed operation contracts, target scopes, workflow/subprocess artifact mappings, and block/recovery health guidance for governed process runs.

## Governance requirements
- Every manifest template step must declare `AllowedOperations` and `OperationTargetScope`. Do not rely on prose-only phrases such as "may edit the app" to define mutation rights.
- Use the narrowest target scope that matches the step. Validation, review, screenshot, writeback, and escalation steps should normally be read-only or external-action controlled; product mutation belongs in implementation or repair steps.
- `ContractMode` should be `Strict` for templates that are ready to enforce typed operation contracts. Use compatibility mode only for explicitly transitional templates.
- Required workflow-backed artifacts must include explicit workflow output mapping fields: `WorkflowOutputId`, `WorkflowOutputName`, and `WorkflowOutputKind`.
- Required subprocess-backed artifacts must include `SubprocessChildArtifactExpectationId` so parent and child process artifacts are not matched by title/kind heuristics.
- Project-structure writeback is an external action. Mutation tools such as project-structure node or asset creation must require `ExecuteExternalAction`; read-only project-structure discovery should remain read-only.
- Workflows are role executors under Processes. A workflow assignment can execute a process step and produce mapped artifacts, but the process definition still owns dependencies, approvals, artifact expectations, recovery, and closure.
- Block/recovery behavior is typed runtime state. New transitions should supply `BlockCause` instead of depending on blocked-reason text; text inference exists only for legacy state.

## Tetris Blazor WASM PWA readiness checklist
- Start from `baseline-blazor-wasm-pwa-tetris` in `seed-catalog/baseline-scenarios.json` or import `blazor-app-delivery` and preserve the scenario-specific acceptance criteria.
- Keep planning and validation steps read-only. Only implementation and repair steps should use product-target mutation.
- Require artifacts for implementation summary, build/test output, browser screenshot or Playwright proof, PWA/offline validation notes, and final handoff.
- For missing current-step output, block with `OwnOutput`; for missing upstream materialization, block with `UpstreamInput`.
- Before treating a run as ready for UI validation, read run detail and confirm required artifact expectations are satisfied, projection lineage is current-run evidence, and runtime invariant diagnostics are clear.

## Folder layout
- `shared/` contains reusable roles, artifacts, checklists, validations, and prompts.
- `processes/<key>/` contains the template definition, local resources, step docs, Mermaid exports, and projection sidecars.
- `toolbox/` contains role/step seeds and the proposed chrome-action catalog.
- `seed-catalog/` contains baseline seeded runtime scenarios.

## Validation
Use `tools/validate_process_template_pack.py` to validate JSON references, dependency graphs, artifact inputs, and current baseline expectations.
