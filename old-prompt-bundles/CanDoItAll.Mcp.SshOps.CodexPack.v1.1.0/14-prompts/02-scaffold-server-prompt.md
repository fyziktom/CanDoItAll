> **Revize 1.1.0 – prerequisite**  
> Tento prompt používej až po dokončení shared foundation fáze a po regression gate nad `CanDoItAll.Mcp.DotNetWatch`.  
> Nový SSH server musí od prvního commitu používat shared projekty místo vlastních kopií common helperů.

# Prompt: scaffold server

V solution `CanDoItAll` vytvoř nový projekt `CanDoItAll.Mcp.SshOps`.

Požadavky:
- target framework `net10.0`,
- official MCP C# SDK,
- hosting přes stdio,
- options classes pro settings,
- root složky minimálně:
  - `Configuration`
  - `Security`
  - `Transport`
  - `Operations`
  - `Tools`
  - `Domain`
  - `Observability`

Přidej:
- minimální `Program.cs`,
- settings model,
- options validation,
- placeholder tool registry,
- health/startup validation.

Nakonec:
- přidej projekt do solution,
- buildni solution,
- napiš krátké shrnutí scaffoldu.
