# Provider Capability And Pricing

Provider configuration is part of the Agents API surface. Keep docs and skills aligned with these source files:

- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/AgentProviderModelParameterPolicy.cs`

## Capability Matrix Fields

`ProviderFeatureMatrix` currently records:

| Group | Fields |
| --- | --- |
| Core chat | `kind`, `transport`, `purpose`, `supportsStreaming`, `supportsTools`, `supportsStructuredOutput`, `preferFrameworkManagedChatHistory`, `supportsServiceManagedHistory` |
| Approval and background behavior | `supportsToolApprovalWrappers`, `supportsToolApprovalRequests`, `supportsApprovalRequiredAIFunction`, `supportsBackgroundResponses` |
| Native and hosted tools | `supportsNativeCodeInterpreter`, `supportsNativeFileSearch`, `supportsNativeWebSearch`, `supportsHostedMcpServer`, `supportsHostedTools`, `supportsHostedMcp`, `supportsLocalMcpBridge`, `supportsLocalMcp` |
| Model features | `supportsVision`, `supportsCompaction`, `supportsFunctionTools`, `supportsRunAsyncTypedOutput`, `supportsResponseFormatJsonSchema`, `supportsImageGeneration` |
| Guidance | `gitHubCopilotRecommendation` |

Do not infer tool support from provider kind alone. Use the feature matrix and provider health result.

## Profile Metadata

`ProviderProfile` includes `isPrivateProvider`, `modelPrices`, and `tags` in addition to transport, model, credential environment variable, and health fields.

Pricing metadata is persisted as JSON keys:

- `isPrivateProvider`
- `modelPrices`

Private providers default to low local/private price rows. OpenAI and Azure OpenAI default to the current GPT price rows in `ProviderPricingDefaults`. When pricing is edited, `ProviderPricingDefaults.NormalizeModelPrices` keeps one row per model and ensures the default model has a price row.

## Model Parameter Policy

OpenAI-like providers are `OpenAi` and `AzureOpenAi`. For OpenAI-like Responses transport models beginning with `gpt-5`, `o1`, `o3`, or `o4`:

- Temperature is omitted unless explicitly forced otherwise.
- `modelParameters.reasoningEffort` can be read from agent configuration first, then provider configuration.
- Supported reasoning effort values are `none`, `low`, `medium`, `high`, and `extraHigh`. The parser also accepts `extra-high`, `extrahigh`, `x-high`, and `xhigh`.

Invalid JSON containing `reasoningEffort` fails explicitly. Unsupported reasoning effort values throw with the supported values listed.
