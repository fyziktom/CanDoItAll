# Normalized Requirements

| ID | Requirement | Owning Subbundles |
| --- | --- | --- |
| RQ-001 | Preserve completed MAF/product decoupling and process execution snapshot boundary. | SB01, SB04, SB08, SB11, SB12 |
| RQ-002 | Inventory artifact/projection/validation behavior before moving production code. | SB02 |
| RQ-003 | Define a narrow artifact evidence seam without creating Process Core or driver packs. | SB03 |
| RQ-004 | Add architecture guardrails and refactor Gate A before production artifact movement. | SB04 |
| RQ-005 | Extract expectation matching and lineage helper logic with focused tests. | SB05 |
| RQ-006 | Add projection planner foundation for execution artifacts without DB/storage side effects. | SB06 |
| RQ-007 | Migrate the first concrete projection path through the new planner with parity proof. | SB07 |
| RQ-008 | Run Refactor Gate B after the first projection migration. | SB08 |
| RQ-009 | Extend planning adapters for mock/response/workspace projection sources without broad rewrites. | SB09 |
| RQ-010 | Introduce artifact validation rule service foundation for selected high-risk rules. | SB10 |
| RQ-011 | Run Refactor Gate C with build, tests, line-count review, and source scans. | SB11 |
| RQ-012 | Produce final red-team review and next cutline. | SB12 |
| RQ-013 | Enforce PC/large-screen-only policy and avoid small/medium/mobile proof artifacts. | SB01-SB12 |
