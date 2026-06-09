# SB034 Manager-Visible Read-Only Diagnostic Projection Proof

## Status
Completed.

## Objective
Prove that manager-visible diagnostic projection is read-only, source-backed, and does not become a process mutation surface.

## Source-Backed Proof
- Production source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationProjection.cs`
- Focused tests: `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`
- Gate transcript: `bundle://proof/SB036/transcripts/manager-diagnostics-no-mutation-tests.txt`
- TRX: `bundle://proof/SB036/SB036-manager-diagnostics-no-mutation.trx`

## Behavior Proven
- `ProcessManagerReadOnlyVerificationProjectionMapper.Project` projects only from supplied read-only verification evidence.
- Diagnostics mode attaches diagnostic rows and does not attach the evidence envelope.
- Evidence-envelope mode attaches the aggregate envelope only when requested.
- `None` mode attaches nothing.
- Attached modes require a requesting manager identity instead of silently accepting anonymous manager projection.
- Every projection asserts `NoMutationPerformed`, `AllowsProcessMutation == false`, `AllowsTransitionMutation == false`, and `AllowsFinalizerMutation == false`.

## Positive Assertions
- `Process_manager_readonly_projection_SB031_INV_001_projects_supplied_observations_as_diagnostics_without_mutation`
- `Process_manager_readonly_projection_SB032_INV_001_attaches_evidence_envelope_only_when_requested`
- `Process_manager_readonly_projection_SB033_INV_001_rejects_unnamed_attached_manager_request`

## Closure
SB034 is closed by source-backed projection tests in the passing SB036 integration run. No browser proof is required because this subbundle did not change browser-visible behavior.
