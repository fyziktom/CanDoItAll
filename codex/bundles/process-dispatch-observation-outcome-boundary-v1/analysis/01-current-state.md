# Current State Analysis

## Completed prior boundaries

The branch has already moved through several safe boundaries:

- MAF no longer owns process tool construction directly.
- Process automation execution calls are behind `IProcessAutomationExecutionClient`.
- Execution snapshots live in `CanDoItAll.Processes.Contracts`.
- Artifact projection/write coordination, artifact validation helpers, implementation proof helpers, candidate hydration, subprocess projection, execution/retry/provider helpers, and many artifact satisfaction rules are module-local in `CanDoItAll.Modules.Processes`.

## Current target files

Primary files for this bundle:

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Concurrency.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationReceiptObservationHelper.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessToolReceiptFacts.cs`

## Why not Process Core yet

Process Core should not start until the module-local observation and outcome vocabulary is stable. Today, several helpers still depend on dispatcher nested models and on provider/runtime observations encoded in session JSON and execution logs. Extracting Core before these are normalized would either leak current internal JSON shapes into Core or force broad public contracts too early.

## Next seam

The next seam should be a module-local **observation + outcome + completion decision boundary**:

- parse session state once into a stable observation snapshot;
- parse execution logs into reusable observation facts;
- centralize successful tool/file/browser-output observations;
- isolate declared process-step outcome parsing/branch selection;
- isolate completion status/reason decision inputs;
- keep wrappers in dispatcher for compatibility.
