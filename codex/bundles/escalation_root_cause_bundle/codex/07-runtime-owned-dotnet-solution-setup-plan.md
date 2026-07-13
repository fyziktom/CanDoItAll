# Task 07 – Introduce runtime-owned or guarded .NET solution setup plan

## Problem

The `.NET solution setup` template already contains a good helper script and a good execution plan. The failure happened because the plan was only prompt text. The agent skipped the helper and claimed completion from scaffold receipts.

## Recommended target

For deterministic setup steps, prefer runtime-owned execution:

- `create-dotnet-project`,
- `add-test-project`,
- `repair-solution-setup`,
- build/test validation steps.

If full runtime-owned execution is too large for this iteration, implement a guarded plan first.

## Phase 1 – Guarded plan from existing launch variables

Create a typed plan from existing launch variables:

- `DotNetCreateProjectScriptRef`,
- `DotNetCreateProjectScript`,
- `DotNetCreateProjectSideEffectManifest`,
- `DotNetCreateProjectExecutionPlan`,
- `ProductCompletionRequiredToolReceiptsByStep`,
- `ProductCompletionRequiredPathsByStep`,
- `ProductCompletionRequiredFileContentChecksByStep`.

The guard must know that for `create-dotnet-project`:

- `workspace_dotnet_new template=sln` is required,
- `workspace_dotnet_new template=blazorwasm` is required,
- `workspace_pwsh_run_script` using the resolved create script is required,
- solution readback must contain app csproj path.

## Phase 2 – Runtime-owned helper execution

Add a runtime service that can execute the helper script deterministically through existing governed workspace tool infrastructure.

Pseudo-flow:

```text
if solution/app scaffold missing:
  scaffold missing parts using workspace_dotnet_new
write helper script to resolved managed script ref
verify helper script ref
run workspace_pwsh_run_script with sideEffectManifest
read back solution membership
return typed evidence receipts
```

The agent can then produce a summary artifact from typed evidence, or runtime can generate the managed artifact.

## Important safety constraints

- Preserve current approval/governance rules for product mutation.
- Do not run arbitrary script from template without sideEffectManifest and managed script ref inspection.
- Do not bypass tool receipt ledger. Runtime-owned operations must still create normal tool receipts.
- Do not use native absolute paths as final evidence refs; keep evidence refs workspace-managed or external-target aliases.

## Acceptance criteria

- Empty `.slnx` with app csproj present is repaired by helper without human escalation.
- Helper script path is resolved before execution.
- `workspace_pwsh_run_script` receipt is produced and matched.
- Solution readback contains `src/Calculator/Calculator.csproj`.
- Managed artifact is accepted only after readback passes.

## Regression tests

```text
DotNetSolutionSetupGuard_requires_pwsh_helper_for_create_project
DotNetSolutionSetupGuard_rejects_scaffold_receipts_as_solution_membership_proof
DotNetSolutionSetupExecutor_writes_verifies_runs_helper_and_reads_back_membership
DotNetSolutionSetupExecutor_is_idempotent_when_solution_already_contains_app_project
DotNetSolutionSetupExecutor_does_not_force_regenerate_existing_project
```
