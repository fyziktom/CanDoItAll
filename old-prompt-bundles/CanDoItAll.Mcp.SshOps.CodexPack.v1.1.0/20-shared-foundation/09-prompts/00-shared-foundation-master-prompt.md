# Prompt: shared foundation master prompt

Pracuješ v solution `CanDoItAll`.

Nejdřív musíš vytvořit shared foundation pro MCP servery, ne nový SSH server izolovaně.

Povinné cíle:
- vytvořit `CanDoItAll.Mcp.Core`,
- vytvořit `CanDoItAll.Mcp.LocalRuntime`,
- migrovat existující `CanDoItAll.Mcp.DotNetWatch`,
- projít regression gate,
- teprve potom pokračovat na `CanDoItAll.Mcp.SshOps`.

Pevná pravidla:
1. Komentáře ve zdrojových kódech musí být anglicky.
2. Shared vrstva nesmí obsahovat server-specific doménovou logiku.
3. Nepřidávej reference ze shared projektů na Web/Infrastructure/Modules.
4. Nezaváděj druhou verzi common helperů v SSH projektu.
5. Po každé fázi buildni, otestuj a proveď self-review.
