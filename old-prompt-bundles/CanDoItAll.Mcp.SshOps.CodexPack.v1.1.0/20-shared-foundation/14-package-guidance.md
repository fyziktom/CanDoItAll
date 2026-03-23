# Shared foundation package guidance

## `CanDoItAll.Mcp.Core`
Preferuj minimum balíčků:
- `Microsoft.Extensions.*` jen pokud je potřeba pro logging/options abstractions,
- žádný `SSH.NET`,
- žádný Docker/Traefik/IPFS/PostgreSQL specific package,
- žádný přímý dependency na MCP SDK, pokud to není nutné.

## `CanDoItAll.Mcp.LocalRuntime`
Povoleno:
- `Microsoft.Extensions.*`
- BCL

Nepovoleno:
- `SSH.NET`
- remote/domain-specific balíčky

## `CanDoItAll.Mcp.DotNetWatch`
Povoleno:
- `ModelContextProtocol`
- shared foundation references
- jen dotnetwatch-specific balíčky, pokud by byly nutné

## `CanDoItAll.Mcp.SshOps`
Povoleno:
- `ModelContextProtocol`
- `SSH.NET`
- shared foundation references
- případné další remote/domain-specific balíčky

## Praktické pravidlo
Když si nejsi jistá, dej balíček raději do server-specific projektu než do shared foundation.
