# Normalized Requirements

| ID | Requirement | Owner | Proof |
| --- | --- | --- | --- |
| R1 | Add typed image-generation provider profiles with OpenAI as the first seeded provider and `gpt-image-1-mini` as the default image model unless provider validation proves it unavailable. | `01-image-provider-profile-foundation` | Provider/editor/API readback and targeted tests. |
| R2 | Add typed agent image-tool access metadata so each agent can allow image generation and choose a preferred image provider. | `01-image-provider-profile-foundation` | Agent editor/readback and metadata serialization tests. |
| R3 | Preserve process core genericity; no process-runtime code should become scenario, screenshot, Playwright, or OpenAI specific. | all | Code review and diff audit. |
| R4 | Create CanDoItAll projects for all scenario apps under `C:\programovani\candoitall-dev-55-output`. | `02-scenario-project-structure-seeding` | Project API/project-structure readback. |
| R5 | Each scenario project must include description, technology/runtime, source-root, page/route nodes, and a delivery block described as `get screenshot of app pages`. | `02-scenario-project-structure-seeding` | Project-structure readback of affected nodes. |
| R6 | Add screenshot process templates for a single page and multiple pages. | `03-screenshot-process-template-pack` | Template pack validation and API template list/detail/import proof. |
| R7 | The multiple-page template must start/run the app once and capture pages without stop/start per screenshot. | `03-screenshot-process-template-pack` | Template step notes and first multi-page dry-read validation. |
| R8 | Add a screenshot-capture agent template that can run .NET or JavaScript apps and use Playwright MCP. | `04-screenshot-agent-template-and-asset-storage` | Agent catalog readback and capability verification. |
| R9 | Add a screenshot-review/storage agent template that reviews captured images and stores accepted screenshots as image asset nodes using file storage. | `04-screenshot-agent-template-and-asset-storage` | Agent catalog readback and asset write/read proof. |
| R10 | Add a process node under the first scenario delivery block and run the screenshot process end to end. | `05-first-scenario-runtime-proof` | Run detail, step artifacts, Playwright screenshot, asset readback. |
| R11 | Repair observed runtime failures in provider access, Playwright capability, process templates, or asset storage without adding process-core special cases. | `05-first-scenario-runtime-proof` | Regression tests and execution-report repair notes. |
| R12 | Add a layout-generation process and agent that uses stored screenshot assets as source instructions for the OpenAI image provider and stores layout recommendations as image assets. | `06-layout-image-generation-workflow` | Layout process run proof or explicit OpenAI credential/model blocker plus asset readback when available. |
