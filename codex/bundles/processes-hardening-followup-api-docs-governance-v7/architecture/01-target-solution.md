# Target solution

The target solution keeps Processes as the authoritative lifecycle/governance layer above Workflows and agent tools.

## Runtime shape

- Persisted definition contracts, import/export DTOs, API/tool request models, templates, and docs use the same typed operation and artifact mapping fields.
- Runtime transitions and automation finalizers use shared artifact validation instead of divergent manual/API and automation paths.
- Tool policy consumes the grounded target alias ledger as the authority source for mutation and read-only decisions.
- Health/read/detail surfaces expose typed block causes, recovery options, artifact validation diagnostics, and operation contract state.
- Templates and red-team scenarios stay generic across software and non-software process domains.

## Non-negotiable constraints

- Keep PostgreSQL-only persistence; do not add SQLite runtime paths or migrations.
- Keep Workflows below Processes.
- Prefer typed contracts and enums over reason-text inference or display-string parsing.
- Keep refactoring checkpoints as dependency gates, not optional cleanup.
