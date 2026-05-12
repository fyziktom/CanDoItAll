# QA Prompt

Review the completed bundle against the raw request.

Checks:

- Workflow API route list includes explicit lifecycle and import/export commands.
- New API tests exercise success and failure paths.
- New skill has required `name` and `description` frontmatter and concise trigger language.
- Reinstall script syncs `codex\skills` recursively, and the local user skill folder contains the new workflow skill.
- Browser validation is marked N/A only because the work is API and skill setup, not UI.
- Raw notes N001-N005 are closed with proof or explicit blocker text.
