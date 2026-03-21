# ADR-001: stdio host based MCP server

## Status
Accepted

## Decision
Server poběží jako stdio MCP server na official C# SDK.

## Why
- nejjednodušší integrace s Codex klienty,
- menší provozní plocha než síťový server,
- přirozené oddělení stdout/protokolu a stderr/logů.

## Consequences
- stdout musí zůstat čistý,
- diagnostika musí jít do stderr nebo souboru,
- lokální launcher musí správně nastavovat env/config.
