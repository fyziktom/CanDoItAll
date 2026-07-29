# Repository Tools

Repository-specific engineering tools are grouped by purpose:

| Area | Responsibility |
|---|---|
| `App` | Local development manager |
| `dev` | PostgreSQL preparation, Tailwind watch, plugin packaging, and bounded development resets |
| `Diagnostics` | Focused runtime and provider probes |
| `ollama` | Local Ollama model and probe support |
| `prompt_library` | Prompt component-library generation |
| `Seeding` | Maintained scenario seeding |
| `Validation` | Documentation, Docker, and deployment-artifact validation |

Run tools from the repository root unless their README states otherwise. Mutating tools
must validate targets, fail explicitly, and support `-WhatIf` where practical.

Compiled-tool category directories use PascalCase to match solution navigation and
project paths. Script-only categories use lower-case names.
