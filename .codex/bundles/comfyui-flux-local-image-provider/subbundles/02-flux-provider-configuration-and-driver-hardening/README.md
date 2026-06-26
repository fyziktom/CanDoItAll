# Flux Provider Configuration And Driver Hardening

## Status

- `Completed`

## Objective

- Make ComfyUI Flux a durable CanDoItAll image-generation provider path by improving the existing driver/provider configuration and adding focused tests without breaking the existing provider-runtime architecture.

## Covered Inputs

- `N001`: existing basic ComfyUI driver.
- `N005`: must use the Flux workflow.
- `N006`: analyze the actual driver.
- `N007`: design architecture improvements and implement driver changes.
- `N008`: add provider for ComfyUI.

## Prerequisites

- SB01 is `Completed`.
- SB01 manifest proves live Flux generation from `ImageGenerationFlux.json`.
- No SB01 blocker remains open.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedNormalizer.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs`
- `repo://tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ComfyUiProviderDriverTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProviderFeatureMatrixTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`
- `bundle://inputs/sample/ImageGenerationFlux.json`

## Deliverables

- Minimal driver hardening for Flux-style workflow configuration if current code is insufficient.
- A seeded or managed provider profile for local ComfyUI Flux image generation.
- Focused tests proving Flux workflow mutation, provider seed shape, and explicit failure behavior.
- SB02 semantic invariant contract and proof manifest.

## Dependency Impact

- SB03 depends on this subbundle because project-structure proof must select a real enabled ComfyUI image provider.
- Provider runtime behavior and catalog UI depend on this subbundle not misclassifying ComfyUI as a chat/tool provider.

## Validation Depth

- Critical foundation with unit/integration proof, source assertions, semantic positive and negative cases, anti-stub audit, and one downstream smoke check.

## Implementation Steps

1. Re-read the driver and tests after SB01 to compare live Flux payload requirements with current options.
2. Add typed constants/options or a small helper for Flux defaults only if it reduces magic string spread and keeps configuration explicit.
3. Add a ComfyUI Flux provider seed/configuration with default model name, base URL, workflow JSON or path policy, node ids, timeout, and image-only purpose.
4. Add driver tests using the Flux workflow shape with positive node `56:51`, sampler node `56:52`, size node `56:50`, output node `9`, and no negative text node.
5. Add or update seed/feature tests proving ComfyUI Flux is an enabled image provider and not a chat/tool provider.
6. Run focused tests and source assertions.
7. Record transcripts, hashes, semantic invariants, anti-stub audit, and execution-report rows.

## Scope Exceptions

- Do not build a new ComfyUI client abstraction unless the existing driver cannot express the Flux workflow safely.
- Do not make live ComfyUI availability a normal unit-test dependency.

## Do Not Do

- Do not introduce fallback from ComfyUI to OpenAI or another provider.
- Do not hardcode local machine paths in production seed data unless the provider configuration model already treats them as editable local defaults.
- Do not move project-structure image-generation logic into the provider project.

## Acceptance Checklist

- `Passed`: Flux workflow test proves prompt, seed, size, and output node handling.
- `Passed`: Negative prompt is optional for Flux and no required negative text node is introduced.
- `Passed`: Provider seed test finds an enabled `ProviderKind.ComfyUi` image-generation provider.
- `Passed`: Failure-mode tests remain explicit for missing workflow, bad HTTP response, timeout, and source-image rejection.
- `Passed`: Focused test transcript exits with code `0`.
- `Passed`: SB02 proof manifest includes changed-file hashes and portable source references.

## Proof Required

- `proof/SB02/transcripts/failing-first-flux-provider.txt`
- `proof/SB02/transcripts/passing-focused-tests.txt`
- `proof/SB02/transcripts/anti-stub-audit.txt`
- `proof/SB02/source-assertions.md`
- `proof/SB02/semantic-invariants.md`
- `proof/SB02/manifest.md`
- Downstream smoke proof: `proof/SB02/transcripts/comfyui-flux-seed-integration-test.txt`

## Browser Validation Logging

- Route/window: `N/A`; this is provider/source/test proof.
- Viewport: `N/A`.
- Actions/assertions: source assertion, unit/integration test execution, provider catalog/seed resolution.
- Screenshots/artifacts: `N/A`.
- Review questions: Does project structure remain decoupled from ComfyUI HTTP details, and does provider discovery expose only image-generation use?

## Progression Gate

- `Passed`: SB03 may start because SB02 is `Completed`, focused tests pass, and a usable ComfyUI Flux image provider is available to the runtime/project-structure path.

## Suggested Agent Prompt

```text
Implement SB02 only after SB01 passes. Make the smallest provider/driver change that makes Flux ComfyUI explicit and testable. Keep project-structure code out of the driver, avoid fallback behavior, and record artifact-backed critical proof before moving to SB03.
```
