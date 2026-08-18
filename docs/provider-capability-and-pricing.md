# Provider Capability And Pricing

Provider behavior is configuration, runtime policy, and cost-estimation input. Do not infer support or price from a provider name alone.

## Sources Of Truth

- [`ProviderModels.cs`](../src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs)
- [`ProviderPricingModels.cs`](../src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs)
- [`AgentProviderModelParameterPolicy.cs`](../src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/AgentProviderModelParameterPolicy.cs)
- [`ProviderRuntimeContracts.cs`](../src/MAF/Common/CanDoItAll.AgentFramework.Providers/Runtime/ProviderRuntimeContracts.cs)
- [`MafProviderRuntimeGateway.cs`](../src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs)

## Current Drivers

| Driver | Main use |
| --- | --- |
| OpenAI | Responses, Chat Completions, image, and supported OpenAI operations. |
| Azure OpenAI | Azure-hosted OpenAI chat operations. |
| Ollama | Local or remote Ollama chat and model maintenance. |
| ComfyUI | Workflow-backed image generation. |

Driver registration does not prove endpoint availability, credential validity, model installation, or health. Use the Agents API provider health/test operations before assigning a profile to production work.

## Feature Matrix

`ProviderFeatureMatrix` records:

| Group | Fields |
| --- | --- |
| Identity | `kind`, `transport`, `purpose` |
| Core execution | `supportsStreaming`, `supportsTools`, `supportsStructuredOutput`, `preferFrameworkManagedChatHistory`, `supportsServiceManagedHistory`, `supportsBackgroundResponses` |
| Approvals | `supportsToolApprovalWrappers`, `supportsToolApprovalRequests`, `supportsApprovalRequiredAIFunction` |
| Native and hosted tools | `supportsNativeCodeInterpreter`, `supportsNativeFileSearch`, `supportsNativeWebSearch`, `supportsHostedMcpServer`, `supportsHostedTools`, `supportsHostedMcp`, `supportsLocalMcpBridge`, `supportsLocalMcp` |
| Model features | `supportsVision`, `supportsCompaction`, `supportsFunctionTools`, `supportsRunAsyncTypedOutput`, `supportsResponseFormatJsonSchema`, `supportsImageGeneration` |
| Guidance | `gitHubCopilotRecommendation` |

Use this matrix together with provider health and the selected model. A capability supported by one transport or purpose is not automatically supported by another profile of the same provider kind.

`ProviderProfile` also carries:

- `isPrivateProvider`
- per-model prices
- tags
- suggested models
- health state and last-check time
- base URL, credential reference, default model, transport, purpose, and provider-specific configuration

## Model Parameter Policy

OpenAI-like providers are OpenAI and Azure OpenAI.

- GPT-5, o1, o3, and o4 model families omit temperature unless explicitly overridden by policy.
- Reasoning effort generally applies to those model families over Responses or
  Chat Completions transport. Request-shape compatibility can narrow it: OpenAI
  Chat Completions requests for `gpt-5.6-terra` that include function tools use
  explicit `none`. Select a Responses provider profile to retain configured
  reasoning with those tools.
- Agent configuration takes precedence over provider configuration.
- Accepted values are `none`, `low`, `medium`, `high`, `xhigh` (including the documented aliases), and `max`.
- `max` is restricted to GPT-5.6 models.
- Invalid JSON or unsupported values fail explicitly.

`maxOutputTokens` is read from agent configuration first, then provider configuration. The allowed maximum is 128,000 for OpenAI-like GPT-5 models and 8,192 otherwise.

Ollama additionally accepts `numPredict` or `num_predict` and `think`. Its defaults are 2,048 output tokens and thinking disabled. These settings remain subject to the selected Ollama model and server.

## Pricing Metadata

Each `ProviderModelTokenPrice` can include:

- input, cached-input, and output USD per million tokens
- cache-write USD per million tokens
- a long-context threshold
- long-context input, cached-input, cache-write, and output rates

Normalization keeps one row per model and ensures the default model has a row. Discovered provider models are merged with configured and known default rows; model discovery without explicit pricing does not invent a vendor price.

The values in `ProviderPricingDefaults` are planning defaults used by runtime cost estimation. They are not a live vendor price feed or invoice. Verify and update pricing metadata before relying on cost reports for financial decisions. Missing or unknown usage/pricing must remain visible instead of being reported as a confident zero cost.

Private/local provider defaults are estimates for comparative planning. They can represent infrastructure cost assumptions, not a claim that local inference is free.

## Managed Bootstrap Profiles

[`AppDatabaseBootstrapper`](../src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs) normalizes managed OpenAI chat profiles and seeds missing managed profiles for:

| Profile | Default |
| --- | --- |
| OpenAI default | Responses with `gpt-5.4-mini` |
| OpenAI chat completions | Chat Completions with `gpt-5.4-mini` |
| OpenAI image generation | `gpt-image-2` |
| Local ComfyUI Flux | `flux1-dev.safetensors`, `http://127.0.0.1:8188` |
| Local Ollama | `llama3.1`, `http://127.0.0.1:11434` |

OpenAI uses `OPENAI_API_KEY` or the runtime secret store. Managed local profiles may be seeded even when their external service or model is not installed; health checks make that state explicit.

## Security And Operations

- Store credentials in environment variables or the runtime secret store, never tracked JSON.
- Logs and API errors must redact credential values and bound provider detail.
- Test the exact profile/model/transport used by the agent.
- Recheck capability and pricing metadata after changing a provider model.
- Treat quota/billing, rate-limit, and general provider errors as distinct remediation paths.

See [Secure configuration](secure-configuration.md) and the [process operator runbook](process-agent-operator-runbook.md).

## Validation

Provider behavior changes should include focused lifecycle, policy, pricing, and driver tests:

```powershell
dotnet build src\MAF\Common\CanDoItAll.AgentFramework.Providers\CanDoItAll.AgentFramework.Providers.csproj --configuration Release
dotnet build src\MAF\Common\CanDoItAll.AgentFramework.Models\CanDoItAll.AgentFramework.Models.csproj --configuration Release
dotnet test tests\Solutions\CanDoItAll.Tests.Unit.slnx --configuration Release --list-tests --filter "FullyQualifiedName~ProviderRuntimeLifecycleTests|FullyQualifiedName~AgentProviderModelParameterPolicyTests|FullyQualifiedName~ProviderPricingTests"
dotnet test tests\Solutions\CanDoItAll.Tests.Unit.slnx --configuration Release --no-build --no-restore --filter "FullyQualifiedName~ProviderRuntimeLifecycleTests|FullyQualifiedName~AgentProviderModelParameterPolicyTests|FullyQualifiedName~ProviderPricingTests"
```

State the expected discovery count before the first command and reject zero or drifted
discovery. Run the [broad stable gate](testing.md#broad-stable-gate) only for CI,
release/merge closure, a frozen checkpoint, or a named invalidation trigger.
