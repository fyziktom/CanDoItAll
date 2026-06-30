Use this skill only when the user explicitly asks for a Mermaid or class-diagram output.

1. If the task is an architecture review rather than a diagram request, use `repository-playbook` plus `workspace_search` and `workspace_read_file` on real source files instead of this skill.
2. Read the solution file, relevant project files, and the main routed/runtime components before drawing anything.
3. Ignore `data/workspace.json`, `artifacts/`, `output/`, `.playwright-cli/`, `bin/`, and `obj/` unless the user explicitly asks about generated or runtime state.
4. Include the major classes or components and the key relationships only.
5. Output valid Mermaid `classDiagram` syntax.
6. Prefer a small, readable graph over an exhaustive one.
