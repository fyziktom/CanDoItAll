# Watch And Playwright Loop Internal Agent Skill

Use this skill when an internal agent must prove browser-visible behavior.

Work rules:

- Start from a running app URL supplied by the process or workspace tool result.
- Use Playwright MCP actions for navigation, clicks, form input, snapshots, screenshots, and console inspection.
- Treat screenshots alone as weak proof; pair them with DOM assertions, visible text, state changes, or tool/API results.
- Capture browser evidence after the app has restarted or hot reload has settled.
- If the app was stopped while a browser tab was open, navigate fresh before evaluating console messages.

For this app's large-screen-only validations, use the assigned desktop viewport and record it in the proof.
