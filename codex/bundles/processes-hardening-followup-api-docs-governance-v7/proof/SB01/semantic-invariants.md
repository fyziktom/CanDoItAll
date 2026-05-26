# SB01 Semantic Invariants

- Invariant ID: SB01-INV-001
- Source raw note: bundle://requirements/01-normalized-requirements.md and bundle://inputs/02-structured-input.md require generic process API, artifact, and recovery governance parity.
- Expected behavior: Nested process API routes and read models preserve typed BlockCause, ProjectionLineage, AllowedOperations, OperationTargetScope, BlockReasonCode, NextRecoveryAction, RecoveryOptions, ProjectionLineageJson, and ProjectionIdentityHash.
- Disallowed shallow implementation: Adding DTO properties without mapping them to ProcessStepTransitionRequest, ProcessArtifactRecordRequest, or read-model projections is insufficient.
- Failing-first test: bundle://proof/SB01/transcripts/failing-first.txt records an adversarial negative source assertion for omitted or placeholder contract mapping.
- Passing test: bundle://proof/SB01/transcripts/passing.txt records ApiIntegrationTests.Api_nested_process_runtime_routes_preserve_typed_contract_state and the focused process validation suites.
- Changed source files: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.cs, repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs, repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Reads.cs, repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs.
- Production assertions: repo://src/CanDoItAll.Web/Api/ProcessesApi.cs maps BlockCause and ProjectionLineage; repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs exposes typed operation, recovery, and projection identity fields.
- Red-team negative case: bundle://proof/SB01/transcripts/failing-first.txt verifies omitted mapping sentinels are absent and fails if the shallow pattern is searched as a required artifact.
- Downstream dependency check: bundle://proof/SB16/transcripts/passing.txt includes integration, component, unit, full build, PostgreSQL-only, and API/tool field source-audit commands.
