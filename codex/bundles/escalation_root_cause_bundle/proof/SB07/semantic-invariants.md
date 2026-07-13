# SB07 Semantic Invariants

## Guarded Plan Boundaries

- Runtime/integration owns deterministic .NET setup preflight; Workbench only supplies launch-variable data.
- The guard reads generic process launch variables and `DotNet*` contract values already present on assignments.
- The guard does not reference Workbench implementation types or templates.
- SB07 does not execute the helper script; runtime-owned execution remains SB11.

## Tool Plan Rules

- `create-dotnet-project` must declare `template=sln`, the app template receipt, and `workspace_pwsh_run_script`.
- `add-test-project` and `repair-solution-setup` must declare `workspace_pwsh_run_script`.
- `workspace_dotnet_new` scaffold receipts alone are not solution membership proof.
- The helper script ref must be a resolved current-run managed `.ps1` ref under the process-run scripts folder.
- The primary `steps/*.md` artifact cannot be used as the script path.
- Side-effect manifests must use `mode=ProductMutation`, declare read/write paths, allow shell delegation, and use native ProductRoot/DotNet paths instead of `external-target/...` aliases.
- Required product paths and manifest paths must stay under `ProductRoot` when native absolute paths are available.
- Required file-content checks must declare both `pathCandidates` and `requiredTextAnyGroups`.

## Production Behavior Artifact Matrix

| Plan state | Guard result | Agent dispatch | Completion behavior |
| --- | --- | --- | --- |
| Complete create-project plan | Satisfied | Allowed if runtime tools are composed | Completion gates still require helper receipt and readback. |
| Scaffold-only required receipts | Failed with `dotnet.setup.plan.required_receipt_missing` | Blocked before agent execution | Cannot claim setup complete from `workspace_dotnet_new` receipts alone. |
| Unresolved script ref | Failed with `dotnet.setup.plan.script_ref_unresolved` | Blocked before agent execution | Agent cannot be asked to run `{CurrentProcessRunId}` placeholder paths. |
| External-target manifest path | Failed with `dotnet.setup.plan.native_path_scope_invalid` | Blocked before agent execution | Scripts keep native paths inside manifest/script content. |
| Product path outside ProductRoot | Failed with `dotnet.setup.plan.path_outside_product_root` | Blocked before agent execution | Wrong product root/scope cannot pass preflight. |
| Missing readback checks | Failed with `dotnet.setup.plan.readback_checks_missing` | Blocked before agent execution | Solution membership remains a hard typed gate. |

## Incident Closure Signal

- The incident class where `workspace_dotnet_new` created an empty solution and the helper was skipped is now caught both before execution when the plan omits `workspace_pwsh_run_script`, and at completion when the receipt/readback is missing.
- `ExecuteAsync_blocks_before_agent_when_dotnet_setup_plan_guard_fails` proves invalid deterministic setup plans do not invoke the assigned agent.
- `Completion_gate_evaluator_reports_missing_required_script_receipt_and_failed_solution_readback` remains the completion-gate backstop for scaffold-only false completion.

## Architecture

- `DotNetSolutionSetupToolPlanGuard` is an internal guard facade plus typed records, scoped to process runtime integration.
- CodeAnalytics snapshot `snap-20260708194440-3c6376ed` reported no scoped dependency cycles.
- The guard is a candidate for extraction into reader/validator collaborators if SB08/SB11 expand the contract surface; SB07 keeps the implementation internal and test-covered.


## Completed Validator Contract

- Invariant ID: SB07-FINAL-001
- Source raw note: GPTPro Extended escalation root-cause analysis and the user's broader process/template/artifact repair requirement.
- Expected behavior: The completed subbundle behavior remains implemented, tested, and represented by typed proof artifacts.
- Disallowed shallow implementation: Do not close the phase with prose-only proof, build-only proof, or hidden prompt-only gates.
- Failing-first test: N/A process/non-production final proof uses adversarial negative tests or preserved subbundle evidence in proof/SB07/transcripts/00-validator-metadata.txt.
- Passing test: Completed proof metadata is recorded in proof/SB07/transcripts/00-validator-metadata.txt and the subbundle manifest.
- Changed source files: bundle://subbundles/07-sb07-tool-plan-guard-dotnet-setup/README.md and bundle://proof/SB07/manifest.md.
- Production assertions: Runtime/template/process behavior remains covered by the subbundle proof manifest and final bundle validation.
- Red-team negative case: Shallow final closure without proof metadata, semantic invariant labels, and transcripts is rejected by the completed validator.
- Downstream dependency check: Final bundle validation and recorded CodeAnalytics snapshots verify no unresolved downstream gate remains.

