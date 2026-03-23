# Final approval after shared foundation update

## Stav
Balík po doplnění shared foundation sekce splňuje očekávání pro další implementační krok.

## Co je teď schválené
- shared-library-first postup,
- dotnetwatch-first regression gate,
- SSH implementation až jako druhá vlna,
- jasné boundary rules.

## Podmínky schválení
1. Shared foundation musí vzniknout dřív než první reálný SSH tool.
2. `CanDoItAll.Mcp.DotNetWatch` musí po refaktoru projít regresní kontrolou.
3. `CanDoItAll.Mcp.SshOps` nesmí zavádět lokální kopie common helperů.

## Verdict
**Schváleno.**
