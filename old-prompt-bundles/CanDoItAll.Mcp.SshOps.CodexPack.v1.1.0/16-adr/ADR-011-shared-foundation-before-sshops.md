# ADR-011: shared MCP foundation before SSH Ops

## Status
Accepted

## Context
V solution `CanDoItAll` už existuje implementovaný `CanDoItAll.Mcp.DotNetWatch`.
Další MCP server by bez shared foundation téměř jistě duplikoval common helpery.

## Decision
Před implementací `CanDoItAll.Mcp.SshOps` vzniknou shared projekty:

- `CanDoItAll.Mcp.Core`
- `CanDoItAll.Mcp.LocalRuntime`

Existující `CanDoItAll.Mcp.DotNetWatch` se na ně přepojí ještě před SSH implementací.

## Consequences
### Positive
- menší duplicita,
- menší regresní riziko do budoucna,
- lepší základ pro další MCP servery.

### Negative
- vyšší upfront cost,
- nutnost udělat regression gate nad dotnetwatch.

## Rationale
Druhé MCP řešení je správný okamžik zavést společnou platform layer.
Po třetím serveru by už bylo pozdě a dražší.
