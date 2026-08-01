# Hand off screenshot writeback evidence

Summarize screenshot applicability, captured routes, Screenshots parent node id, accepted and diagnostic image asset node ids, no-UI evidence, and concrete execution blockers for parent QA. Copy the exact evidence-assessment `Branch outcome key`: `visual-accepted`, `visual-defect-observed`, or `no-ui-evidence-recorded`. `visual-defect-observed` and `no-ui-evidence-recorded` are completed evidence handoffs for QA, not child no-go outcomes and not instructions to alter the product. `no-ui-evidence-recorded` must cite the explicit applicability classification; it is not a substitute for the parent’s non-browser QA checks. This step writes managed process artifacts only.

## Contract
- Inputs: Screenshot storage receipts, target manifest, and the visual evidence disposition.
- Outputs: Parent-ready screenshot writeback handoff carrying the exact visual disposition.
- Evidence: Applicability, node ids, accepted and diagnostic asset ids, route evidence, current-run image-analysis receipts, no-UI status, exact branch outcome key, and concrete execution blockers.
- Operation target scope: `ExternalProductTargetReadOnly`
