# Current State

The repo is on `maf-processes-refactor`, ahead of origin by one commit at bundle intake. The previous process refactor already split the dry-run host pipeline, strengthened runtime-host contracts, and added static capability catalog/readback pieces.

The current gap is behavioral proof, not another abstraction pass. Representative process templates are present, but key tests still lean on manual transitions or isolated service reads where the bundle requires production-path automation dispatch, finalizer completion, artifact projection, manager/operator readback, and read-only verification lifecycle evidence.

Primary implementation surfaces:
- `repo://src/CanDoItAll.Modules.Processes/Templates`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch`
- `repo://tests/CanDoItAll.Tests.Integration`

Current constraints remain active: Process Core must stay generic, drivers must not gain execution-capable side effects, representative proof must not use `SuppressAutomationDispatch = true` as the primary execution proof, and final closure must satisfy the code-first ratio.
