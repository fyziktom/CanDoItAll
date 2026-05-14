# Assumptions And Risks

## Assumptions

- The running CanDoItAll app can use the HTTP APIs documented by the current repo, with bearer auth checked through `/api/access/status` before mutation.
- `OPENAI_API_KEY` is the expected first credential source for OpenAI image generation.
- The first useful screenshot proof can run against Scenario 01 because it has a single, explicit `/inventory` page and a small Razor Pages host.
- Image provider preference belongs in agent configuration metadata and seed catalog models, not only in prompt text.
- File storage driver-backed assets can be created through existing project-structure asset endpoints; if those endpoints lack binary/image upload support, that gap becomes an implementation defect in subbundle 04.

## Critical Path Risks

- If image provider models are added only as prompt conventions, process agents will not be able to reason safely about allowed tools or preferred providers.
- If templates are imported without role and artifact expectations, runs may complete without screenshots or without storing them as project assets.
- If the first process is started before project-structure nodes and agent templates exist, failures will be ambiguous and hard to repair.
- If the multiple-page screenshot workflow starts/stops the app per page, it will not test the user’s key runtime constraint.

## Validation Risks

- `OPENAI_API_KEY` or image model access may be missing, which blocks live image-generation proof.
- Playwright MCP may be callable in seed config but still fail at runtime because of workspace policy, process start timing, or app URL discovery.
- Asset write success may not mean project-structure read projections include the asset node and content; readback is mandatory.
- Scenario routes with IDs, especially `/calibrations/{RecordId}`, require route discovery or seeded data lookup before screenshot capture.

## Reopen Triggers

- Reopen subbundle 01 if agent templates or layout generation cannot resolve an image provider preference without untyped parsing.
- Reopen subbundle 03 if process import/list proof shows missing step artifacts, roles, or prompt references.
- Reopen subbundle 04 if agents cannot access storage driver tools or create image asset nodes through project structure.
- Reopen subbundle 05 if Playwright screenshots exist on disk but are not attached as process outputs and project asset nodes.
- Reopen subbundle 06 if layout recommendations are stored as markdown notes instead of generated image assets, unless OpenAI credentials are explicitly blocked.
