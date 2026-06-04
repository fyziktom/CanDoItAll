# Requirement Traceability

| Requirement | Bundle files | Owning subbundles | Proof expected |
| --- | --- | --- | --- |
| RQ-001 | `inputs/01-source-artifacts.md`, `analysis/01-current-state.md` | SB01, SB12 | Entry/final scans |
| RQ-002 | `inventories/02-projection-source-map.md` | SB02 | Method inventory + side-effect map |
| RQ-003 | `architecture/02-source-adapter-boundary.md` | SB03, SB04 | Boundary tests |
| RQ-004 | `architecture/02-source-adapter-boundary.md` | SB03, SB04 | Source scans for nested dispatcher dependencies |
| RQ-005 | `subbundles/05-*` | SB05 | Process mock key parity tests |
| RQ-006 | `subbundles/06-*` | SB06 | Workspace/existing parity tests |
| RQ-007 | `plan/01-phase-plan.md` | SB07 | Gate B proof |
| RQ-008 | `subbundles/08-*` | SB08 | Response/browser adapter parity tests |
| RQ-009 | `architecture/03-write-coordinator-boundary.md` | SB09 | Write coordinator unit tests |
| RQ-010 | `subbundles/10-*` | SB10 | Execution path migration tests |
| RQ-011 | `shared-prompts/qa-prompt.md` | SB07, SB11, SB12 | Artifact regression tests |
| RQ-012 | `inputs/03-large-screen-only-proof-policy.md` | All | Proof path scan |
| RQ-013 | `subbundles/12-*` | SB12 | Final cutline |
