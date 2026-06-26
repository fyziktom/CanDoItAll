# SB02 Source Assertions

## Flux Defaults And Typed Keys

- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs:5` defines `ComfyUiProviderConfigurationKeys` so ComfyUI configuration keys are not duplicated as ad hoc strings.
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs:28` defines `ComfyUiFluxProviderDefaults` for the local Flux provider.
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs:35` pins the Flux positive prompt node to `56:51`.
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs:38` pins the Flux output node to `9`.
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs:45` embeds the provided Flux workflow template using `flux1-dev.safetensors`.
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs:176` creates the serialized provider configuration from typed constants.

## Driver Behavior

- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs:131` still mutates the configured positive prompt node through `SetRequiredInput`.
- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs:142` and `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs:146` keep seed handling explicit.
- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs:154` and `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs:159` map requested size into the configured width/height node inputs.
- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs:163` validates that a configured output node exists before enqueueing.
- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs:230` keeps output download filtering tied to the configured output node.
- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs:386` aliases option keys to `ComfyUiProviderConfigurationKeys`.

## Provider Seed

- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:39` creates a stable local ComfyUI Flux provider id.
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:492` seeds `Local ComfyUI Flux` as `ProviderKind.ComfyUi`.
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:504` stores the Flux workflow configuration generated from `ComfyUiFluxProviderDefaults`.
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:509` sets the provider purpose to `ImageGeneration`.
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs:512` tags the provider as local ComfyUI Flux image generation.

## Tests

- `repo://tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ComfyUiProviderDriverTests.cs:101` proves the Flux defaults mutate prompt, seed, and size without requiring a negative prompt text node.
- `repo://tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ComfyUiProviderDriverTests.cs:189` proves a missing configured output node fails before `/prompt`.
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs:139` proves the seeded local ComfyUI Flux provider is enabled, image-only, and configured with the Flux workflow.

## Hashes

- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs`: `5571B2C6734E7CB7ACBE7C778AD7BEC954A0423CE701CE0F7A914E98EAA2B662`
- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs`: `7BC3A9AC2FC049D2055834775C1C02E45F0499C75AEBCDF9A0A8EB3FD5D0597E`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`: `5C9BC774C90940DAD6660CB9F1B8930DB7D3CEDE1786A99DF78E1C6151679DFE`
- `repo://tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ComfyUiProviderDriverTests.cs`: `D40165598F7176D41AFA979032E8C41805C037EEE3A3B9521CC98BB27006A360`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`: `520530C03BE2ABA21418AF555265A0610C106EB3792252D1C33F04F562D66830`
