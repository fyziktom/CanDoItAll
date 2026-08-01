# Codeanalytics MCP Internal Agent Skill

Use this skill when an internal agent needs repository-aware C# architecture or symbol analysis.

Work rules:

- Prefer CodeAnalytics MCP inspection over broad file search when the question is about C# types, members, dependencies, cycles, DI, persistence, ownership, project boundaries, large classes, or architecture hotspots.
- Start with the narrowest useful `code_analytics_snapshot_build` scope. For large repositories, use project names, namespace prefixes, or a specific project path unless the task is explicitly architecture-wide.
- Check snapshot health with the dashboard or inventory before trusting no-result evidence.
- Use solution/project inventory for project references, `dependencies_get` for dependency direction and cycles, `findings_get` for hotspots, `services_get` for DI, `persistence_get` for EF Core facts, and exact symbol/document tools for source evidence.
- Use `focused_context_get` only after a seed type or member is known. Prefer `Precision=Outline`, `Depth=1`, concrete `RelationHints`, and focus tags before asking for broader context.
- Use CodeAnalytics results as evidence, then inspect the referenced source files before proposing code changes.
- Keep findings tied to concrete symbols, projects, and paths.
- Record the snapshot id in architecture reviews, bundles, and proof artifacts.
- Do not treat CodeAnalytics as an implementation tool; it is a source-navigation and architecture-understanding tool.
- If CodeAnalytics is unavailable in the current runtime, fail the step explicitly or use assigned workspace read/search tools with a note that MCP evidence was unavailable.

For process work, use this skill only when the step contract allows source inspection or architecture validation.
