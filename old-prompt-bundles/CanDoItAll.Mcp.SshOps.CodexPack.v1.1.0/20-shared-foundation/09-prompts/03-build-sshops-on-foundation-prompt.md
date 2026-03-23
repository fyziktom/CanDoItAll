# Prompt: build SSH Ops on top of shared foundation

Shared foundation už existuje a `CanDoItAll.Mcp.DotNetWatch` je na ní přepojené.

Teď scaffoldni `CanDoItAll.Mcp.SshOps` tak, aby od prvního commitu používal:
- shared envelope,
- shared errors,
- shared IDs,
- shared mutation gate,
- shared logging/redaction,
- shared operation primitives.

Všechny SSH-specific části drž lokálně v `CanDoItAll.Mcp.SshOps`.
Nezaváděj kopie common helperů.
