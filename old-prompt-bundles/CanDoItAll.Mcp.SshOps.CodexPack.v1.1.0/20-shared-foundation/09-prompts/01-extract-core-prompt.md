# Prompt: extract MCP core

Vytvoř `CanDoItAll.Mcp.Core` a přesuň do něj pouze stabilní common primitives.

Začni těmito kandidáty:
- shared response envelope a error model,
- server/correlation/operation identity helpery,
- mutation gate,
- log entry / log read / ring buffer / file log store,
- secret redactor,
- generické async operation primitives,
- shared HTTP/TLS probe helpery.

Po každém přesunu:
- oprav namespaces a references,
- buildni řešení nebo dotčené projekty,
- zkontroluj, že jsi nevytáhla server-specific logiku do core.
