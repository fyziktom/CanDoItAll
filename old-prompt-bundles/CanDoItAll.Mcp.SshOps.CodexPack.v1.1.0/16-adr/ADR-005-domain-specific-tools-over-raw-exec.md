# ADR-005: domain specific tools over raw exec

## Status
Accepted

## Decision
Veřejné MCP API bude doménově orientované. Raw exec bude oddělený a defaultně zakázaný.

## Why
- menší riziko zneužití,
- lepší determinismus pro Codex,
- lepší validace a idempotence.

## Consequences
- musíme explicitně navrhnout tool surface,
- některé neobvyklé zásahy zůstanou mimo MVP.
