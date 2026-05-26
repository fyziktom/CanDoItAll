# SB01 Semantic Invariants

## Invariant SB01-INV-001

- Invariant ID: `SB01-INV-001`
- Source raw note: RN03 "deny legitimate product mutation due to read-only alias overlap"; RN01 "block unnecessarily".
- Expected behavior: Prompt grounding must not add an alias to read-only metadata when it is equal to or covered by a trusted writable alias, and policy must not deny product mutation under a trusted writable root because a broader or duplicate read-only alias also exists.
- Disallowed shallow implementation: Removing all read-only aliases, making prompt aliases writable, fixture-specific path hardcoding, or changing tests without changing production merge and policy paths.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB01/transcripts/passing.txt`
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`; `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- Production assertions: `ExecutionInvocationMetadata.GroundPromptExternalTargetAliases` prunes writable-covered read-only aliases; `DefaultAgentToolInvocationPolicy.EvaluateReadOnlyExternalTargetMutation` skips denial for referenced aliases covered by `AllowedExternalTargetAliases`.
- Red-team negative case: Same writable alias and writable child alias failed before the fix; read-only-only alias still denies mutation after the fix.
- Downstream dependency check: SB04 can extract metadata/grounding logic with SB01 behavior protected by direct unit tests.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| External target alias authority metadata | `ExecutionInvocationMetadata.GroundPromptExternalTargetAliases` | `DefaultAgentToolInvocationPolicy.EvaluateReadOnlyExternalTargetMutation` | Dispatch metadata is built, prompt aliases are grounded, read-only aliases covered by writable authority are pruned, policy evaluates product mutation. | `bundle://proof/SB01/transcripts/failing-first.txt` |
