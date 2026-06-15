# Capture and store .NET UI screenshots

Launch and observe the .NET UI screenshot writeback subprocess after runtime command nodes exist. UI targets must capture screenshots and store accepted image assets under a Screenshots parent node below the current process run node. Backend-only or no-UI targets must produce explicit no-UI evidence.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET UI screenshot project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, and blockers.
- Operation target scope: `ExternalActionControlled`
