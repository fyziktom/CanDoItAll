# ADR-006: remote state and revision backups

## Status
Accepted

## Decision
MCP server bude na remote hostu ukládat operation state a revision backupy.

## Why
- rollback,
- audit trail,
- reconnect po přerušení.

## Consequences
- je potřeba cleanup/retention politika,
- disk usage musí být sledovaná.
