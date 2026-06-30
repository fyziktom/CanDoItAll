# Codeanalytics MCP Internal Agent Skill

Use this skill when an internal agent needs repository-aware C# architecture or symbol analysis.

Work rules:

- Prefer codeanalytics MCP inspection over broad file search when the question is about types, dependencies, DI, persistence, or ownership.
- Use codeanalytics results as evidence, then inspect the referenced source files before proposing code changes.
- Keep findings tied to concrete symbols, projects, and paths.
- Do not treat codeanalytics as an implementation tool; it is a source-navigation and architecture-understanding tool.
- If codeanalytics is unavailable in the current runtime, fail the step explicitly or use assigned workspace read/search tools with a note that MCP evidence was unavailable.

For process work, use this skill only when the step contract allows source inspection or architecture validation.
