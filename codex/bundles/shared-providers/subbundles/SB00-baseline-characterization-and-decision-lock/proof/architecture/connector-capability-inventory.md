# SB00 Workspace connector and capability inventory

Captured: 2026-08-24  
Evidence mode: static source characterization  
Product behavior changed by this artifact: **No**

## Registry behavior

`ProviderRegistry` is defined in
`src/Modules/CanDoItAll.Modules.Workspace/Providers/ProviderExecution.cs:66`.
It constructs a case-insensitive plugin-key dictionary, so duplicate plugin keys fail during
registry construction. It also maintains a legacy-kind lookup for old rows without a connector
key. `ListManifests` returns manifests sorted by display name.

Production registration is in
`src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceModuleServiceCollectionExtensions.cs:19-28`.

## Registered Workspace adapters

| Adapter symbol | Plugin key | Manifest capability | Secret requirement | Health operation | Effective use |
| --- | --- | --- | --- | --- | --- |
| `OpenAiProviderAdapter` | `provider.openai` | Provider execution, agent exposure | Required bearer API key | `GET /models` | OpenAI-compatible Responses/basic provider calls; also the Workspace connector origin used for Azure metadata profiles. |
| `ScenarioHarnessProviderAdapter` | `provider.scenario-harness` | Provider execution, agent exposure | None | deterministic `scenario-harness` check | Test/scenario execution, registered and therefore visible through manifest enumeration. |
| `ProcessMockProviderAdapter` | `provider.process-mock` | Provider execution, agent exposure | None | deterministic `process-mock` check | Process-flow mock execution, registered and therefore visible through manifest enumeration. |
| `ComfyUiProviderAdapter` | `provider.comfyui.local` | Provider execution, agent exposure | None | `GET /system_stats` | Image-generation-only Workspace adapter. Generic chat send returns validation failure. |
| `OllamaProviderAdapter` | `provider.ollama.local` | Provider execution, agent exposure | None | `GET /api/tags` | Local Ollama generation/model discovery. |
| `OllamaRemoteProviderAdapter` | `provider.ollama.remote` | Provider execution, agent exposure | None | `GET /api/tags` | Remote Ollama; delegates execution/discovery to the local Ollama adapter implementation. |

Source anchors:

- OpenAI manifest: `ProviderExecution.cs:149-179`;
- scenario harness: `ProviderExecution.cs:452-475`;
- process mock: `ProviderExecution.cs:521-546`;
- ComfyUI: `ProviderExecution.cs:590-620`;
- local Ollama: `ProviderExecution.cs:653-676`;
- remote Ollama: `ProviderExecution.cs:810-835`.

The Workspace provider UI is manifest/schema driven. It lists `WorkspaceService.ListProviderManifests`
and renders `SelectedProviderManifest.ConfigurationSchema.Fields` in
`src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor:293-378`.
This is the correct extension point for a typed shared connector; source/import-managed fields
must not be exposed as freely editable raw endpoint/key fields.

## Azure status

There is no `provider.azure-*` Workspace adapter or manifest. Azure support exists in the inner
provider runtime through `AzureOpenAiProviderDriver`, but Workspace persistence represents an
Azure profile using:

- `ConnectorPluginKey = provider.openai`;
- `providerKind = AzureOpenAi` in AgentFramework metadata;
- explicit transport/purpose metadata.

Evidence:

- Agent UI enumerates all `ProviderKind` values at
  `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor:98`;
- `AgentFrameworkProviderMetadata.ResolveConnectorPluginKey` maps both OpenAI and Azure OpenAI
  to `OpenAiProviderAdapter.PluginKey` at
  `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderMetadata.cs:393`;
- `AgentFrameworkProviderMetadata.BuildExtraSettingsJson` persists kind/transport/purpose at
  `AgentFrameworkProviderMetadata.cs:88`;
- `WorkspaceAgentProviderProfileMapper.Map` reads the metadata over the connector fallback at
  `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceAgentProviderProfileMapper.cs:22`;
- concrete Azure runtime support is owned by
  `src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/AzureOpenAiProviderDriver.cs` and
  registered in
  `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs`.

Therefore the prepared assumption "Azure may be driver-only" is amended: Azure is configurable
through the AgentFramework provider editor, but not through a distinct Workspace connector
manifest. Editing such a row through the older Workspace-only editor risks collapsing its
AgentFramework metadata back to the OpenAI fallback.

## Workspace-to-runtime mapping constraints

`WorkspaceAgentProviderProfileMapper` contains explicit connector-key switches for kind,
transport, purpose, defaults, and fallback tags:

- `ResolveMappedProviderKind`, `WorkspaceAgentProviderProfileMapper.cs:205`;
- `ResolveLegacyMappedTransport`, `:226`;
- `ResolveLegacyMappedPurpose`, `:250`;
- `ResolveDefaultModel`, `:118`.

The mapper evaluates a connector fallback before metadata overrides, so an unknown connector key
throws even if extra settings contain a valid effective provider kind/transport. The shared
connector therefore needs one explicit outer mapping to an effective OpenAI-compatible profile.
Do not add `ProviderKind.Shared` to inner models or branch ordinary MAF execution on origin.

Locked shared connector projection:

- origin/connector key: `provider.candoitall-shared`;
- effective kind: `ProviderKind.OpenAi`;
- effective transport and purpose: validated catalog/import snapshot;
- endpoint: central OpenAI-compatible inference base;
- credential reference: source secret-record reference;
- default model: public routing model ID;
- capabilities/models: catalog-derived and not freely editable;
- tags: shared/source/publication state with no secret or private endpoint data.

## Voice/audio status

There is no separate Workspace audio manifest or provider purpose. Agent voice settings select an
enabled OpenAI chat profile at
`src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentVoiceSettingsPanel.razor:138,311`.
`ProviderRuntimeVoiceDriver` leases the ordinary provider runtime and resolves the typed speech
drivers at
`src/MAF/Common/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs:6,37,72`.
Of the current concrete providers, `OpenAiProviderDriver` implements speech-to-text and
text-to-speech at
`src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs:326,376`.

Therefore audio cannot be inferred merely because a source profile projects to OpenAI-compatible
chat. A shared catalog needs explicit audio capability evidence before voice settings may offer
that profile; otherwise the typed driver call must fail predictably.
