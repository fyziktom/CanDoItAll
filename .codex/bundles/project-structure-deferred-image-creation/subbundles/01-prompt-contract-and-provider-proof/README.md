# Prompt contract and provider proof

## Status

- `Completed`

Closure proof: `proof/SB01/manifest.md` and `proof/SB01/semantic-invariants.md`.

## Objective

Prove and, if needed, repair the generated-image form-to-provider contract so prompt textarea text and image options reach the selected image provider exactly.

## Success Criteria

- The generated-image create path sends textarea notes as `AgentImageGenerationRequest.Prompt`.
- Provider id, model, size, quality, and output format are transferred without fallback masking.
- ComfyUI Flux driver proof remains valid for positive prompt node `56:51`.

## Covered Inputs

- User concern that the calculator-app prompt may not have reached the provider.
- Requirement R1 and R2.

## Prerequisites

- Prepared bundle validated.
- Existing source references still exist.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Providers/Drivers/ComfyUiProviderDriver.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Models/Providers/ComfyUiProviderDefaults.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/AgentFramework/Providers/ComfyUiProviderDriverTests.cs`

## Deliverables

- Updated or retained component test proving project-structure prompt/options transfer.
- Any minimal code fix if proof shows a missing or incorrect field.
- Proof notes in `reviews/01-execution-report.md`.

## Dependency Impact

- SB02 and SB03 depend on this proof. If the provider request is wrong, deferred completion would only make the wrong request asynchronous.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inspect current generated-image create request mapping.
2. Update tests to account for deferred behavior while preserving prompt/options assertions.
3. If a mapping bug is found, fix only that mapping.
4. Run targeted tests covering generated image prompt/options transfer.

## Scope Exceptions

- Visual quality of the generated image is not proof of prompt transfer.
- Live ComfyUI proof belongs to SB04.

## Do Not Do

- Do not change dropdown rendering behavior.
- Do not alter ComfyUI Flux defaults without a failing proof showing they are wrong.

## Acceptance Checklist

- [ ] Prompt assertion passes.
- [ ] Provider id assertion passes.
- [ ] Model assertion passes.
- [ ] Size/quality/format assertions pass.
- [ ] ComfyUI Flux unit test still asserts node `56:51`.

## Proof Required

- Targeted component test transcript.
- Relevant source assertions in `proof/SB01/manifest.md`.
- Existing or rerun ComfyUI driver unit proof.

## Browser Validation Logging

- N/A for this subbundle. Browser proof is owned by SB04.

## Progression Gate

- Do not start SB02 until the prompt/options contract is either proven correct or repaired with passing targeted tests.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
