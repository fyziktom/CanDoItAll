# Current provider domain and runtime

Prepared against CanDoItAll `development` commit
`1625b336e4f60ddb64987240c3a3dc485591d20f` (inspected Git tree `da6da849abd3dd7b9895431e92c6a2e0c9b8e4da`).

## Provider models are internal runtime models

`src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs` owns the
AgentFramework `ProviderProfile`. It carries runtime information such as provider kind, base
URL, credential reference, default model, transport, capability flags, pricing, tags, health,
and provider configuration.

`ProviderKind` currently distinguishes OpenAI, Azure OpenAI, Ollama, and ComfyUI. Runtime
transport distinguishes Responses from Chat Completions, and purpose distinguishes chat from
image generation.

These types are not suitable public network DTOs. In particular, internal request records in
`ProviderRequestContracts.cs` embed the complete `ProviderProfile` and may carry binary
attachments. Exposing them would leak implementation and secret-resolution details.

## Capability-specific driver layer

`CanDoItAll.AgentFramework.Providers` contains capability contracts for health, model catalog,
chat, streaming, image generation, speech, and model maintenance. The driver registry selects
one driver by provider kind and capability.

This is useful for central execution and isolated provider behavior, but it is not itself a
remote provider-sharing protocol:

- request records are internal;
- the registry is keyed mainly by provider kind/capability, not publication;
- chat requests do not model the complete evolving OpenAI wire surface;
- a relay must preserve client-side tool-call semantics instead of executing client tools
  centrally.

## Real agent path still branches by ProviderKind

`MafProviderAgentFactory` builds OpenAI/Azure/Ollama clients directly according to
`ProviderKind`. Therefore merely registering a new driver is insufficient for ordinary agent
invocation.

The target avoids a new cross-cutting `ProviderKind.Shared` branch. An imported shared text or
image profile projects to the existing OpenAI-compatible runtime kind while retaining
`provider.candoitall-shared` as its connector/origin identity. The central service handles the
actual upstream-specific adaptation.

## Runtime gateway and legacy path

`MafProviderRuntimeGateway` uses concrete provider drivers for health, test chat, image, and
maintenance operations. A Workspace-level `ProviderRuntimeGateway`/adapter path also exists.

The feature must select one canonical runtime path and keep any legacy Workspace behavior as a
thin facade. Implementing separate shared-provider logic in both pathways would create
divergent capability, error, streaming, and security semantics.

## Key implication

Provider sharing needs three explicit boundaries:

1. public catalog/wire protocol;
2. central relay/adaptation;
3. client-side runtime projection.

None of these should serialize the internal provider request records.
