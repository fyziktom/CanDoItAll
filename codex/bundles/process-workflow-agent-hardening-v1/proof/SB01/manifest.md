# SB01 Proof Manifest

## Subbundle

SB01 - Canonical Contracts And Inventory

## Implementation Summary

Added canonical descriptors for process operations, target scopes, provider usage phases, workflow JSON selectors, workspace/browser tool ids, and process operation runtime traits. Replaced low-risk hot-path string lists in tool policy and process dispatch with the new descriptors. Added a scoped drift scanner test suite with positive, negative, repository baseline, and template validation coverage.

## Changed Files

See `proof/SB01/changed-file-hashes.md`.

## Command Transcripts

| Transcript | Purpose | Result |
| --- | --- | --- |
| `proof/SB01/transcripts/prepared-validator.txt` | Revalidated prepared bundle structure before SB01 closure. | Exit code 0. |
| `proof/SB01/transcripts/focused-contract-tests.txt` | Built and ran focused SB01 contract scanner tests. | Exit code 0; 6 passed, 0 failed. |
| `proof/SB01/transcripts/source-assertions.txt` | Captured source locations for descriptors, scanner tests, and hot-path catalog usage. | Exit code 0. |
| `proof/SB01/transcripts/anti-stub-audit.txt` | Searched production SB01 surfaces for stubs, deliberate bad ids, and Tetris-specific logic. | Exit code 0; no matches. |

## Source Assertions

- `ProcessOperationContractNames`, `ProviderUsagePhaseContractNames`, and `WorkflowJsonPathContractNames` are defined in `src/CanDoItAll.AgentFramework.Models/Contracts/ProcessOperationContractNames.cs`.
- `ToolContractCatalog` is defined in `src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`.
- `AgentToolInvocationPolicy.cs` and `ProcessRunAutomationDispatchService*.cs` use `ToolContractCatalog` / `AgentToolInvocationPolicyMetadata` instead of repeating the touched tool id literals.
- `ProcessContractCatalog` is defined in `src/CanDoItAll.Modules.Processes/Definitions/ProcessContractCatalog.cs`.
- `ProcessContractDriftScannerTests` includes rejection, acceptance, scoped repository scan, and structured template validation tests.

## Semantic Proof

- Adversarial negative proof: `Scanner_rejects_unowned_internal_tool_id` asserts that `workspace_destroy_everything` in a production source path is reported as internal canonical drift.
- Semantic positive proof: `Scoped_repository_contract_drift_scan_has_no_unowned_internal_ids` scans the scoped source/template/skill surfaces and passes with zero findings.
- Template proof: `Process_template_operation_ids_are_known` parses `Templates/Processes/processes/software-delivery/definition.json` and rejects any operation not known to `ProcessContractCatalog`.
- Enum parity proof: `Process_operation_contract_names_match_runtime_enums` compares canonical operation and target-scope names to runtime enums.

## Shallow-Pass Trap

This proof does not rely on class existence. The passing test suite exercises both rejection of an unknown internal id and acceptance of classified external/template/test literals, then scans the real scoped files.

## Anti-Stub Audit

`proof/SB01/transcripts/anti-stub-audit.txt` found no production `TODO`, `NotImplemented`, `throw new NotImplementedException`, `workspace_destroy_everything`, or `Tetris` matches in the SB01 production surfaces.

## Raw Note Literal Closure

| Raw note area | SB01 closure |
| --- | --- |
| Canonicity drift | Centralized initial internal contract owners and scanner. |
| String-key/JSON-path surface | Cataloged workflow selectors and tool ids touched by scoped files. |
| Duplicated template/runtime/skill/UI rules | Classified boundaries and added structured template validation. |
| Preserve genericity | No Tetris-specific production logic introduced; scanner is domain-neutral. |

## Dependency Smoke Proof

No browser UI was changed in SB01. The focused `dotnet test` command builds the unit-test dependency graph and verifies downstream-visible descriptors from model, core, and processes assemblies.
