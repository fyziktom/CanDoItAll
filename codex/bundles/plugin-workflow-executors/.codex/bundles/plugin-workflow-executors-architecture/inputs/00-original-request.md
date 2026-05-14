# Original Request

The request was to analyze the current CanDoItAll codebase after AI workflows and improved password/secret storage were added, and to decide whether the project is ready to add simple plugins that can be used as workflow executors.

The requested plugin direction includes:

- plugins as a separate module;
- clearly defined interfaces;
- plugins able to use existing services such as secret vault, file storage driver, project structure access, and future OAuth2;
- plugin-specific settings renderers/components;
- a plugin settings page and plugin catalog;
- bundled plugins now, with a future public plugin shop on a server where local instances can browse and install plugins;
- gradual addition of plugin tools such as OAuth2 routines for SaaS integrations;
- an architecture roadmap and detailed pre-plugin refactoring plan;
- a large Codex bundle with subbundles, checklists, file references, and recurring architecture reviews;
- final output as a ZIP artifact.
