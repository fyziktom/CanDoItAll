# Legacy Backlog Disposition

| Legacy task | Audit claim | Live repo disposition | Decision in this repaired bundle |
| --- | --- | --- | --- |
| `COD-PRM-001` Refactor oversized module files | Still a real maintainability concern. | Still true, but not required to deliver the branching fix safely in this pass. | Not reopened now. Track as follow-up bundle candidate after branching lands cleanly. |
| `COD-PRM-002` Complete canonical definition model | Branch semantics and stronger publish validation were still missing. | Still partially open. | Reopened now in subbundle 02. |
| `COD-PRM-003` Stable diff-based save | Existing save path still recreates child rows. | Still partially open. | Not reopened now. Documented as a follow-up risk because branch work can land without changing identity strategy. |
| `COD-PRM-004` Rebuild runtime over canonical transitions | Runtime still advances by `Sequence`. | Still partially open. | Reopened now in subbundle 03. |
| `COD-PRM-005` Baton handoffs and normalized work brief snapshots | Legacy audit claimed they were missing. | Baseline work briefs, decisions, and runtime artifacts now already exist. | Not reopened as a stand-alone phase. Regression-check only where touched. |
| `COD-PRM-006` Approval and escalation policy engine | Still broader than current branch fix. | Partially open. | Not reopened now because the user’s proven live defect is branch routing, not a full policy engine. Follow-up bundle candidate. |
| `COD-PRM-007` Hard agent guardrails and external correlations | Broader agent-control-plane roadmap item. | Still broader roadmap work. | Out of scope for this run. Explicit future bundle candidate. |
| `COD-PRM-008` Propagate interventions into project structure | Broader projection workflow item. | Still broader roadmap work. | Out of scope for this run. Explicit future bundle candidate. |
| `COD-PRM-009` Journal and live overlay expansion | Live journal and runtime surfaces already exist in baseline form. | Partially present. | Not reopened now except regression awareness where touched. |
| `COD-PRM-010` to `COD-PRM-015` Analytics, governance, template convergence, portability, hardening | Long-range roadmap items. | Not the live defect proven by the user request. | Not reopened in this bundle. Must be handled by later bundles if still desired. |

## Narrowing Justification

- The repaired bundle does not claim the long-range roadmap is complete.
- It reopens only the legacy items that the live code and the user request still prove are missing.
- Every non-reopened legacy item above has an explicit reason instead of being silently dropped.
