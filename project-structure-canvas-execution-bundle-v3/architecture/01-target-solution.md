# Target Solution

The shipped target architecture keeps ownership split by runtime cost and product semantics.

- CanvasLib JavaScript owns canvas scene composition, dirty rendering, hit-zone projection, diagnostics, export composition, and browser-facing interaction hot paths.
- Blazor and C# own typed surface models, persistence, committed state, toolbox and dialog orchestration, and consumer-specific semantics.
- HTML remains for overlays, dialogs, context menus, help cards, floating windows, and the accessibility mirror.
- Shared asset includes are generated once and consumed by the web shell and the sandbox shell so the runtime script graph stays deterministic.
- ProjectStructure is the primary tuned adopter, PromptFactory is the secondary shared-consumer proof, and the sandbox benchmark route provides dedicated performance evidence.
