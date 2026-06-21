# Record .NET run commands under process run node

Launch and observe the .NET runtime command writeback subprocess after first-pass QA acceptance. It must create or update runtime-capable Run app and Run tests nodes under the current process run node, using Environment nodes for runnable .NET app launch commands and Script nodes for test/build/utility commands. It must not write runnable commands as delivery blocks. This step coordinates subprocess evidence only and does not mutate product files.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET runtime command project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, and blockers.
- Operation target scope: `ExternalActionControlled`
