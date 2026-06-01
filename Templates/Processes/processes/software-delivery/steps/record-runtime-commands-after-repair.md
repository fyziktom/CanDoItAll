# Record repaired .NET run commands under process run node

Launch and observe runtime command writeback after repaired QA acceptance. Refresh or confirm Run command, Run app, and Run tests nodes under the current process run node using repaired evidence.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET runtime command project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, and blockers.
- Operation target scope: `ExternalActionControlled`
