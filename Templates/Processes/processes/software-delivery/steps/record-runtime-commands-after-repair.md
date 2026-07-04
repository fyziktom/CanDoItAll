# Record repaired .NET run commands under process run node

Launch and observe runtime command writeback after repaired QA acceptance. Refresh or confirm runtime-capable Run app and Run tests nodes under the current process run node using repaired evidence. Runnable commands must use Environment, Script, or Infrastructure node types with runtime metadata, not delivery blocks. The child handoff must include launcher-compatible metadata receipts for every runnable command node; do not accept a Run app or Run tests node as repaired when it lacks the project path, command, arguments, working directory, subtype, or other metadata needed by the project-structure runtime launcher.

First call `project_structure_process_subprocess_launch` with `definitionKey` set to `dotnet-runtime-command-writeback`. If the launch response includes `ParentDeferredOutcomeJson`, submit that parent outcome exactly. Running children defer the parent; completed children complete the parent from child evidence; stopped children propagate their concrete blocker. Do not hand-author a different finalizer for the same child run.

If a previous repaired runtime-command child subprocess is Completed, Failed, Cancelled, or Blocked, treat it as historical evidence rather than an active wait. Inspect its artifacts, then complete from valid child evidence or relaunch the runtime-command subprocess when required evidence is missing and relaunch is allowed. Do not return `Blocked` only because the stopped child exists.

After the child run completes, write the parent step record to `artifacts/process-runs/<current-process-run-id>/steps/record-runtime-commands-after-repair.md`. The final `evidenceRefs` for this parent step must include that exact current-run step artifact path plus the child `runtime-command-handoff` evidence and the child `write-run-command-nodes` receipt. Do not return `Completed` without child-run evidence in the final reason and evidence refs.

## Contract
- Inputs: Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.
- Outputs: Observed .NET runtime command project-structure writeback child run with parent-ready writeback evidence.
- Evidence: Child run status, managed artifacts, project-structure receipts, node ids, launcher-compatible metadata receipts, and blockers.
- Operation target scope: `ExternalActionControlled`
