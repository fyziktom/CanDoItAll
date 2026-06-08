# SB002 Test Debt Inventory

## Scope
- Subbundle: `SB002`
- Purpose: separate current-scope failures from historical fixture/file-lock failures before downstream implementation.
- Test platform: VSTest runner with xUnit, .NET SDK `10.0.204`.

## Current-Scope Failures
- Proof-induced secret-scan false positive: fixed during SB002 by redacting token-shaped proof output and removing the old SB029 `task-creation` slug from durable proof text.
- Verification proof: `bundle://proof/SB002/transcripts/secret-scan-after-proof-redaction.txt` passed 1/1.

## Known Debt Buckets
| Bucket | Current evidence | Classification | Owning follow-up |
| --- | --- | --- | --- |
| Stale architecture fixture paths | `bundle://proof/SB002/transcripts/full-unit-tests-no-build-inventory-after-redaction.txt` failed 21 tests, all in `ProcessAgentExecutionBoundaryArchitectureTests`, all `DirectoryNotFoundException` for old `codex/bundles/*` artifacts. | Historical fixture-path debt; not caused by current process-driver implementation. | SB004 |
| TuningRequest file-lock cleanup | `bundle://proof/SB002/transcripts/unit-tests-excluding-stale-architecture-fixtures.txt` failed 1 test in `TuningRequestServiceTests` during async cleanup of `events.ndjson`. | Intermittent cleanup/file-lock debt; not process-driver behavior. | SB005 |

## Green Baseline
- `bundle://proof/SB002/transcripts/unit-tests-excluding-known-debt.txt` passed 975/975 when excluding only `ProcessAgentExecutionBoundaryArchitectureTests` and `TuningRequestServiceTests`.
- SB001 focused baseline tests still pass: `bundle://proof/SB001/transcripts/focused-baseline-unit-tests.txt`.
- SB001 solution build passed: `bundle://proof/SB001/transcripts/solution-build-no-restore.txt`.

## Failure List
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_stabilization_SB022_INV_001_limits_process_core_consumers_to_explicit_call_site_map`
- `ProcessAgentExecutionBoundaryArchitectureTests.Execution_boundary_design_stays_on_staging_facade_cutline`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_evidence_driver_contract_SB024_INV_001_keeps_driver_permission_model_non_production`
- `ProcessAgentExecutionBoundaryArchitectureTests.Step_completion_finalizer_gate_a_SB04_INV_001_records_live_inventory_and_blocks_core_driver_or_viewport_drift`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_pre_extraction_consolidation_SB030_INV_001_keeps_core_rehearsal_docs_tests_only`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_dispatch_claim_route_gate_a_SB04_INV_002_rejects_placeholder_or_stale_inventories`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_dispatch_claim_route_gate_a_SB04_INV_001_records_live_inventory_and_blocks_core_driver_or_viewport_drift`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_contract_candidate_driver_readiness_SB027_INV_001_preserves_pure_rule_parity_and_core_candidate_boundaries`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_pre_extraction_consolidation_SB033_INV_001_keeps_driver_readiness_docs_tests_only`
- `ProcessAgentExecutionBoundaryArchitectureTests.Artifact_validation_gate_a_records_live_inventory_and_blocks_driver_or_viewport_drift`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_evidence_driver_contract_SB033_INV_001_defers_driver_runtime_and_blocks_broad_core_extraction`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_stabilization_SB028_SB030_INV_001_keeps_domain_lane_maps_read_only_and_side_effect_denied`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_contract_candidate_gate_a_SB003_INV_001_keeps_bundle_rows_and_production_guardrails`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_dispatch_pre_execution_guard_gate_a_SB04_INV_001_locks_local_boundary_without_core_driver_or_viewport_drift`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_execution_boundary_inventory_records_direct_dispatcher_calls_before_movement`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_stabilization_SB026_SB027_INV_001_keeps_driver_contract_proposal_non_production`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_stabilization_SB034_SB036_INV_001_closes_final_handoff_with_scorecard_and_driver_denial`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_evidence_driver_contract_SB027_INV_001_keeps_domain_schemas_readonly`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_contract_candidate_driver_readiness_SB033_INV_001_closes_final_red_team_cutline_without_core_or_driver_api`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_pre_extraction_consolidation_SB002_INV_001_guards_core_driver_ui_drift_and_collapsed_rows`
- `ProcessAgentExecutionBoundaryArchitectureTests.Process_core_contract_candidate_driver_readiness_SB030_INV_001_keeps_driver_readiness_docs_traceability_only`

## Closure Decision
- SB002 passes as an inventory subbundle.
- Downstream work may continue only because SB004 and SB005 explicitly own the two remaining debt buckets before Gate B.
