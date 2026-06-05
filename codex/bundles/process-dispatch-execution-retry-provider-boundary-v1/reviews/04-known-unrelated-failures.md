# Known Unrelated Failure Notes

## Broad Architecture Test Class

The full `ProcessAgentExecutionBoundaryArchitectureTests` class is not used as the SB42 smoke command because some older tests in that class depend on historical bundle files outside this refactor cutline. That is a pre-existing repository-state coupling, not a failure of the execution/retry/provider helper extraction.

The bundle-specific architecture guard was run instead:

- `Process_dispatch_execution_retry_provider_gate_a_SB04_INV_001_keeps_refactor_module_local_without_driver_or_ui_proof`
- Proof: `bundle://proof/SB42/transcripts/broad-focused-smoke-matrix.txt`

## Existing SaveAgentAsync Plumbing

`ProcessAutomationExecutionClient` and `ProcessDispatchTechnicalAgentBindingCoordinator` still reference `SaveAgentAsync`. Those are existing dispatch infrastructure surfaces and are outside this bundle's provider-recovery helper cutline.

The provider-recovery helper scan verifies the new provider recovery helpers only call `SaveAgentAsync` from `ProcessAssignedAgentProviderRepairCoordinator.cs`.

- Proof: `bundle://proof/SB43/transcripts/final-source-hardening-scans.txt`
