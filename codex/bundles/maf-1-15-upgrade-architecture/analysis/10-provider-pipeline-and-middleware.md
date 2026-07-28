# Provider Pipeline and Middleware Verification

## Why This Is a Gate

MAF 1.15 approval-response binding is automatic only when `ChatClientAgent` applies its default decorators. The application currently constructs `ChatClientAgentOptions`, but the provider factory owns the final creation path.

A provider implementation can unintentionally bypass binding by:

- setting `UseProvidedChatClientAsIs = true`;
- returning an already decorated `IChatClient` and suppressing default middleware;
- invoking the `IChatClient` directly instead of invoking the `AIAgent`;
- replacing the session between request and approval continuation;
- running outside an active `AgentRunContext`;
- applying a custom run-level chat client transformation that drops required decorators.

## Required Effective Pipeline

For ordinary default paths:

```text
AIAgent invocation
  -> active AgentRunContext with restored session
  -> ApprovalResponseBindingChatClient
  -> ApprovalNotRequiredFunctionBypassingChatClient
       disabled during parity through option, enabled later only by feature gate
  -> FunctionInvokingChatClient
  -> optional message injection
  -> optional per-service-call history persistence
  -> provider/telemetry pipeline
```

CanDoItAll's AIAgent/tool policy middleware may wrap the agent outside this stack. It remains required.

## Provider Inventory Template

For every provider kind/transport, record:

| Provider | Factory type | Agent type | `UseProvidedChatClientAsIs` | FICC source | Binding source | Active session proven | Per-service-call persistence | Approval support |
|---|---|---|---|---|---|---|---|---|

Cover at least:

- OpenAI Responses;
- Azure OpenAI;
- generic MEAI provider;
- Ollama/local provider;
- A2A remote/provider path if it constructs local agents;
- any mock/fake/test provider;
- any custom HTTP or Copilot Studio provider.

## Behavioral Middleware Probe

Do not rely only on reflection/type names. Add a deterministic probe:

1. fake provider emits an approval-required function call;
2. run through the actual provider factory and `AIAgent.RunStreamingAsync`;
3. serialize the active session;
4. continue with a response that has the correct request ID but modified tool arguments;
5. assert the original arguments execute;
6. repeat with unknown ID and assert no invocation;
7. repeat after process-local cache clear and session deserialize.

If the original arguments do not execute or unknown IDs reach the function invoker, the effective stack is invalid.

## Custom Stack Rule

If a provider must use `UseProvidedChatClientAsIs = true`, its builder must explicitly include:

- `ApprovalResponseBindingChatClient` as the outermost relevant decorator;
- approval-not-required bypass only according to the parity/feature flag;
- `FunctionInvokingChatClient`;
- per-service-call persistence when required;
- message injection only when deliberately configured;
- telemetry in the correct location.

Document the order with a focused test and a source comment in English.

## Run Options

Inspect every `ChatClientAgentRunOptions.ChatClientFactory` or equivalent transformation. A run-specific factory must not replace the already secured stack with a leaf client.

## Session Invariant

The session used to record the approval request must be the session restored for
the approval response. An equivalent transcript is insufficient for native
binding. If exact native 1.15 serialized session state is unavailable, reject the
continuation and drain or reissue the approval.

## AIAgent Builder Interaction

CanDoItAll calls `agent.AsBuilder()` and adds application middleware. Confirm:

- the resulting built agent delegates to the secured inner `ChatClientAgent`;
- middleware does not call the leaf provider directly;
- tool invocation policy sees the exact rebound call;
- the active run context survives wrapper layers;
- disposal does not remove session state before persistence.
