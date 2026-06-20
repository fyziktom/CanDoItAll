# Record .NET run commands under process run node

Launch and observe the .NET runtime command writeback subprocess after first-pass QA acceptance. It must create or update runtime-capable Run app and Run tests nodes under the current process run node, using Environment nodes for runnable .NET app launch commands and Script nodes for test/build/utility commands. It must not write runnable commands as delivery blocks. The child handoff must include launcher-compatible metadata receipts for every runnable command node; do not accept a Run app or Run tests node as complete when it lacks the project path, command, arguments, working directory, subtype, or other metadata needed by the project-structure runtime launcher. This step coordinates subprocess evidence only and does not mutate product files.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET runtime command project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, launcher-compatible metadata receipts, and blockers.
- Operation target scope: `ExternalActionControlled`
