# Shared Prompt — Playwright And Screenshot Proof

```text
Use Playwright MCP to validate the current subbundle.

Required browser proof style:
- Start with a large desktop viewport or maximized headed window.
- Capture screenshots that are actually reviewed, not just attached.
- When layout changes, run a narrower-width follow-up pass.
- When tabs, dropdowns, dialogs or overlays changed, capture them in the open state.

For each route under test, record:
- route
- viewport
- main Playwright actions
- assertions
- screenshot path
- visual findings

Always answer:
- Is the primary task obvious?
- Is there any clipped, overlapping or duplicated UI?
- Are unread/badge/status states visible and understandable?
- Does the page clearly show what is business-facing versus technical?
- Does the deep link preserve context?
- If this is a process or collaboration screen, can the operator understand what to do next without knowledge of the old sandbox host?

Use FrontendSkill-style screenshot review discipline:
- check spacing and alignment,
- check text density and scanability,
- check tab semantics and hierarchy,
- check destructive/approval actions are visually distinct,
- check transcript/message history remains readable.

Treat missing screenshot analysis as failed proof.
```
