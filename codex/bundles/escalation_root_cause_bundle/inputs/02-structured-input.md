# Structured Input

## Incident

- Parent run: `e5f874f1-02b9-43c8-9c2d-ee932972e992`
- Parent step: `prepare-solution-skeleton`
- Child run: `ab4a1ed8-8b1b-4974-973d-93983bf41f09`
- Child step: `create-dotnet-project`
- Agent execution: `48c3753c-d0bb-4679-9eae-2f295d2b8181`
- Process instance context: blocked 5032 instance.

## Observed Product State

- `Calculator.slnx` existed but contained `<Solution></Solution>`.
- `dotnet sln list` reported no projects.
- The Blazor WASM project existed.
- The required solution wiring helper was not run.

## Observed Tool Receipts

- Present: `workspace_write_file`
- Present: `workspace_dotnet_new`
- Present: `workspace_create_directory`
- Present: `workspace_stat_path`
- Present: `workspace_read_file`
- Missing: `workspace_pwsh_run_script`

## Runtime Diagnostic

- Code: `process.adapter.product_required_file_content_missing`
- Retry safety: `SafeToRetry`
- Idempotency: `Idempotent`
- Actual route: `ManagerRequired` / `ManagerAction`
- Expected route: bounded `SafeRetry` / `CurrentStepRetry` with diagnostic-specific repair instructions.

## Broader Scope

- All 24 process definitions must be audited.
- All 155 step markdown files must be audited for hard gates hidden in prose.
- All 30 validation JSON files and 30 prompt JSON files must be audited where they participate in hard completion behavior.
- All six artifact templates under `business-plan-development/artifacts` must be audited for semantic completion gates and ledger acceptance rules.
- Blazor, screenshot, runtime-command writeback, .NET solution setup, .NET feature, .NET development slice, and software-delivery templates are explicitly in scope.
