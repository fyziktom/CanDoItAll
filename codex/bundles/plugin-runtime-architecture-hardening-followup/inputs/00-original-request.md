# Original Request

The user asked for a follow-up bundle after `C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-package-install`.

Raw request summary:

- Do architecture review of plugins and how they are connected.
- Validate plugins can work properly as expected.
- Use `analyzing-dotnet-performance` and `optimizing-ef-core-queries` to avoid anti-patterns and optimize for smooth, safe runtime behavior.
- Analyze plugin logging and whether plugin logs exist.
- Add access for listing plugin logs in the plugins page in its own subtab.
- Sort installation logs separately from runtime logs from plugin usage.
- Ensure the main implementation is generic and has no leftovers from the previous implementation where plugins were part of the plugins module, including possible workflow module leftovers.
- Prepare instructions for improving workflow canvas right-click executor menu.
- Plugin executors currently appear directly in the second menu layer under executors.
- Desired workflow canvas menu: one generic plugin icon in the second layer that opens a third layer with exact plugin executors.
- Office365 can have many executors, so it needs its own layer.
- Find and prepare icons for Docker, Office365, and Gmail.
- Each plugin must have an icon in the menu and a small icon in the workflow canvas executor node.
- Include a final subbundle that disables Docker as a default app plugin and prepares it as a ZIP for manual install testing.
- The Docker ZIP must be tested before handoff, and the app must end running without Docker registered as a default module.
- Prepare bundle only.
- Do not do implementation.
- Prepare a detailed checklist with references in XLSX so the implementation agent does not get lost.
