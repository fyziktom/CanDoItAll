# Master prompt pro Codex

Jsi implementační agent pro solution `CanDoItAll`.

Tvůj úkol už není jen vytvořit `CanDoItAll.Mcp.SshOps`.
Musíš postupovat ve dvou povinných krocích:

1. vytvořit shared MCP foundation pro solution,
2. teprve potom implementovat `CanDoItAll.Mcp.SshOps`.

## Povinný target stav

### Shared foundation
V solution mají vzniknout nebo být doplněny tyto projekty:

- `CanDoItAll.Mcp.Core`
- `CanDoItAll.Mcp.LocalRuntime`

### Existing server refactor
Existující `CanDoItAll.Mcp.DotNetWatch` musí být přepojen tak, aby common primitives neimplementoval lokálně, ale používal shared foundation.

### New SSH server
Až po úspěšném shared refaktoru má vzniknout `CanDoItAll.Mcp.SshOps`.

## Pevná pravidla

1. Všechny komentáře ve zdrojových kódech musí být anglicky.
2. `Program.cs` v MCP serverech musí být minimální, bez business logiky.
3. stdout nesmí obsahovat nic mimo MCP protokol.
4. Nepřeskakuj shared foundation krok.
5. Nezaváděj duplicity common helperů mezi `DotNetWatch` a `SshOps`.
6. Do shared vrstev nevyváděj server-specific doménovou logiku.
7. Každý mutující tool musí být idempotentní nebo musí mít bezpečný fail mode.
8. Dlouhé operace musí být resumovatelné přes `operationId`.
9. Secret data nikdy neloguj ani nevracej v tool response.
10. Host key verification musí být first-class feature v SSH serveru.
11. Raw exec implementuj jen pokud je explicitně povolený configem a zůstane oddělený od běžných workflow.
12. Po každém větším refaktoru nebo fázi musíš udělat build, testy a self-review.

## Povinné pořadí práce

1. Projdi `20-shared-foundation/01-current-state-analysis.md`.
2. Potvrď nebo upřesni shared candidate inventory.
3. Vytvoř `CanDoItAll.Mcp.Core`.
4. Vytvoř `CanDoItAll.Mcp.LocalRuntime`.
5. Refaktoruj `CanDoItAll.Mcp.DotNetWatch`.
6. Proveď dotnetwatch regression gate.
7. Teprve potom scaffoldni `CanDoItAll.Mcp.SshOps`.
8. Implementuj SSH server po vrstvách podle roadmapy.

## Hotovo je až když

- shared projekty buildí,
- `CanDoItAll.Mcp.DotNetWatch` používá shared foundation bez zjevné regrese,
- `CanDoItAll.Mcp.SshOps` buildí a používá shared contracts,
- testy procházejí,
- veřejné tooly odpovídají kontraktům,
- dokumentace a příklady jsou konzistentní s implementací.
