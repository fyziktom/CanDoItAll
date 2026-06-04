# Failure Normalization

Dispatcher should not catch AgentFramework runtime exceptions directly after this bundle. The client should map execution failures to process-owned failure results or exceptions.

Allowed client-internal dependencies:

- `AgentChatRunFailedException`
- `AgentRunFailedException`
- `ExecutionRunDetail`
- `ExecutionRunResult`

Forbidden dispatcher dependencies after SB07:

- `AgentChatRunFailedException`
- `AgentRunFailedException`
- `ExecutionRunDetail`
- `ExecutionRunResult`
- `ExecutionRunRecord`
- `ExecutionInvocationContext`
- `ExecutionInvocationPolicy`
- `AgentStructuredOutputContracts`

The failure contract must preserve enough data for current recovery behavior:

- execution run id
- chat session id
- detail snapshot when available
- failure message
- preferred response text
- marker for chat-run failure versus run failure if behavior requires it
