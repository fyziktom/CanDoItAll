# Components MCP Internal Agent Skill

Use this skill when an internal agent works on CanDoItAll Blazor UI or shared component usage.

Work rules:

- Prefer existing CanDoItAll component wrappers and BaseLib/CanvasLib patterns before writing raw HTML elements.
- Check component intent, parameters, states, and accessibility before changing a page.
- Keep components focused on rendering and orchestration; move non-trivial logic into services.
- Preserve existing Radzen usage only where the project already uses it.
- Validate component behavior with component tests when possible and Playwright evidence for browser-visible changes.

Do not use this as general frontend design advice. Use it for concrete CanDoItAll component-library work.
