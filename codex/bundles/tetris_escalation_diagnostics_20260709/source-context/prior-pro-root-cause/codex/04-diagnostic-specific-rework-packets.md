# Task 04 – Generate diagnostic-specific rework packets

## Problem

Manual and automatic rework currently append generic instructions. For the incident, a generic rework does not tell the agent exactly what was missing:

- current-run `workspace_pwsh_run_script` receipt,
- solution membership readback,
- resolved helper script path,
- do-not-rerun scaffold instructions.

Therefore the same agent can repeat the same false `Completed` output.

## Implementation

1. Add `IProcessStepRecoveryInstructionBuilder`.
2. Build recovery instructions from:
   - diagnostics,
   - recovery decision,
   - assignment launch variables,
   - observed tool receipts,
   - product readback issue details,
   - step key.
3. Use it for both:
   - automatic safe rework,
   - manual operator rework.
4. The instruction must be compact and specific, not another large generic process prompt.

## Required `.NET create-dotnet-project` packet

When diagnostics include missing `workspace_pwsh_run_script` or failed solution membership, and launch variables contain `DotNetCreateProjectScript`, build a packet equivalent to:

```text
Previous attempt was rejected by runtime completion gates.
Do not claim solution membership from workspace_dotnet_new scaffold receipts.
Observed current-run receipts include solution/app scaffold creation, but no workspace_pwsh_run_script receipt.
The current solution readback does not contain src/Calculator/Calculator.csproj.
Do not rerun workspace_dotnet_new with force=true unless the contracted files are missing.
Write DotNetCreateProjectScript verbatim to <resolved DotNetCreateProjectScriptRef>.
Verify that script ref with workspace_stat_path or workspace_read_file.
Invoke workspace_pwsh_run_script with that script path, workingDirectory <WorkspaceAlias>, and sideEffectManifest from DotNetCreateProjectSideEffectManifest.
Read back the solution or solution list and verify it contains src/Calculator/Calculator.csproj.
Only then rewrite steps/create-dotnet-project.md and submit Completed.
```

## General packet rules

- Mention exact observed missing receipt names.
- Mention exact product readback failures.
- Mention resolved paths only; never unresolved `{CurrentProcessRunId}`.
- Include idempotency guidance.
- Include forbidden actions if they caused repeated issues.
- Keep packet short enough to be salient.

## Acceptance criteria

- Manual rework after this incident includes `workspace_pwsh_run_script` and resolved script ref.
- Auto rework includes the same packet.
- Packet says scaffold receipts are not proof of solution membership.
- Packet says not to write primary artifact before helper receipt/readback.

## Regression tests

```text
RecoveryInstructionBuilder_dotnet_create_project_mentions_missing_pwsh_receipt_and_resolved_script_ref
RecoveryInstructionBuilder_does_not_include_unresolved_current_process_run_id_placeholder
RecoveryInstructionBuilder_includes_observed_receipts_and_failed_readback
OperatorRework_appends_diagnostic_specific_packet
AutoSafeRework_uses_same_instruction_builder
```
