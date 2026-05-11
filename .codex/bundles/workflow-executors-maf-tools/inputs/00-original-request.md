# Original Request

The user asked to use the CanDoItAll bundle workflow to add executors in MAF workflows.

Key raw notes preserved for implementation:

- Workflows need a way to execute code/tool steps during workflow execution.
- First typical tools: access files through the storage driver; access project structure; get one node, a tree, or full tree info from a point; add asset node with artifact by type selector such as Mermaid, JSON, image, Markdown.
- Add generic HTTP/HTTPS fetch executor.
- Add AI image generation executor using existing image providers.
- Add Excel read/write executors. Use the same library pattern as `C:\programovani\Aqualectra\pve-invoicing-connector`, specifically ClosedXML, but wrap it in a new small project library named `CanDoItAll.Tools.Documents` so ClosedXML can be replaced later and PDFs/DOCX can be added there later.
- Think about setup needs for Excel: get/write cell, multiple reads/writes, and workflows such as difficult xlsx input to simple Markdown report output.
- Identify other generic executor tools that are obvious users will need.
- In the workflow canvas right-click menu, add executors as a second layer of the right-click menu and also into a component toolbox similar to the project-structure canvas.
- Do not fully integrate plugins now, but prepare executor architecture for later custom plugins, including setup UI renderer contracts.
- Include details for timeouts, retries, and non-happy paths.
- Prepare a detailed bundle with subbundles, architecture reviews, and implementation proof.
- Use an xlsx plan to keep source references, requirements, dependencies, and validation tracking aligned.
- Use the Microsoft Agent Framework durable workflows article and the local `C:\repositories\agent-framework` clone.
- Properly test workflows with at least 20 real-world examples; use `gpt-5-mini` and test `gptoss20b64k` through local Ollama.
