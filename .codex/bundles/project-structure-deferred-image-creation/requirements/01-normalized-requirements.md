# Normalized Requirements

| ID | Requirement | Observable Success Criteria |
| --- | --- | --- |
| R1 | Generated-image create must transfer prompt textarea text to the selected provider. | A test records `AgentImageGenerationRequest.Prompt` equal to the typed prompt. |
| R2 | Generated-image create must transfer provider id, model override/default, size, quality, and output format. | Tests assert provider id, model, size, quality, and `AgentGeneratedImageFormat` on the provider request. |
| R3 | The project structure must create a canonical image asset node immediately after save. | The node exists in the persisted structure before provider completion and has a stable `custom:` node id. |
| R4 | The immediate node must display a waiting image. | The node has image media content and a placeholder filename/content containing "Waiting for Image creation by AI...". |
| R5 | Provider completion must update the same node with generated media. | The persisted node id remains unchanged and its media filename/content type changes to the generated image. |
| R6 | Provider failure must update the same node to an explicit failure state. | The node status/progress/metadata record failure details and the app logs provider/action state. |
| R7 | Deferred completion must be generic enough for future project-structure asynchronous enrichment. | The queue/request model is typed by completion kind and is not hard-coded into canvas JavaScript. |
| R8 | Canonicity must remain in `ProjectWorkbenchService`. | Node creation and media replacement go through workbench service methods and DB-backed bindings. |
| R9 | The normal path must avoid unnecessary full graph reloads. | Existing patch methods are reused for immediate create and completion where the page can observe it. |
| R10 | The 5032 app must be rebuilt, restarted, and browser-tested through the right-click generate-image path. | Playwright proof records provider options, prompt entry, create action, waiting node, and completion or explicit ComfyUI blocker. |
