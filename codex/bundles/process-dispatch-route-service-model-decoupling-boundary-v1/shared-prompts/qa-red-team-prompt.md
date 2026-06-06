# QA / Red-team Prompt

Review the completed implementation against this bundle.

Reject the work if:
- `CanDoItAll.Processes.Core` is created.
- Production driver APIs appear.
- Route order changes.
- Any route handler accepts `ProcessRunAutomationDispatchService`.
- Route handlers/facets/services still use dispatcher nested aliases outside explicit adapter files.
- One all-facet route service remains the primary implementation.
- Any original behavior path is omitted: database requirement, materialization, stranded recovery, subprocess, start transition, workflow, direct-agent execution, competing guard, run-closed guard, finalizer transition, failure closure.
- The execution report collapses SB001-SB128 into one row.
- UI/mobile/small/medium proof is created.
