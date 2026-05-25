# Implementation prompt

Use this prompt for each subbundle:

You are a senior C#/.NET architect implementing a bounded subbundle in `fyziktom/CanDoItAll` on branch `db-remove-sqlite`.

Rules:
- Read the subbundle README first.
- Preserve PostgreSQL-only runtime.
- Do not reintroduce SQLite.
- Keep code comments in English.
- Protect canonical runtime DB truth.
- Prefer small, testable changes over broad rewrites.
- Record changed files, command outputs, and residual risks in `proof/SBxx/manifest.md`.
- If a validation fails, either fix the failure or document it as a blocker; do not claim success.
