# SB02 Semantic Invariants

## SB02-INV-001

- Invariant ID: `SB02-INV-001`
- Source raw note: N001, N005, N007
- Expected behavior: analysis, architecture, planning, review, and approval process steps cannot mutate product targets unless metadata explicitly permits product mutation.
- Disallowed shallow implementation: prompt warnings only, deny decisions tied to one tool name only, or alias auto-promotion from prompt text.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB02/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Red-team negative case: current-run managed artifacts remain allowed while product writes are denied.
- Downstream dependency check: SB08 retry proof no longer treats wrong-root writes as valid product progress.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessAllowsProductMutation` | `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Audit/WorkspaceExecutionAuditContext.cs` | `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | `bundle://proof/SB02/transcripts/failing-first.txt` |
