# Hand off screenshot writeback evidence

Summarize screenshot applicability, captured routes, Screenshots parent node id, accepted image asset node ids, rejected images, no-UI evidence, and unresolved blockers for parent release approval. This step writes managed process artifacts only.

## Contract
- Inputs: Screenshot storage receipts and target manifest.
- Outputs: Parent-ready screenshot writeback handoff.
- Evidence: Applicability, node ids, asset ids, route evidence, no-UI status, and blockers.
- Operation target scope: `ExternalProductTargetReadOnly`
