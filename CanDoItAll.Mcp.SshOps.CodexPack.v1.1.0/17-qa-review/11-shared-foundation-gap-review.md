# Shared foundation gap review

## Kontext
Po obdržení aktuálního stavu solution `CanDoItAll` už nestačí hodnotit jen kvalitu SSH návrhu samotného.
Je nutné hodnotit i to, zda návrh vede k udržitelné více-serverové MCP architektuře.

## Zjištěné mezery v původním SSH balíku
1. neobsahoval current-state audit nad existujícím `CanDoItAll.Mcp.DotNetWatch`,
2. nevynucoval shared-library-first postup,
3. neměl dotnetwatch regression gate,
4. neměl extrakční matici typů,
5. neměl explicitní dependency rules pro shared layer.

## Důsledek
Bez těchto doplnění by bylo příliš snadné:
- postavit SSH server s vlastní kopií envelope/logging/wait helperů,
- zhoršit maintainability solution,
- odhalit regresi dotnetwatch až pozdě.

## QA verdict
**Původní verze 1.0.0 nebyla dostatečná pro multi-server MCP architekturu.**
