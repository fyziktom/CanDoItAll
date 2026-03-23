# Let's Encrypt / ACME provozní poznámky

## Rizika
- rate limits při častých pokusech,
- poškozené nebo špatně permissionované `acme.json`,
- chybné DNS záznamy,
- blokovaný port 80/443,
- špatný cert resolver v labels.

## Doporučení
- nový hostname nejdřív testuj přes staging,
- teprve pak přepni production,
- `acme.json` drž jako persistentní soubor,
- neprovozuj více nezkoordinovaných writerů nad stejným ACME storage.

## Co má dělat MCP server
- umět zkontrolovat cert summary,
- dát srozumitelný hint při ACME failu,
- mít checklist a runbook pro staging -> production přechod.
