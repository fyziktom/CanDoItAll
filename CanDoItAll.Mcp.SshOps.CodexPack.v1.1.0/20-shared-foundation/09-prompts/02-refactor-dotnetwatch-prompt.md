# Prompt: refactor existing CanDoItAll.Mcp.DotNetWatch

Přepoj `CanDoItAll.Mcp.DotNetWatch` na `CanDoItAll.Mcp.Core` a `CanDoItAll.Mcp.LocalRuntime`.

Povinné cíle:
- odstranit duplicitu common helperů,
- zachovat stejné tool names,
- zachovat stdout discipline,
- zachovat app lifecycle a operation semantics.

Nedělej:
- změny názvů veřejných toolů,
- velké doménové přepisy nad rámec shared extrakce,
- současnou implementaci SSH serveru.

Po refaktoru proveď regression gate podle checklistu.
