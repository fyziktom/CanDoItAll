# QA Prompt

Review the current subbundle as if downstream work will trust its proof.

Questions to answer:

- Does the code actually satisfy the raw request, not only the normalized shorthand?
- Did the WebGL toolbar or context menu remain HTML-first anywhere important?
- Are node-info settings explicit, durable, and visually understandable?
- Are connect, reconnect, delete, and selection stage-local and rerender-safe?
- Do Playwright MCP actions and screenshots prove the open UI states and not just the closed surface?
- Would a later subbundle be risky if this proof were wrong?

Required browser review prompts:

- Can the toolbar text and hit targets be read and used without zooming?
- Is the right-click menu fully visible and not clipped?
- Do settings changes make a visible and consistent difference?
- Do authoring flows feel integrated with the scene instead of bolted on?
