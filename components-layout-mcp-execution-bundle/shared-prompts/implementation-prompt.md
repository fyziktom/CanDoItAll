# Implementation Prompt

Implement only the current subbundle.

- Prefer shared CanDoItAll components over custom structural markup.
- Treat `Grid` as the owner of track definitions, `Row` as a nested grid that inherits or overrides those tracks, and `Column` as the content-alignment and span primitive.
- Keep custom CSS for visual identity only when a shared component or prop cannot express the structure.
- When updating the component MCP, preserve existing component metadata and extend it with practical guidance and real examples rather than replacing it with ad-hoc prose.
- When updating the installer, modify the normal repo-managed install path instead of creating a one-off script.
