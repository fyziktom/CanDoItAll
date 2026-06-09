# SB033 Semantic Invariants

## Invariant SB033-GATEWAY-V1-PUBLIC-API-SNAPSHOT
- Invariant ID: `SB033-GATEWAY-V1-PUBLIC-API-SNAPSHOT`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: The verification gateway v1.x public API remains an explicit typed surface with four public types, a stable surface hash, the existing lane-specific methods, additive `VerifyBatch`, and explicit typed batch request/response records.
- Disallowed shallow implementation: Relying on README prose only, omitting reflection-backed public type/member checks, allowing generic object dispatch, changing `ProcessDriverContractVersion.Current`, or replacing lane-specific methods with string/lane lookup.
- Failing-first test: No deliberate P11 production compile/test failure was produced.
- Passing test: bundle://proof/SB033/transcripts/focused-p11-contract-api-tests.txt, bundle://proof/SB033/transcripts/focused-p11-gateway-batch-tests.txt, bundle://proof/SB033/transcripts/full-unit-p11.txt, and bundle://proof/SB033/transcripts/p11-source-scans.txt
- Changed source files: repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/README.md and repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs
- Production assertions: Existing gateway production code still exposes typed methods and typed batch records; no production behavior changed.
- Red-team negative case: Source scans reject `Verify(object)`, dynamic dispatch, DI/runtime host surfaces, file/network/storage/workspace APIs, stubs, Core reverse dependency, and UI/media drift.
- Downstream dependency check: SB034 may start because the gateway batch additions now have version-governance and public-surface proof.

## Invariant SB033-BATCH-MIGRATION-NO-RUNTIME-COMPATIBILITY-GUARD
- Invariant ID: `SB033-BATCH-MIGRATION-NO-RUNTIME-COMPATIBILITY-GUARD`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Gateway batch migration guidance states that `VerifyBatch` is additive, typed, diagnostic-only, and not a runtime host, driver discovery, DI, scheduler, manager command, connector, file, workspace, storage, or process mutation surface.
- Disallowed shallow implementation: Documenting batch as a replacement for typed lane methods, implying runtime host approval, allowing service registration, hiding `AllResponses` mutability expectations, or omitting no-runtime negative claims.
- Failing-first test: No deliberate P11 production compile/test failure was produced.
- Passing test: bundle://proof/SB033/transcripts/focused-p11-contract-api-tests.txt and bundle://proof/SB033/transcripts/p11-source-scans.txt
- Changed source files: repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/README.md and repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs
- Production assertions: Existing `VerifyBatch` still routes through typed lane methods and optional aggregation over already-produced verification responses.
- Red-team negative case: `Process_driver_contract_api_SB032_INV_001_gateway_batch_migration_guard_is_documented_and_runtime_free` rejects runtime-host, DI, scheduler, manager-command, workspace-write, and storage-write approval claims.
- Downstream dependency check: P12 can start from an API compatibility gate that does not weaken the runtime-free boundary.
