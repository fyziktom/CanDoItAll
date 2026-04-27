# Subbundle 04 — Provider Capability Matrix and Approval Alignment

## Goal

Align provider capability decisions with Microsoft Agent Framework semantics and prevent unsafe approval assumptions.

## Current problem

The current matrix says structured output is supported only for OpenAI/Azure Responses. MAF docs show structured output can be configured through `AgentRunOptions.ResponseFormat` for compatible chat clients, including examples using Azure OpenAI Chat Completion service. The matrix also treats approval wrappers as equal to ordinary tool support, but MAF tool approval support is narrower than function tools.

## Implementation tasks

1. Split capabilities.

Replace or extend the matrix with separate fields:

```csharp
SupportsFunctionTools
SupportsStructuredOutput
SupportsRunAsyncTypedOutput
SupportsResponseFormatJsonSchema
SupportsToolApprovalRequests
SupportsApprovalRequiredAIFunction
SupportsHostedTools
SupportsHostedMcp
SupportsLocalMcp
```

2. Derive support by provider + transport + client type.

Do not use `SupportsTools` as a proxy for approval.

3. Update `EnsureStructuredOutputCapability(...)`.

Instead of hard rejecting all non-Responses transports, allow compatible chat clients that support `ResponseFormat`.

If capability cannot be guaranteed:

- For machine-critical runs: reject or use a known structured-output decorator/repair path explicitly.
- For non-critical runs: allow fallback only with explicit opt-in.

4. Update tool approval planning.

Before wrapping a tool with `ApprovalRequiredAIFunction`, verify approval requests are supported for the selected provider/client.

If not supported:

- Use application-level pending approval before execution; or
- Block the tool for that run.

5. Update tests.

Tests must cover:

- Compatible Chat Completion structured output allowed.
- Incompatible client structured output rejected for machine-critical runs.
- Function tools supported does not imply approval supported.
- `ApprovalRequiredAIFunction` unsupported => mutation cannot execute silently.
- Responses/Foundry-like approval-capable provider supports approvals.

## Acceptance gate

The matrix must not block valid structured-output providers, and it must not allow approval-required tool execution on transports that cannot produce approval requests.

## Execution Result

Status: Complete. Provider features now distinguish structured output, JSON-schema response format, function tools, and approval-specific support.
