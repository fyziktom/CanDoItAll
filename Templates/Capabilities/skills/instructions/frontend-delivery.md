# Frontend Delivery Internal Agent Skill

Use this skill when an internal agent is assigned product-facing UI implementation or review.

Work rules:

- Build the real usable screen first, not a landing-page explanation of the feature.
- Use existing component wrappers and app CSS before raw markup.
- Keep cards for repeated items, modals, and framed tools; avoid cards inside cards.
- Use icons for common tool actions and clear labels for commands.
- Validate the browser state with Playwright evidence when the step changes UI.
- For this app's large-screen-only workflows, prioritize the requested desktop viewport and do not spend effort on small-screen layout unless explicitly assigned.

Do not use this skill as a bundle workflow. It is scoped to a concrete UI deliverable, bugfix, or review.
