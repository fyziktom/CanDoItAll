# Record repaired .NET run commands under process run node

Launch and observe runtime command writeback after repaired QA acceptance. Refresh or confirm runtime-capable Run app and Run tests nodes under the current process run node using repaired evidence. Runnable commands must use Environment, Script, or Infrastructure node types with runtime metadata, not delivery blocks.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET runtime command project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, and blockers.
- Operation target scope: `ExternalActionControlled`
