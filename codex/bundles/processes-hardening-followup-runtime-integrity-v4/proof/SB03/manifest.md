# SB03 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` adds inspected script content/failure to `ToolInvocationPolicyContext`, classifies workspace script execution tools, and denies script side effects for governed non-mutating process steps.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` inspects workspace PowerShell/Python script files before policy evaluation and passes inspection content or failure to the policy context.
- `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs` covers SB03 PowerShell/Python product-write denial, read-only validation allowance, and uninspected-script denial.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Inspected script content / inspection failure | MAF middleware in `MafAgentRuntime.AgentFactory.cs` | `DefaultAgentToolInvocationPolicy` through `ToolInvocationPolicyContext` | Per tool invocation only; not durable state | `failing-first.txt` shows old policy saw only redacted arguments and allowed script tools via mutation auto-approval |
| Script side-effect policy decision | `EvaluateGovernedScriptSideEffectBoundary` | Agent tool invocation middleware and block guard | Per tool invocation decision; existing tool trace captures blocked result | SB03 tests deny PowerShell/Python product writes and deny uninspected scripts |
| Read-only validation script allowance | `DefaultAgentToolInvocationPolicy` | Runtime script tool call path | Allowed only when inspected content has no write signal and aliases are grounded read-only | SB03 tests allow read-only `Get-Content` while product mutation remains false |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB03/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB03/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB03/transcripts/changed-file-hashes.txt`

## Validation

Passed:

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~SB03_INV_001" --no-restore -v minimal`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentToolInvocationPolicyTests" --no-restore --no-build -v minimal`

## Blockers

None recorded yet.
