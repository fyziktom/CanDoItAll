# SB06 Proof Manifest

## Status

Completed.

## Scope

SB06 hardens governed script execution for `workspace_pwsh_run_script` and `workspace_python_run_file`.

Implemented behavior:

- Added a typed `GovernedScriptSideEffectManifest` contract with explicit side-effect modes.
- Required `sideEffectManifest` JSON for governed process scripts when the step does not allow product mutation.
- Denied undeclared or unverifiable encoded commands, shell delegation, child scripts, static IO APIs, redirection, and Python write APIs in non-mutating steps.
- Validated declared write paths against current-run managed artifacts or allowed external artifact destinations.
- Added post-execution product-root snapshot auditing for non-mutating governed script runs.
- Preserved product-mutation script execution when the step has typed `MutateProductTarget` authority.

## Production Files

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/GovernedScriptSideEffectManifest.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandExecutionService.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandPlanBuilder.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandProcessRunner.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Process/WorkspaceProcessContracts.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`

## Test Files

- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkspaceCommandExecutionServiceTests.cs`

## Proof Artifacts

- `bundle://proof/SB06/semantic-invariants.md`
- `bundle://proof/SB06/transcripts/failing-first.txt`
- `bundle://proof/SB06/transcripts/passing.txt`
- `bundle://proof/SB06/transcripts/changed-file-hashes.txt`
- `bundle://proof/SB06/transcripts/source-assertions.txt`
- `bundle://proof/SB06/transcripts/anti-stub-audit.txt`

## Validation

Focused command:

```powershell
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~WorkspaceCommandExecutionServiceTests"
```

Result: Passed, 120 tests.

Known warnings: existing `MSB3277` Entity Framework Core relational version conflict warnings appeared during build/test and are not introduced by SB06.

## SQLite Audit

No SQLite runtime or migration dependency was added. The audit still finds existing retired/legacy SQLite strings and bundle text, but no SB06 production path adds SQLite provider switching or migrations.


## Changed-File Hashes

- `D0F8B24A39B0A420F87D8F13E8FDF2B51E2D1E90B10DBFD08C2BA9C363C49F2D` `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/GovernedScriptSideEffectManifest.cs`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB06 verified runtime behavior | repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/GovernedScriptSideEffectManifest.cs | bundle://proof/SB06/manifest.md | bundle://proof/SB06/transcripts/passing.txt | bundle://proof/SB06/transcripts/failing-first.txt |


