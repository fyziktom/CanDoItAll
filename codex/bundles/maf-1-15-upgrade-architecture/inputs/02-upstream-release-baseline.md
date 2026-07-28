# Upstream Microsoft Agent Framework Baseline

## Target Release Train

Use the following exact package strategy:

| Package family | Target |
|---|---:|
| `Microsoft.Agents.AI` | `1.15.0` |
| `Microsoft.Agents.AI.OpenAI` | `1.15.0` |
| `Microsoft.Agents.AI.Workflows` | `1.15.0` |
| `Microsoft.Agents.AI.A2A` | `1.15.0-preview.260722.1` |
| `Microsoft.Agents.AI.Hosting.A2A` | `1.15.0-preview.260722.1` |

Do not assign `1.15.0` to the A2A packages; their published version is a matching preview build.

## Release Dates

- .NET MAF `1.14.0`: July 21, 2026
- .NET MAF `1.15.0`: July 22, 2026

The migration from 1.13 to 1.15 includes both releases. Most runtime correctness fixes relevant to CanDoItAll landed in 1.14; 1.15 adds hosting and declarative-workflow changes on top.

## Relevant Upstream Changes

### Approval pipeline

- New default `ApprovalResponseBindingChatClient` stores model-originated approval requests in the active `AgentSession.StateBag`.
- Inbound approval responses are bound back to the exact stored request and tool call.
- Unknown, forged, replayed, or substituted responses are ignored or rebound.
- A 1.13 session serialized while an approval is outstanding lacks this new state.
- Mixed approval behavior changed from an opt-in 1.13 option to an enabled-by-default 1.15 decorator with a disable switch.
- `ToolApprovalAgent` and related APIs were stabilized; rule callbacks now use `ToolAutoApprovalRuleContext`.

### Workflow and response correctness

- Workflow-hosted agents preserve message order and tool-call/tool-result adjacency.
- Non-streaming `Workflow.AsAIAgent()` prefers explicit terminal workflow outputs over intermediate agent updates.
- Workflow message merging delegates to MEAI `ToAgentResponse()` while preserving first-seen order and id-less reasoning segments.
- Workflow session restoration tolerates assembly version/culture/public-key-token differences for external request payload types.

### Sessions and history

- `ChatClientAgentSession` constructor parameters are optional for stricter `System.Text.Json` settings.
- Compaction summaries survive serialization/deserialization when metadata values return as `JsonElement`.

### Harness and file features

- Harness file access is opt-in through `HarnessAgentOptions.FileAccessStore`.
- `DisableFileAccess` was removed.
- `FileAccessProviderOptions` configures the provider when a store is supplied.
- `HarnessAgent` and `FileMemoryProvider` were stabilized, while some advanced options remain experimental.
- Shell head/tail buffering was fixed for multi-byte UTF-8 boundaries.
- Local code-act AST validation was hardened.

### Hosting and protocol packages

- AG-UI support was split into `AGUI.Client`, `AGUI.Server`, `AGUI.Abstractions`, `AGUI.Formatting`, and optional `AGUI.Protobuf`.
- OpenAI Responses hosting gained public hosted state/result helpers and session deletion support.
- Declarative `autoSend` output no longer duplicates already-streamed content and uses stable message IDs.
- Workflow resume logging was changed to source-generated logging.

## Compatibility Principle

Adopt fixes that directly strengthen existing CanDoItAll paths, but separate them from optional feature adoption:

- required in this upgrade: package alignment, approval binding, state migration, response ordering, terminal output semantics, session/checkpoint compatibility, A2A;
- evaluate after parity: approval-not-required bypass, stable ToolApprovalAgent, message injection, FileMemoryProvider;
- out of first-pass scope: Harness conversion, AG-UI conversion, declarative workflow migration, OpenAI Responses hosting redesign.

## Official References

- https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.14.0
- https://github.com/microsoft/agent-framework/releases/tag/dotnet-1.15.0
- https://github.com/microsoft/agent-framework/pull/7111
- https://github.com/microsoft/agent-framework/pull/7123
- https://github.com/microsoft/agent-framework/pull/6212
- https://github.com/microsoft/agent-framework/pull/6826
- https://github.com/microsoft/agent-framework/pull/7032
- https://github.com/microsoft/agent-framework/pull/7093
- https://github.com/microsoft/agent-framework/pull/7042
- https://github.com/microsoft/agent-framework/pull/7142
- https://github.com/microsoft/agent-framework/pull/7000
- https://github.com/microsoft/agent-framework/pull/7217
