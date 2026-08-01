# Hand off runtime command nodes

Summarize the Run command parent, Run app node, Run tests node, command values, command applicability, launcher-compatibility receipts, and any unresolved blockers for parent release approval and screenshot capture. This step writes managed artifacts only. If a runnable command node was not launcher-compatible, the handoff must state what field or tool access is missing and why the parent should request repair instead of treating the node as accepted runtime proof.

## Contract
- Inputs: Run command node receipts and command manifest.
- Outputs: Parent-ready runtime command handoff.
- Evidence: Node ids, commands, command applicability, launcher-compatibility receipts, and blockers.
- Operation target scope: `ExternalProductTargetReadOnly`
