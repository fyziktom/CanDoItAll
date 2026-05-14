# layout-image-generation-workflow

## Status

- `Completed`

## Objective

Add and prove a process/agent workflow that reads stored screenshot asset information from project structure, uses the preferred OpenAI image-generation provider to create improved layout recommendations, and stores generated layouts as new image asset nodes.

## Success Criteria

- Layout-generation process template or process node exists and references stored screenshot assets as inputs.
- Specialized layout image-generation agent can read screenshot asset metadata/content and resolve its preferred image provider.
- OpenAI image-generation call succeeds and stores a generated image asset, or a precise credential/model blocker is recorded.
- Generated layout recommendation assets read back through project structure.

## Covered Inputs

- R12, R1, R2, R3.
- Raw note `N007` plus provider/process genericity constraints.

## Prerequisites

- Subbundle 01 closure gate passed.
- Subbundle 05 closure gate passed with Scenario 01 screenshot asset readback.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\manifest.json`

## Deliverables

- Layout-generation process/template instructions.
- Layout image-generation agent template or seeded agent.
- Process node under the Scenario 01 delivery area.
- Generated image asset node and readback proof, or explicit OpenAI credential/model blocker.

## Dependency Impact

- This is final closure for the user’s image-generation workflow.
- If provider credentials are missing, the feature should still be structurally ready but final proof must honestly stop at provider-health/blocker.

## Validation Depth

- `End-to-end image-generation closure`

## Implementation Steps

1. Add a layout-generation process template or instantiate a process definition using existing generic templates if sufficient.
2. Add or bind a layout image-generation agent with preferred OpenAI image provider metadata.
3. Read Scenario 01 screenshot asset references from project structure.
4. Start the layout-generation process.
5. Validate OpenAI provider health/model access before generation.
6. Generate improved layout image recommendations using screenshot content as input.
7. Store generated layouts as project-structure image asset nodes.
8. Read back asset metadata/content and update execution report.

## Scope Exceptions

- If `OPENAI_API_KEY` is absent or the requested model is unavailable, do not fake image generation. Record the blocker and leave the workflow structurally complete.
- Do not add ComfyUI calls in this phase.

## Do Not Do

- Do not store layout recommendations only as text when image generation succeeds.
- Do not send unrelated project data to the image provider.
- Do not add OpenAI-specific logic to process core.

## Acceptance Checklist

- [x] Layout process/agent can identify stored screenshot inputs.
- [x] Preferred OpenAI image provider resolves from typed metadata.
- [x] Generated layout image asset is stored and read back, or credential/model blocker is recorded.
- [x] Process core remains generic.

## Proof Required

- Process run detail.
- Provider health/model-access proof.
- Generated image asset node/content readback, or blocker proof showing missing credentials/model access.
- Browser/image review of generated layout if an image is produced.

## Browser Validation Logging

- Route: N/A unless a generated-layout preview UI is used.
- Asset proof: record source screenshot asset ID, generated layout asset ID, storage path/content readback, and generated image preview path.

## Progression Gate

- Final closure may pass when generated layout asset readback succeeds.
- Final closure may also pass with an explicit OpenAI credential/model blocker only if the app-side workflow is otherwise complete and readback-ready.

## Closure Proof

- Layout process definition: `46ae9763-f793-4935-bbad-9b39e795fddb`.
- Runtime layout agent: `91bde847-4b81-486f-8fd3-820edbd2c17c`.
- Successful repaired layout run: `e73b3e93-c478-4033-853c-08a67257d323`.
- Source screenshot asset: `custom:ed5db391937e4c17b15641e60770b30b`.
- Generated layout asset: `custom:50980b65bb61471c9b361fa472881869`.
- Managed layout storage: `managed-files/project-media/images/3569901cdcc24f88a08a01801bfae9b9/02-generated-layout-inventory-9b10e4121ac7417a854fb02ea19788ce.png`.
- Evidence: `evidence/scenario-01-layout-repaired-run-detail-final.json`, `evidence/scenario-01-layout-repaired-execution-tool-evidence.json`, `evidence/scenario-01-layout-repaired-asset-node.json`, and `evidence/scenario-01-layout-repaired-asset-content.json`.
- Generic repair proof: process tool validation now recognizes completed internal MAF tool invocations for `project_structure_*`, `process_*`, and `image_generation_*` tools only after a succeeded structured step outcome.

## Suggested Agent Prompt

```text
Implement only the layout-image-generation-workflow subbundle.
Use stored screenshot asset information from project structure as source input, resolve the agent preferred OpenAI image provider, generate layout recommendation images when credentials allow, store the outputs as image asset nodes, and update the execution report. Keep process core generic.
```
