# SB02 Proof Manifest

## Status

Completed.

## Owned Requirements And Raw Notes

- Requirements: RQ02, RQ03, RQ04
- Raw notes: N001, N005, N007
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Source Assertions

- `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs` resolves `ProcessStepAllowsProductMutation` and keeps prompt-grounded aliases read-only when product mutation is disallowed.
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` denies external product writes and managed output product writes when `ProcessAllowsProductMutation` is false.
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` passes process mutation metadata from audit context into policy evaluation.
- Transcript: `bundle://proof/SB02/transcripts/source-assertions.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessAllowsProductMutation` audit context | `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Audit/WorkspaceExecutionAuditContext.cs` | `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` | Agent runtime policy evaluates every governed tool call | `bundle://proof/SB02/transcripts/failing-first.txt` covers external product writes and managed output product writes denied under a read-only process step |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB02/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB02/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`

Representative changed-file SHA-256: `da504e54474754e2fc2757879f69f92d525af0ad5a72a9175adf437a6e8873b3`

## Validation

Completed through focused unit policy and metadata tests plus full unit and build validation.

## Blockers

None.
