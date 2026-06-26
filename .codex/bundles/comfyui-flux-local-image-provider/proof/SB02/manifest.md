# SB02 Proof Manifest

## Summary

- Subbundle: `SB02`
- Status: `Completed`
- Owned requirements: `R001`, `R003`, `R005`, `R006`, `R007`, `R008`
- Owned raw notes: `N001`, `N005`, `N006`, `N007`, `N008`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Changed File Manifest

| Path | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs` | `new` | `5571B2C6734E7CB7ACBE7C778AD7BEC954A0423CE701CE0F7A914E98EAA2B662` |
| `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs` | `modified` | `7BC3A9AC2FC049D2055834775C1C02E45F0499C75AEBCDF9A0A8EB3FD5D0597E` |
| `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs` | `modified` | `5C9BC774C90940DAD6660CB9F1B8930DB7D3CEDE1786A99DF78E1C6151679DFE` |
| `repo://tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ComfyUiProviderDriverTests.cs` | `modified` | `D40165598F7176D41AFA979032E8C41805C037EEE3A3B9521CC98BB27006A360` |
| `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs` | `modified` | `520530C03BE2ABA21418AF555265A0610C106EB3792252D1C33F04F562D66830` |

## Proof Artifact Hashes

| Path | SHA-256 |
| --- | --- |
| `bundle://proof/SB02/transcripts/failing-first-flux-provider.txt` | `17EA19F6051DF9EFBE9FED257B17B87CB0B0A16EB33FA5834E0957B758576311` |
| `bundle://proof/SB02/transcripts/passing-focused-tests.txt` | `8832CFEFD47B2095A84D5071D311ACCFA7EAA65939C3B528A9A5AFDB490B6D80` |
| `bundle://proof/SB02/transcripts/anti-stub-audit.txt` | `8AF48202D8945CE3386FBD4CD81AF1EB0F5F9FB3074F495703D4630C06B1BCD0` |

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-flux-provider.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing-focused-tests.txt`
- Driver unit transcript: `bundle://proof/SB02/transcripts/comfyui-driver-focused-tests.txt`
- Seed integration transcript: `bundle://proof/SB02/transcripts/comfyui-flux-seed-integration-test.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Source-Level Assertions

- `bundle://proof/SB02/source-assertions.md`

## Semantic Adequacy

- Shipped behavior: local ComfyUI Flux is an explicit seeded image-generation provider with typed Flux configuration, driver output-node validation, and focused tests.
- Source proof: `bundle://proof/SB02/source-assertions.md`.
- Test proof: `bundle://proof/SB02/transcripts/passing-focused-tests.txt`.
- Shallow-pass trap: a provider seed without Flux workflow JSON or image-only purpose would not satisfy the seed integration test.
- Adversarial negative proof: `bundle://proof/SB02/transcripts/failing-first-flux-provider.txt`.
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`.

## Downstream Smoke

- `bundle://proof/SB02/transcripts/comfyui-flux-seed-integration-test.txt` proves project-structure/runtime code can resolve an enabled image-generation provider before SB03.
