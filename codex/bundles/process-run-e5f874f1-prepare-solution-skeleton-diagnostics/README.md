# Process Run e5f874f1 Diagnostics Handoff

## Purpose

This folder is structured input for ChatGPT Pro to analyze the recurring block in the running 5032 instance without doing implementation work.

Parent process run:

- Run id: `e5f874f1-02b9-43c8-9c2d-ee932972e992`
- Step: `prepare-solution-skeleton`
- Step instance id: `db3e7295-b523-4343-8be6-85598427385b`
- Current result: `NeedsManager` / `Blocked`

Child process run propagated by the parent:

- Run id: `ab4a1ed8-8b1b-4974-973d-93983bf41f09`
- Step: `create-dotnet-project`
- Step instance id: `53d370f4-04c6-4f9c-8ce0-9cd89efda764`
- Agent execution run id: `48c3753c-d0bb-4679-9eae-2f295d2b8181`
- Current result: `NeedsManager` / `Blocked`

## Main Observed Facts

1. The parent step did not block because its own agent wrote an invalid artifact. The parent receipt says it propagated a stopped child process: `Child process run ab4a1ed8-8b1b-4974-973d-93983bf41f09 is Blocked`.

2. The exact parent step has no AgentFramework execution run. The API file `api/agents/parent-prepare-step-execution-runs.json` is `[]`. That matches the UI text saying no exact AgentFramework result summary was found. This is a diagnostics/projection gap for subprocess-driven steps, not proof that no runtime receipt exists.

3. The child `create-dotnet-project` step did have an AgentFramework run. It returned structured `Completed`, wrote `steps/create-dotnet-project.md`, and used these observed tools: `workspace_stat_path`, `workspace_create_directory`, two `workspace_dotnet_new` calls, and `workspace_write_file`.

4. The child step did not use `workspace_pwsh_run_script`, even though the assignment launch variables and template say the deterministic helper must be written, verified, run, and read back before completion.

5. Runtime rejected the child completion with `process.adapter.product_required_file_content_missing`: `Calculator.slnx` did not contain `src/Calculator/Calculator.csproj` or `src\Calculator\Calculator.csproj`.

6. Product readback confirms the blocker. `product-target/Calculator.slnx.txt` contains only an empty solution, and `product-target/dotnet-slnx-list.txt` says the solution has no projects.

7. The child managed artifact file exists in workspace storage, but `ProducedArtifactsJson` is empty in the child receipt. That means the runtime did not accept/materialize the step artifact into the process artifact ledger after product completion validation failed.

## Key Investigation Questions For Pro

- Why did the agent skip the mandatory `DotNetCreateProjectScript` / `workspace_pwsh_run_script` plan despite the assignment prompt and launch variables containing it?
- Why did the persisted diagnostic report only `product_required_file_content_missing` instead of first reporting missing `workspace_pwsh_run_script`, even though `ProductCompletionRequiredToolReceipts` includes it?
- Is required tool receipt enforcement receiving the same tool receipt set that is shown by the Agent API for execution run `48c3753c-d0bb-4679-9eae-2f295d2b8181`?
- Should subprocess parent blocked packets surface the child diagnostic directly when the parent has no exact AgentFramework result summary?

## Evidence Index

- `api/processes/parent-run.json`: parent run projection and result lineage.
- `api/processes/child-run.json`: child run projection and result lineage.
- `api/agents/parent-prepare-step-execution-runs.json`: exact parent-step AgentFramework lookup, expected to be `[]`.
- `api/agents/child-create-step-execution-runs.json`: exact child-step AgentFramework execution run.
- `api/agents/execution-runs/48c3753c-d0bb-4679-9eae-2f295d2b8181-tool-receipts.json`: child tool receipts.
- `db/parent-prepare-receipt.txt`: parent strategy receipt with child-run blocker.
- `db/child-create-receipt.txt`: child strategy receipt with product file-content failure.
- `db/child-create-assignment-full.txt`: full child prompt and launch variables.
- `workspace-artifacts/ab4a1ed8-8b1b-4974-973d-93983bf41f09/steps/create-dotnet-project.md`: artifact written by the agent before runtime rejection.
- `product-target/Calculator.slnx.txt`: direct product file readback.
- `source-map.md`: source files and why each matters.
