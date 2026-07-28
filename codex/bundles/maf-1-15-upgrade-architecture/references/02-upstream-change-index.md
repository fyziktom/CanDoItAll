# Upstream Change Index

| Change | Upstream reference | CanDoItAll relevance | Bundle owner |
|---|---|---|---|
| .NET 1.14 release | https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.14.0 | Main correctness/security fixes | SB01-SB07 |
| .NET 1.15 release | https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.15.0 | Target release train; hosting/declarative additions | SB02, SB07 |
| Approval response binding | https://github.com/microsoft/agent-framework/pull/7111 | Native binding state, legacy pending approvals, forged response protection | SB03 |
| Workflow message ordering | https://github.com/microsoft/agent-framework/pull/7123 | Handoff tool-call/result adjacency and persisted history | SB04 |
| Prefer terminal workflow outputs | https://github.com/microsoft/agent-framework/pull/6212 | Handoff final response; custom streaming merge may bypass | SB04 |
| Delegate workflow merge to MEAI | https://github.com/microsoft/agent-framework/pull/6826 | Reasoning/text order and message grouping | SB04 |
| Workflow session assembly-version fix | https://github.com/microsoft/agent-framework/pull/7032 | Native checkpoint/external request restore | SB05 |
| Harness file access opt-in | https://github.com/microsoft/agent-framework/pull/7093 | Only hidden Harness paths; custom tools unaffected | SB01, SB06 |
| Compaction summary deserialization | https://github.com/microsoft/agent-framework/pull/7042 | Only if compaction is active | SB07 |
| ChatClientAgentSession constructor | https://github.com/microsoft/agent-framework/pull/7142 | Strict JSON/cross-version session tests | SB05 |
| ToolApprovalAgent stabilization | https://github.com/microsoft/agent-framework/pull/7107 | Compile if used; future auto-approval evaluation | SB01, SB07 |
| HarnessAgent stabilization | https://github.com/microsoft/agent-framework/pull/7119 | Optional future; no conversion now | SB07 |
| Shell UTF-8 buffer fix | https://github.com/microsoft/agent-framework/pull/7128 | Only Harness/CodeAct shell paths | SB07 |
| LocalCodeAct AST hardening | https://github.com/microsoft/agent-framework/pull/7138 | Only if active | SB07 |
| OpenAI Responses hosting changes | https://github.com/microsoft/agent-framework/pull/7000 | Future hosted Responses API; not provider polling replacement | SB07 |
| Declarative autoSend fix | https://github.com/microsoft/agent-framework/pull/7217 | Only if declarative workflow path exists | SB07 |
| Workflow logging allocation fix | https://github.com/microsoft/agent-framework/pull/7268 | No expected source change; observe | SB07 |
| Cosmos TTL fix | https://github.com/microsoft/agent-framework/pull/7030 | Only if Cosmos history provider exists | SB07 |
| Compaction summary source behavior | MAF core 1.15 source | Optional path | SB07 |

## Upstream Source Files Inspected

- `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgentOptions.cs` at `dotnet-1.13.0` and `dotnet-1.15.0`
- `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientExtensions.cs` at `dotnet-1.15.0`
- `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientBuilderExtensions.cs` at `dotnet-1.15.0`
- `dotnet/src/Microsoft.Agents.AI/ChatClient/ApprovalResponseBindingChatClient.cs` at `dotnet-1.15.0`
- `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgentSession.cs` at `dotnet-1.13.0` and `dotnet-1.15.0`
- `dotnet/src/Microsoft.Agents.AI.Workflows/WorkflowHostAgent.cs` at `dotnet-1.15.0`
- `dotnet/src/Microsoft.Agents.AI.Workflows/MessageMerger.cs` at `dotnet-1.15.0`
- `dotnet/src/Microsoft.Agents.AI.Harness/HarnessAgentOptions.cs` at `dotnet-1.15.0`
- `dotnet/Directory.Packages.props` at `dotnet-1.15.0`

## Critical Semantic Comparison

### 1.13 mixed approval option

```text
EnableNonApprovalRequiredFunctionBypassing
Default false
```

### 1.15 mixed approval option

```text
DisableApprovalNotRequiredFunctionBypassing
Default false
```

The new default means bypassing is enabled. Preserve parity explicitly before optional adoption.
