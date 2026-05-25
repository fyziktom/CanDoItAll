# Current State Analysis

## What Codex Improved

The branch now has a process-owned finalizer and a typed execution-boundary concept. That is a meaningful improvement over the earlier implementation where workflow-backed role completion and direct agent execution could diverge.

Current important state:

- `ProcessStepExecutionBoundary` is resolved from step text and expected artifacts.
- Metadata carries `agentProcessStepExecutionBoundary`.
- Read-only and writable external target aliases are computed.
- Workflow and subprocess dispatch candidates now include expected artifacts, artifact inputs, branch outcomes, and recorded expectation ids.
- Completed subprocess parent steps call `FinalizeStepCompletionAsync`.
- Missing upstream artifact materialization is journaled.
- Artifact validation diagnostics have fingerprints.
- ProcessDefinitionLinter exists.

## Why This Is Still Not Enough

The runtime still relies too much on text heuristics and prompt discipline. A process engine should not have to guess whether "create architecture record" means "write a managed artifact" or "mutate the product target". It needs a typed operation contract.

The current classifier uses broad tokens such as `create`, `build`, `generate`, `repair`, and `fix`. This can misclassify non-mutating artifact-production work as product mutation, especially when a process step is `Work` but its output is a `Brief`, `Decision`, report, plan, or analysis artifact.

The core risk is now inverted:

- Earlier: runtime was too permissive and allowed missing artifacts.
- Current: runtime may become too strict or misclassified and block valid process progress.
- Both problems come from the same source: process semantics are inferred instead of declared.

## Desired Direction

Move from heuristic inference to a two-layer model:

1. Explicit process definition/runtime contract:
   - allowed operation classes
   - required artifact modes
   - allowed target scopes
   - allowed disposition behaviors
   - retry/no-progress policy

2. Heuristic classifier as only a migration/default assistant:
   - can suggest boundary classifications
   - can generate warnings
   - cannot silently override explicit step contract
