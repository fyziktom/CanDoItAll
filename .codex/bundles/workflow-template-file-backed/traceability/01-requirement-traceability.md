# Requirement Traceability

| Requirement | Bundle files | Owning subbundle | Proof required |
| --- | --- | --- | --- |
| R1 | `requirements/01-normalized-requirements.md`, `architecture/01-target-solution.md` | `01-workflow-template-pack-and-loader`, `02-seed-service-conversion` | Source inspection plus tests showing templates load from `Templates\Workflows`. |
| R2 | `analysis/01-current-state.md`, `architecture/01-target-solution.md` | `01-workflow-template-pack-and-loader` | YAML manifest and definition parse tests. |
| R3 | `architecture/01-target-solution.md` | `01-workflow-template-pack-and-loader` | `WorkflowDefinitionValidator` passes for all loaded defaults. |
| R4 | `analysis/01-current-state.md` | `02-seed-service-conversion` | Seed tests prove managed marker/version behavior and component creation are preserved. |
| R5 | `analysis/02-assumptions-and-risks.md` | `01-workflow-template-pack-and-loader`, `02-seed-service-conversion` | Loader error path includes template path/key context and no compiled fallback remains. |
| R6 | `plan/01-phase-plan.md` | `01-workflow-template-pack-and-loader` | Manifest and folder shape review. |
