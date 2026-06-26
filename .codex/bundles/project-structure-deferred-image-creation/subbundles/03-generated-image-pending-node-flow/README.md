# Generated image pending node flow

## Status

- `Completed`

Closure proof: `proof/SB03/manifest.md` and `proof/SB03/semantic-invariants.md`.

## Objective

Convert generated-image asset creation to create an immediate canonical waiting node, enqueue background image generation, and update the same node when completion arrives.

## Success Criteria

- The create handler returns after creating the node and enqueuing completion, not after waiting for provider bytes.
- The placeholder image contains `Waiting for Image creation by AI...`.
- The node is an `ImageAsset` with subtype `generated`.
- The completed image replaces the placeholder on the same node.
- Existing create placement, parent link, selection, and surface patch behavior remain intact.

## Covered Inputs

- Requirements R3, R4, R5, and R9.

## Prerequisites

- SB01 prompt/provider contract gate passed.
- SB02 deferred completion and media replacement gate passed.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ImageGeneration.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureCreateRequestComposer.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProjectStructurePageSimpleMutationTests.cs`

## Deliverables

- Generated-image create flow changed to placeholder-first.
- Placeholder media generator.
- Enqueue call for generated image completion.
- Component tests proving immediate node and eventual update.

## Dependency Impact

- SB04 browser proof depends on this flow being correct through the same right-click route the user uses.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Create placeholder upload payload with waiting text.
2. Create the generated image node immediately with prompt notes and metadata.
3. Enqueue typed generated-image deferred completion.
4. Patch the created node into the surface immediately.
5. When tests run with a worker/processor, prove completion updates the same node.

## Scope Exceptions

- Real ComfyUI latency and output quality are proven in SB04.

## Do Not Do

- Do not rework create dialog dropdown behavior.
- Do not block the page on `ImageGenerationService.GenerateAsync`.
- Do not create a second node for the completed image.

## Acceptance Checklist

- [ ] Node exists before delayed fake provider completes.
- [ ] Placeholder media route is an image preview.
- [ ] Provider request still has prompt/options.
- [ ] Same node id after completion.
- [ ] Failure path remains explicit.

## Proof Required

- Component test transcript.
- Source assertions in `proof/SB03/manifest.md`.
- Browser proof requirements handed off to SB04.

## Browser Validation Logging

- Browser proof owned by SB04, but this subbundle must define expected visible states: provider dropdown populated, prompt textarea accepted, waiting image node created, same node updated or failure marked.

## Progression Gate

- Do not start final browser validation until component tests prove immediate waiting node creation and same-node completion.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
