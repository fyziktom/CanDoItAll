# Implementation Prompt

Execute the current subbundle from `bundle://subbundles` only after reading `bundle://README.md`, `bundle://plan/01-phase-plan.md`, `bundle://traceability/01-requirement-traceability.md`, and the current subbundle README.

Keep the change minimal:

- Move only active MCP source/test/helper projects into `C:\repositories\CanDoItAll.Mcp`.
- Keep main-repo settings and skills in `C:\repositories\CanDoItAll`.
- Keep `tools/Reinstall-CanDoItAllMcps.ps1` in the main repo and make its MCP source root explicit.
- Record command transcripts under `bundle://proof/SBxx/transcripts`.
- For critical subbundles, write `bundle://proof/SBxx/manifest.md` and `bundle://proof/SBxx/semantic-invariants.md` before marking closure complete.

Do not broaden scope into application modules, web UI, suppressed MCP servers, or unrelated `.artifacts` cleanup.
