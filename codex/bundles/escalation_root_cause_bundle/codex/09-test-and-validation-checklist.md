# Task 09 – Required validation checklist

## Unit tests

Add or update unit tests for:

### Launch variable resolution

```text
LaunchVariableTemplateResolver_resolves_script_refs_from_current_process_run_id
LaunchVariableTemplateResolver_resolves_execution_plan_script_ref
LaunchVariableTemplateResolver_reports_unresolved_tool_critical_placeholder
LaunchVariableTemplateResolver_reports_cycle
DotNetProcessLaunchVariableContributorTests_no_longer_expect_unresolved_current_process_run_id
```

### Completion gate aggregation

```text
CompletionGateEvaluator_reports_missing_required_script_receipt_and_failed_solution_readback
CompletionGateEvaluator_does_not_short_circuit_after_first_product_failure
CompletionGateEvaluator_prioritizes_missing_required_tool_receipt_over_readback_failure
CompletionGateEvaluator_preserves_safe_retry_and_idempotent_metadata
```

### Recovery classification

```text
RecoveryClassifier_routes_safe_idempotent_completion_gate_to_current_step_retry
RecoveryClassifier_routes_repeated_same_safe_retry_to_manager_after_budget
RecoveryClassifier_does_not_safe_retry_policy_or_denied_capability
RecoveryClassifier_classifies_product_required_file_content_missing_as_product_completion_gate
```

### Recovery instruction builder

```text
RecoveryInstructionBuilder_dotnet_create_project_mentions_resolved_script_ref
RecoveryInstructionBuilder_mentions_missing_workspace_pwsh_run_script_receipt
RecoveryInstructionBuilder_mentions_empty_solution_membership_readback
RecoveryInstructionBuilder_forbids_scaffold_receipts_as_solution_membership_proof
```

### Subprocess bridge

```text
ParentSubprocessBridge_returns_child_stopped_blocked_with_child_diagnostics
ParentSubprocessBridge_does_not_accept_rejected_child_markdown_as_produced_artifact
ParentSubprocessBridge_prefers_artifact_ledger_over_file_existence
```

### Managed artifact materialization

```text
ManagedArtifactMaterialization_does_not_label_rejected_output_as_completion_gate_accepted
ManagedArtifactMaterialization_promotes_artifact_after_gates_pass
```

## Integration tests

Add an integration-style test with fake workspace/product root:

1. Product root contains:

```xml
<Solution>
</Solution>
```

2. App project exists at `src/Calculator/Calculator.csproj`.
3. Observed receipts include `workspace_dotnet_new template=sln` and `workspace_dotnet_new template=blazorwasm`.
4. Observed receipts do not include `workspace_pwsh_run_script`.
5. Assignment launch variables require all three receipts and content readback.

Expected result:

- aggregate diagnostics include missing `workspace_pwsh_run_script` and failed solution membership,
- recovery decision is `SafeRetry/CurrentStepRetry`,
- generated rework packet includes resolved script ref and exact readback failure,
- no manager escalation on first attempt.

## Manual validation scenario

Run the calculator process again after implementation.

Expected behavior:

1. `prepare-solution-skeleton` launches or observes `dotnet-solution-setup` child.
2. `create-dotnet-project` creates/wires solution.
3. `Calculator.slnx` contains `src/Calculator/Calculator.csproj`.
4. No manager escalation occurs for first deterministic scaffold/wire issue.
5. If a tool is genuinely denied, escalation packet names the exact tool/path/policy.
6. Parent UI shows child root cause if a child step blocks.

## Build/test command suggestions

Use repository-standard commands. If no solution-level command exists, run at least:

```text
dotnet test
```

and targeted test filters for the added tests.

## Acceptance gate before merge

Codex must provide:

- list of changed files,
- list of added tests,
- exact test commands run,
- summary of expected calculator process behavior,
- note if any template migration/backward compatibility fallback remains.
