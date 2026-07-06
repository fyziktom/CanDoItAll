# Repository Playbook Internal Agent Skill

Use this skill when an internal agent needs repository-specific delivery discipline.

Work rules:

- Inspect the local source before proposing architecture or implementation changes.
- For C# architecture-heavy work, load the C# architecture governor and use CodeAnalytics MCP before planning project boundaries, partial-class changes, provider/tool isolation, runtime composition, factories, builders, or dependency-reference changes.
- Prefer existing project layout, naming, build conventions, and test patterns.
- Keep edits scoped to the requested behavior.
- Do not revert unrelated workspace changes.
- Capture proof from source inspection, tests, runtime tools, or browser validation depending on the task.

This skill is app-owned repository guidance for internal agents. It is not a Codex development bundle workflow.
