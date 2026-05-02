# QA Prompt

Validate the Mermaid bundle against the original request, not just the code diff.

Required checks:

- Confirm the wrapper package uses official Mermaid v11.14.0 as a static web asset and does not build Mermaid from source.
- Confirm `/groups/mermaid` renders flowchart and architecture-beta SVG content.
- Click a rendered node and confirm the Blazor callback log updates with node details.
- Exercise zoom and pan controls or wheel/drag behavior and confirm the SVG viewport changes.
- Trigger invalid syntax and confirm visible error details include a message and location/excerpt when Mermaid exposes them.
- Confirm the Mermaid MCP tools return syntax rules, architecture-beta guidance, and forbidden-symbol guidance by graph type.

Visual questions for the sandbox page:

- Can all text be read without browser zoom?
- Is anything overlapping, clipped, or visually colliding?
- Are the diagram, editor, controls, callback log, and error panel aligned and sized intentionally?
- Does the page fit the existing sandbox visual system?
- At narrow width, does the editor/diagram/log stack without clipped controls?
