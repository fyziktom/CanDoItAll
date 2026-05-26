# SB07 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` delegates governed operation authorization to `ProcessToolOperationAuthorizer` and script side-effect inspection to `ProcessScriptSideEffectAnalyzer`.
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ProcessScriptSideEffectAnalyzer.cs` owns typed write, encoded command, shell delegation, and child script findings for governed script policy.
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ProcessToolOperationAuthorizer.cs` owns allowed-operation enforcement for governed process tool calls.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs` owns the artifact expectation validation entrypoint used by the dispatch service wrapper.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactIdentityService.cs` owns projection lineage normalization, serialization, and identity hashing.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs` uses `ProcessArtifactIdentityService` before dedupe and persistence.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB07 verified runtime behavior | repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs | bundle://proof/SB07/manifest.md | bundle://proof/SB07/transcripts/passing.txt | bundle://proof/SB07/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB07/semantic-invariants.md`

## Failing-First or Red-Team Proof

Transcript: `bundle://proof/SB07/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB07/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB07/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB07/transcripts/changed-file-hashes.txt`

- `B7C06C3B06D24ADCA0D4218E1116851D2604F928D08F22CFD2CE0CDDD7723565` `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
## Validation

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests"` passed: 107 tests.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"` passed: 432 tests.
- Existing EF Core relational version MSB3277 warnings remain unrelated to SB07.
- SQLite audit found only existing retired/legacy strings and bundle prohibition text; no SB07 runtime or migration dependency was added.

## Blockers

None.




