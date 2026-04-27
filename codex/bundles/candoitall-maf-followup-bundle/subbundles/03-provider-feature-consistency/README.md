# Subbundle 03 — Provider feature consistency

## Problem

The central feature matrix now correctly recognizes that compatible OpenAI/Azure Chat Completions profiles can support JSON schema response format, while tool approval is narrower. However workspace-backed provider persistence still stores structured-output support using a stale Responses-only rule.

## Evidence

- `ProviderServices.cs:149-178` computes:
  - `SupportsResponseFormatJsonSchema` for OpenAI/Azure `Responses` or `ChatCompletions`.
  - `SupportsStructuredOutput = supportsResponseFormatJsonSchema`.
  - `SupportsToolApprovalRequests` only for OpenAI/Azure `Responses` with tools.
- `WorkspaceBackedAgentProviderProfileRegistry.cs:139` still sets:

```csharp
entity.SupportsStructuredOutput = model.Transport == ProviderTransportKind.Responses;
```

## Required change

Use the central feature matrix to compute provider capability flags wherever possible.

Possible implementation approach:

1. Normalize/create the provider profile from the editor model.
2. Resolve `ProviderFeatureMatrix` from `ProviderProfileService.ResolveFeatureMatrix(...)`.
3. Persist `entity.SupportsStructuredOutput = matrix.SupportsStructuredOutput`.
4. Keep UI/API flags consistent with the same source.

## Transport persistence

The registry maps OpenAI Chat Completions based on provider name:

```csharp
OpenAiProviderAdapter.PluginKey when IsOpenAiChatCompletionsProvider(provider) => ProviderTransportKind.ChatCompletions
```

Persist the selected `ProviderTransportKind` in provider metadata/settings. Read that metadata first when mapping back to `ProviderProfile`. Keep the name-based check only as a migration fallback.

## Tests

Add `ProviderFeatureMatrixTests` covering:

- OpenAI + Responses supports structured output, function tools, tool approval, hosted tools.
- OpenAI + ChatCompletions supports structured output and function tools but not tool approval/hosted tools.
- Azure OpenAI + ChatCompletions same expected structured-output behavior.
- Ollama + ChatCompletions does not claim MAF JSON schema structured output unless there is explicit supported proof.
- Workspace-backed provider persistence stores `SupportsStructuredOutput` equal to the central matrix.
- Provider transport round-trips through metadata and does not depend on provider display name.

## Status

Completed. Proof is recorded in `../../reviews/01-execution-report.md`.

## Requirements Owned

R04, R05.

## Prerequisites

None.

## Dependency Impact

Critical foundation for approval/tool composition and verification claims about provider capability behavior.

## Validation Depth

Provider matrix unit tests plus registry persistence tests or targeted code proof for capability flags and explicit transport metadata round-trip.

## Progression Gate

Downstream work may continue only after provider capability persistence and UI/API flags use the central feature matrix and selected transport no longer depends on display-name inference except as a legacy fallback.
