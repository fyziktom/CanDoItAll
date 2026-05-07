# Requirement Traceability

| Requirement | Inputs | Analysis | Architecture | Subbundle | Proof |
| --- | --- | --- | --- | --- | --- |
| R-001 | N001 | `analysis/01-current-state.md` | `architecture/01-target-solution.md` | 01 | OpenAPI route smoke. |
| R-002 | N001, N008 | `analysis/02-assumptions-and-risks.md` | `architecture/01-target-solution.md` | 01 | Auth integration tests. |
| R-003 | N008 | `analysis/02-assumptions-and-risks.md` | `architecture/01-target-solution.md` | 01 | Options validation test. |
| R-004 | N008 | `analysis/01-current-state.md` | `architecture/01-target-solution.md` | 03 | Settings UI proof. |
| R-005 | N001, N002 | `analysis/01-current-state.md` | `architecture/01-target-solution.md` | 02 | Project endpoint test/source review. |
| R-006 | N001, N002, N006 | `analysis/01-current-state.md` | `architecture/01-target-solution.md` | 02, 06 | Existing project-structure API test, focused command test, and auth/OpenAPI smoke. |
| R-007 | N001, N002, N005, N006 | `inventories/01-scope-inventory.md` | `architecture/01-target-solution.md` | 02, 07 | Process endpoint tests, focused route tests, and source review. |
| R-008 | N007 | `analysis/02-assumptions-and-risks.md` | `architecture/01-target-solution.md` | 02, 07 | Filtered detail and step-scoped artifact tests. |
| R-009 | N001, N002 | `analysis/01-current-state.md` | `architecture/01-target-solution.md` | 02, 07 | Agent endpoint smoke, OpenAPI focused route proof, and source review. |
| R-010 | N004 | `requirements/user-stories.xlsx` | `architecture/01-target-solution.md` | 04, 08 | Workbook regenerated and sheet list verified. |
| R-011 | N009 | `analysis/02-assumptions-and-risks.md` | `architecture/01-target-solution.md` | 04, 08 | Architecture review in execution report after correction. |
