# Implementation Prompt

Implement one subbundle at a time from `C:\repositories\CanDoItAll\codex\bundles\project_structure_agent_node_tooling_bundle`.

- Preserve project-structure API and MAF tool compatibility.
- Use `WorkItem` plus `objectSubtype = "task"` for work task examples.
- Keep selected-node context concrete: selected node IDs must be present in prompt context before a tool can act on "selected nodes."
- For selected-node movement, include descendants by default, preserve internal links, and parent moved roots to the target project root.
- Update `reviews/01-execution-report.md` after each subbundle with commands, proof, and raw-note closure changes.
- Stop and repair the bundle if implementation reveals an unsupported raw note or weakened dependency assumption.
