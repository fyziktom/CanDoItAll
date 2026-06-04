# SB01 Proof Manifest

## Scope

Subbundle: `SB01 Fail-closed process operation contracts`.

This pass closes the reopened fail-open gap in governed process operation contracts without duplicating the typed contract/linter infrastructure that already exists in the codebase.

## Source Changes

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ProcessToolOperationAuthorizer.cs`
  - Missing allowed operations now deny governed process tool invocations when the tool has a required operation.
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesService.Support.cs`
  - `ProcessOperatingMode.GovernedLive` now forces strict lint mode.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
  - Run start passes the resolved operating mode into effective lint-mode selection.
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
  - Adds `ProcessToolOperationAuthorizer_SB01_INV_001_denies_governed_step_with_missing_operation_contract`.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
  - Adds `StartRunAsync_SB01_INV_001_applies_strict_lint_for_governed_live_runtime`.

Changed file hashes:

- `bundle://proof/SB01/changed-file-hashes.txt`

Source assertions:

- `bundle://proof/SB01/source-assertions.txt`

## Existing Contract Infrastructure Reused

The current codebase already provides the equivalent resolver/status infrastructure required by SB01:

- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessStepOperationContractState.cs`
  - Typed normalization result with missing allowed operations, missing target scope, and invalid combination diagnostic codes.
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs`
  - Strict/advisory lint behavior for missing, inferred, partial, and invalid operation contracts.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRunAutomationDispatchService.*`
  - Persisted operation contract resolver used by automation dispatch.
- `repo://Templates/Processes/README.md` and `repo://Templates/Processes/processes/*/definition.json`
  - Shipped templates declare typed `AllowedOperations` and `OperationTargetScope`.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
  - Runtime UI/API view models expose allowed operations and target scope.

No new resolver abstraction was added because the missing behavior was specifically the fail-open policy branch and governed-live lint escalation. Adding another resolver layer would have duplicated established local code.

## Failing-First Proof

- `bundle://proof/SB01/transcripts/failing-first-authorizer-missing-contract.txt`
  - Replayed the old authorizer behavior where missing operations returned `null`.
  - Result: targeted unit theory failed for mutation, validation, launch, browser proof capture, and external action tool requirements.
- `bundle://proof/SB01/transcripts/failing-first-governed-live-run-start.txt`
  - Replayed the old run-start behavior where `GovernedLive` did not force strict lint.
  - Result: governed-live compatibility-mode run incorrectly started instead of failing with lint errors.

## Passing Proof

- `bundle://proof/SB01/transcripts/passing-authorizer-missing-contract.txt`
  - `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "ProcessToolOperationAuthorizer_SB01_INV_001" --no-restore`
  - Result: 5/5 passed.
- `bundle://proof/SB01/transcripts/passing-governed-live-run-start.txt`
  - `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "StartRunAsync_SB01_INV_001" --no-restore`
  - Result: 1/1 passed.
- `bundle://proof/SB01/transcripts/passing-process-definition-linter-contracts.txt`
  - `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessDefinitionLinterTests" --no-restore --no-build`
  - Result: 23/23 passed.
- `bundle://proof/SB01/transcripts/passing-publish-contract-mode-gates.txt`
  - `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~PublishAsync_SB12_INV_001|FullyQualifiedName~PublishAsync_SB12_INV_002" --no-restore --no-build`
  - Result: 2/2 passed.
- `bundle://proof/SB01/transcripts/passing-template-operation-contracts.txt`
  - `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "Manifest_process_templates_SB08_INV_001_all_steps_declare_typed_operation_contracts" --no-restore --no-build`
  - Result: 1/1 passed.

## Anti-Stub Audit

- `bundle://proof/SB01/anti-stub-audit.txt`
  - Scanned changed production files for `TODO`, `NotImplemented`, `throw new NotImplementedException`, and `fixture-specific`.
  - Result: pass.

## Production Behavior Artifact Matrix

| Behavior artifact | Producer | Consumer | Lifecycle | Negative proof | Positive proof |
| --- | --- | --- | --- | --- | --- |
| Governed process tool invocation with missing operation contract is denied when a required operation exists. | `ProcessToolOperationAuthorizer.Evaluate` | `AgentToolInvocationPolicy` process-step tool evaluation | Per invocation; no persistence added. | `failing-first-authorizer-missing-contract.txt` | `passing-authorizer-missing-contract.txt` |
| `GovernedLive` run-start requests force strict process definition lint before dispatch. | `ProcessesService.ResolveEffectiveLintMode` and run-start context creation | `ProcessesService.StartRunAsync` callers and process runtime dispatch | Per run-start request before run execution can proceed. | `failing-first-governed-live-run-start.txt` | `passing-governed-live-run-start.txt` |
| Strict contract lint rejects missing, partial, inferred, and invalid typed operation contracts while preserving explicit compatibility warnings. | `ProcessDefinitionLinter` and `ProcessStepOperationContractState` | Publish/start gates and editor diagnostics | Draft/editor lint, publish lint, and run-start lint. | Existing linter assertions in class run | `passing-process-definition-linter-contracts.txt`, `passing-publish-contract-mode-gates.txt` |
| Shipped process templates carry typed operation contracts. | Template manifest loader and process template JSON | Process template governance tests and import/publish paths | Template source lifecycle. | Existing template governance assertion | `passing-template-operation-contracts.txt` |

## Raw Note Closure

SB01 closes the raw-note slice for skipped/omitted work where missing process operation contracts allowed governed tool execution. Remaining raw-note slices for canonical registry, cost reconciliation, real process E2E proof, proof quality, and final QA remain assigned to SB02-SB09.

## Downstream Impact

SB02 can proceed with the operation-contract gate stable. SB04 real process E2E must use templates that pass strict operation-contract lint and must expect missing governed contracts to block before tool dispatch.
