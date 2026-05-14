# Target Solution

## Core Boundary

Process core remains an orchestration engine. It must not gain code paths for screenshots, Playwright, OpenAI images, scenario routes, layout design, or ComfyUI. Those details belong in provider profiles, agent capability metadata, process templates, step notes, prompt resources, and asset tools.

## Provider And Agent Model

- Add strongly typed image-generation provider configuration alongside existing provider profiles or as a clearly named provider capability subtype.
- Seed an OpenAI image-generation provider profile with `OPENAI_API_KEY`, OpenAI base URL, and default image model `gpt-image-1-mini`.
- Add a future ComfyUI-compatible extension point without adding a silent fallback that hides missing OpenAI capability.
- Add agent configuration metadata for:
  - whether image generation is allowed;
  - the preferred image provider profile ID;
  - the default image model;
  - whether generated images may be stored into project-structure assets.
- Keep agent-level preference explicit and read/writeable through existing catalog/editor/API paths.

## Process Templates

- Add `app-page-screenshot` for one page.
- Add `app-pages-screenshot-set` for multiple pages where the app is started once, pages are captured, and the app is stopped after the set.
- Add a layout-generation process that consumes screenshot asset nodes and produces generated image asset nodes.
- Add roles and prompts for screenshot capture, screenshot review/storage, and layout generation.
- Each template must define artifact expectations for captured screenshots, review findings, storage receipts, and generated layout assets.

## Project Structure

- Create three CanDoItAll projects, one per scenario app.
- Each project gets nodes for app description, technology/runtime, source root, pages/routes, and delivery.
- The delivery block must contain description `get screenshot of app pages`.
- Add process nodes under the delivery block for screenshot capture and later layout generation.
- Captured screenshots and layout recommendations are file/image asset nodes, not free-form notes.

## Runtime Proof

- Start with Scenario 01 because it has one route and a small app host.
- The screenshot process must start the app, capture `/inventory` through Playwright MCP, review the image, store it through file storage/project structure, and read back the asset node/content.
- The layout-generation process must use the stored screenshot reference as input and produce a generated layout image asset when credentials allow.
