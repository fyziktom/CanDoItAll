# Known Unrelated Failures

If broad architecture test classes fail because of stale historical bundle fixture files, record them here with exact test names and proof that the scoped route-handler tests still pass.

Do not use unrelated failures to waive scoped route-handler failures.

## 2026-06-06 Broad Unit Architecture Class Run

Command:
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProcessAgentExecutionBoundaryArchitectureTests" --logger trx`

Result:
- Failed: 5
- Passed: 53
- Total: 58

Unrelated failures:
- `Execution_boundary_design_stays_on_staging_facade_cutline` failed because `repo://codex/bundles/process-agent-execution-boundary-foundation-v1/architecture/02-execution-boundary-staging.md` is missing from the current checkout.
- `Process_execution_boundary_inventory_records_direct_dispatcher_calls_before_movement` failed because `repo://codex/bundles/process-agent-execution-boundary-foundation-v1/inventories/02-agentframework-usage-in-processes.md` is missing from the current checkout.
- `Artifact_validation_gate_a_records_live_inventory_and_blocks_driver_or_viewport_drift` failed because `repo://codex/bundles/process-dispatch-artifact-validation-rule-boundary-v1/inventories/02-artifact-validation-method-inventory-seed.md` is missing from the current checkout.
- `Step_completion_finalizer_gate_a_SB04_INV_001_records_live_inventory_and_blocks_core_driver_or_viewport_drift` failed because `repo://codex/bundles/process-dispatch-step-completion-finalizer-boundary-v1/inventories/02-finalizer-method-classification-template.md` is missing from the current checkout.
- `Process_dispatch_claim_route_gate_b_SB08_INV_001_records_concurrency_helper_parity_and_blocks_side_effect_drift` failed on an older concurrency assertion looking for `executionClient.GetExecutionRunDetailAsync`.

Scoped route-handler proof:
- `bundle://proof/transcripts/unit-route-boundary-tests.txt` passed 4 focused route-boundary unit tests with `ExitCode: 0`.
- `bundle://proof/transcripts/integration-route-boundary-tests.txt` passed 5 focused route planner/order/finalizer integration tests with `ExitCode: 0`.
