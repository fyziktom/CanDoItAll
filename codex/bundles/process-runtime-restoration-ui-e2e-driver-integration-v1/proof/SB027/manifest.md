# SB027 Proof Manifest

Status: Passed.

## Scope

Gate I covers `P09: .NET software-development scenario`.

No production runtime, driver, Core, UI, scheduler, workflow, shell, Office, Graph, workspace/storage, or process mutation implementation code was changed in SB025-SB027. The source change for this gate is a test-only strengthening in `repo://tests/CanDoItAll.Tests.Integration/ProcessMockAgentRuntimeIntegrationTests.cs` so the deterministic scenario reads managed artifact files and generated C# output, not just artifact rows.

The gate proves the deterministic software-development process contract: a process run launches, dispatches, completes, writes concrete managed implementation/rollout artifacts, writes a C# output file, routes implementation work through the software-development tool profile, and routes review through the quality-validation profile. It also proves the .NET web implementation guard rules for build/test/run receipts, existing scaffold validation, runtime startup proof, and missing-startup negative behavior.

## Command Transcripts

- `bundle://proof/SB025/transcripts/dotnet-scenario-contract-source-assertions.txt`
- `bundle://proof/SB026/transcripts/dotnet-scenario-runtime-source-assertions.txt`
- `bundle://proof/SB027/transcripts/focused-dotnet-scenario-runtime-tests.txt`
- `bundle://proof/SB027/transcripts/anti-stub-dotnet-scenario-runtime-tests.txt`
- `bundle://proof/SB027/transcripts/forbidden-drift-scan.txt`
- `bundle://proof/SB027/transcripts/prepared-validator-after-sb027.txt`
- `bundle://proof/SB027/transcripts/changed-file-hashes.txt`

## Source Assertions

- SB025 source assertions prove default `.NET`/Blazor process templates, staffing capability signals, execution prompt guidance, and deterministic implementation/rollout artifact expectations exist in current source.
- SB026 source assertions prove the deterministic process scenario runs through launch, dispatch, artifact projection, managed artifact file reads, generated C# output file reads, completion status checks, software-development route metadata, quality-validation route metadata, .NET build/test/run receipt rules, existing scaffold validation, and runtime startup proof guards.

## Test Proof

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "<P09 focused filter>"` passed with 9 tests:

- `Process_mock_three_agent_artifact_handoff_completes_required_outputs_without_full_delivery_process`
- `Process_mock_developer_single_agent_writes_change_set_and_db_free_rollout_artifacts`
- `ResolveRequiredToolNames_adds_run_for_runnable_dotnet_implementation_steps`
- `ResolveMissingRequiredToolExecutions_accepts_validated_existing_dotnet_scaffold_without_dotnet_new`
- `ResolveCompletionStatus_accepts_dotnet_web_implementation_with_runtime_startup_proof_after_mutation`
- `ResolveCompletionStatus_accepts_retry_artifacts_after_validation_without_new_product_mutation`
- `ResolveMissingConcreteImplementationProofSummary_allows_dotnet_validation_after_source_mutation`
- `ResolveCompletionStatus_blocks_completed_dotnet_web_implementation_without_runtime_startup_proof`
- `ResolveCompletionStatus_allows_process_mock_implementation_with_db_free_rollout_checklist`

## Anti-Stub And Adversarial Proof

- The anti-stub audit confirms the scenario test uses a real `TestApplication`, DI-resolved process services, deterministic process mock catalog, real launch-plan execution, durable outbox dispatch, workspace file reads, managed artifact content checks, generated C# output checks, execution-run checks, and process cooperation metadata checks.
- The audit rejects bundle paths, sleeps, test-server shortcuts, direct update shortcuts, and raw SQL mutation inside the scoped scenario method.
- The adversarial proof includes a negative .NET startup-proof guard: completed .NET web implementation is blocked without runtime startup proof.

## Forbidden Drift

`bundle://proof/SB027/transcripts/forbidden-drift-scan.txt` confirms:

- no production source files under `repo://src` changed in SB025-SB027;
- no transient `codex/bundles` path references exist in source/test code;
- no runtime host, registry, selector, driver DI registration, manager command, scheduler/workflow hook, shell execution, Office/Graph call, workspace/storage write, or read-only driver mutation path was introduced by this gate.

## Changed-File Hashes

See `bundle://proof/SB027/transcripts/changed-file-hashes.txt`.

## Downstream Dependency Check

SB028-SB030 can rely on the software-development path being covered by deterministic process runtime proof and .NET build/test/run guard coverage. The next phase must prove a non-software-development business-analysis scenario without relying on software-domain artifacts.
