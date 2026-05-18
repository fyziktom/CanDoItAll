# 01 Voice Driver Core

## Status

Completed

- Subbundle status: `Completed`

## Objective

Add the provider-neutral AgentFramework voice project and first OpenAI TTS/STT driver with exact factory selection, secure credential resolution, settings models, and automated tests.

## Covered Inputs

- MAF wrapper voice driver as own project.
- Provider interfaces/factory for OpenAI now and local models later.
- OpenAI API first implementation.
- OpenAI TTS/STT can use the same API key.

## Prerequisites

- Bundle readiness gate passed.
- Official OpenAI audio docs reviewed.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\CanDoItAll.AgentFramework.Hosting.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Providers\Credentials\SecretStoreAgentProviderCredentialResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowCatalogModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`

## Deliverables

- New `CanDoItAll.AgentFramework.Voice` project included in the solution.
- Voice driver interfaces, request/result records, settings records, and exact-driver factory.
- OpenAI STT implementation for `audio/transcriptions`.
- OpenAI TTS implementation for `audio/speech`.
- Voice application service that resolves effective settings and credentials without exposing raw keys.
- Unit tests for settings normalization, factory selection, request construction, and error behavior.

## Dependency Impact

- Blocks all UI and Cognitive Memory voice work.
- Downstream phases must reopen this subbundle if they need contract changes to avoid provider-specific leakage.

## Validation Depth

- Build new project.
- Run targeted unit tests.
- No live OpenAI calls in automated tests.
- Confirm no raw API key is stored in serialized settings or agent metadata.

## Implementation Steps

1. Add voice models/settings to shared AgentFramework model layer where they must serialize with settings or agent metadata.
2. Add new voice project and project references.
3. Implement `IAgentVoiceService`, `IAgentVoiceDriverFactory`, `ISpeechToTextDriver`, and `ITextToSpeechDriver`.
4. Implement OpenAI driver using `HttpClient`, explicit endpoints, bearer auth, JSON/multipart request construction, and typed validation.
5. Register the voice services in AgentFramework module DI.
6. Add unit tests with fake HTTP handlers and fake credential/provider inputs.

## Scope Exceptions

- Local model driver implementation is explicitly deferred.
- Realtime streaming duplex audio is explicitly deferred.

## Do Not Do

- Do not put OpenAI HTTP calls directly in Razor components.
- Do not silently switch STT/TTS drivers when the configured driver fails.
- Do not store raw API keys in persisted JSON.

## Acceptance Checklist

- [x] New project exists and is listed in `CanDoItAll.slnx`.
- [x] OpenAI driver can synthesize and transcribe through typed interfaces.
- [x] Factory rejects unsupported or missing drivers.
- [x] Settings support separate STT/TTS drivers/providers and shared provider credentials.
- [x] Unit tests cover success and predictable failure cases.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~Voice`
- `dotnet build CanDoItAll.slnx --no-restore`

## Browser Validation Logging

- N/A for this subbundle; no browser-visible UI changes.

## Progression Gate

- Do not proceed to subbundle 02 until the new voice contracts and service build cleanly and unit tests prove request construction and exact-driver behavior.

## Suggested Agent Prompt

Implement the AgentFramework voice driver core. Keep OpenAI behind provider-neutral interfaces, use existing provider credential resolution, write unit tests without network calls, and stop if downstream UI would need OpenAI-specific types.
