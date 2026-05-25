# SB04 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs` introduces `ProcessTargetGroundingRecord`, source-kind and authority enums, and builds dispatch metadata from typed grounding records instead of one free-text alias bucket.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs` separates project-structure context, project-structure writable current-run lines, launch-plan text, explicit step contracts, generic text mentions, upstream artifacts, and upstream artifact provenance.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs` promotes writable aliases only from writable-authority records; upstream artifact/provenance aliases remain read-only unless separately present in launch plan, current-run project structure, or explicit step contract.
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs` keeps process prompt-grounded external target mentions read-only even when the governed step allows product mutation.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` covers stale upstream/sibling product aliases remaining read-only for mutating steps.
- `repo://tests/CanDoItAll.Tests.Unit/AgentWorkspaceToolAccessMetadataTests.cs` covers free-text process prompt aliases remaining read-only under product-mutation metadata.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Typed process target grounding records | `BuildProcessInvocationMetadataJson` via `ResolveExternalTargetGroundings` | Allowed/read-only alias metadata consumed by tool policy | Per dispatch invocation; no durable state | Failing-first proof shows stale text could previously become writable; SB04 tests keep stale upstream aliases read-only |
| Project-structure writable authority | `AddProjectStructureGroundings` and `EnumerateProjectStructureWritableGroundingLines` | `ResolveMutableExternalTargetAliases` | Per dispatch invocation from current project-structure summary | Old/stale/sibling project-structure lines are rejected as writable and remain context only |
| Upstream artifact/provenance read-only authority | `AddArtifactInputGroundings` and artifact-inspection grounding | Read-only alias metadata | Per dispatch invocation from upstream records/summaries | SB04 integration invariant proves sibling upstream product root is not in allowed aliases |
| Process prompt free-text read-only grounding | `ExecutionInvocationMetadata.GroundPromptExternalTargetAliases` | Agent workspace access metadata | Per invocation metadata merge | SB04 unit invariant proves prompt text does not become writable when process mutation metadata is true |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB04/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB04/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB04/transcripts/changed-file-hashes.txt`

## Validation

Passed:

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~SB04_INV_001" --no-restore --no-build -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~SB04_INV_001" --no-restore --no-build -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~BuildProcessInvocationMetadataJson" --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentWorkspaceToolAccessMetadataTests" --no-restore -v minimal`

Known unrelated warning noise during build: existing MSB3277 EntityFrameworkCore.Relational 10.0.0/10.0.4 conflicts.

## Blockers

None recorded yet.
