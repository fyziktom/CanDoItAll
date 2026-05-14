# Original Request

The user asked Codex to use the `candoitall-bundle-workflow` skill to solve a complex real scenario test that should also improve the app and expose bugs.

Raw requested work:

1. Add provider profiles for image generation AIs. First default provider is OpenAI API with a cheaper image model such as `gpt-image-1-mini`; later providers such as ComfyUI should be addable. Agents must be able to have image generation as a default allowed tool, similar to project access, and each agent can have a preferred image provider.
2. Add new CanDoItAll web-app projects for each scenario app from `C:\programovani\candoitall-dev-55-output`. Each project structure must contain basic app-description nodes, page-information nodes, and a delivery block with the description `get screenshot of app pages`.
3. Create process templates for acquiring a screenshot of one app page and for acquiring screenshots of multiple pages. The multiple-page template must keep the app running and not start/stop it for every screenshot. Load those templates into processes.
4. Add an agent template specialized for running .NET or JavaScript apps and using Playwright MCP for page screenshots. Add another agent template that reviews images captured by the first agent and then adds them into project structure as process outputs and image asset nodes. It must use the file storage driver to store assets properly.
5. When ready, add a process node under that delivery block and start the process for the first app. Observe whether agents can use Playwright MCP, capture screenshots, validate them, and add them into project structure. Repair and improve failures.
6. After screenshot processes pass and screenshots are captured by agents through processes, add another process and specialized agent for creating improved layouts based on those screenshots. This workflow must read stored screenshot information from project structure, pass it to an image-generation agent using the OpenAI image provider, and store layout recommendations as new image assets in project structure.

Explicit architecture constraint:

- Process core must remain generic. Specific instructions must live in process-step descriptions, agent instructions, skills, or tools. The process core must stay flexible for many kinds of processes.
