# 07 - Provider Approval Capability Proof

## Problem

The current feature matrix appears to allow approval wrappers only for OpenAI/Azure Responses transport. Official MAF documentation demonstrates function tool approval with Azure OpenAI Chat Completion. Verify the installed package behavior and align provider truth.

## Required implementation

1. Inspect the installed MAF package/version and the actual approval APIs available.
2. Create a unit/integration-style proof using `ApprovalRequiredAIFunction` with the provider/client type used for Chat Completions, without making a live model call if possible.
3. If approval works for Azure/OpenAI Chat Completions, update `ProviderFeatureMatrix` and `ProviderFeatureMatrixTests`.
4. If approval is not supported in the installed package or current adapter, document the exact limitation and keep the matrix strict.
5. Ensure mutation tools are never exposed when approval is required but unavailable.

## Acceptance criteria

- Provider feature matrix matches actual MAF behavior, not assumptions.
- Tests cover Responses and Chat Completions approval capability.
- Documentation explains any divergence from current official docs.

## Suggested test names

- `AzureChatCompletionsApprovalCapabilityMatchesMafPackage`
- `OpenAiChatCompletionsApprovalCapabilityMatchesMafPackage`
- `MutationApprovalToolsAreRejectedWhenProviderCannotApprove`

## Execution status

Completed. Provider feature matrix and MAF runtime tests cover approval-required function wrapping for OpenAI/Azure OpenAI Chat Completions when tools are enabled.
