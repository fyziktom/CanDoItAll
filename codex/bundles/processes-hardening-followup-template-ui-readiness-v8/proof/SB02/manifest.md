# SB02 Proof Manifest

## Status

Completed.

## Semantic invariant

SB02-INV-001: HTTP API save/export/import, runtime detail responses, and MAF process run detail tools expose the typed process governance contract without losing health, recovery, projection, operation, or artifact mapping fields.

See `bundle://proof/SB02/semantic-invariants.md`.

## Failing-first or adversarial proof

`bundle://proof/SB02/transcripts/failing-first.txt`

The pre-change adversarial check failed because `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs` did not expose `ProcessRunHealthSummaryViewModel Health` on `InternalProcessRunDetailToolData`; exit code 1.

## Passing proof

`bundle://proof/SB02/transcripts/passing.txt`

Command: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Api_nested_process_runtime_routes_preserve_typed_contract_state|FullyQualifiedName~Api_definition_routes_round_trip_typed_contract_and_artifact_mapping_fields|FullyQualifiedName~CreateCapabilityState_attaches_internal_process_tools_by_default_when_workspace_services_are_available"`

Result: Passed, 3 tests, exit code 0.

## Source assertions

`bundle://proof/SB02/transcripts/source-assertions.txt`

Assertions cover the MAF health property, API definition save/import/export routes, persisted artifact workflow mapping fields, and runtime detail health/recovery/projection read-model fields.

## Anti-stub audit

`bundle://proof/SB02/transcripts/anti-stub-audit.txt`

No TODO, NotImplemented, or `throw new NotImplementedException` markers were found in the SB02 touched/asserted paths.

## Changed-file hashes

`bundle://proof/SB02/transcripts/changed-file-hashes.txt`

- `FDE212F24DEF6BB9C7780CA2E4549AB5E7A606E40C0FE40A5E76922ABB4A35BE` `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`
- `1B14C83C8AF72622CDB408F6B18DB5977F34E5C937EFDB45933B9B553226A5F4` `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `0B4A9239764467828A9E2BA059E63E0AA6868CB132F59CFF333CC8E0B0767A38` `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionEditorModels.cs`
- `510EAB4DDF72F0818FB969434BAA709B2BFDEE42F91996DD9A759FEDD0C89EB7` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
- `3E5F391A58F782EAB916ED6E8B2180D63E10254E0F2202C65653BCF2354B715B` `repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs`
