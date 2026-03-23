# Shared foundation overview

## Proč tato sekce existuje

Původní SSH balík byl kvalitní, ale implicitně předpokládal, že `CanDoItAll.Mcp.SshOps` bude první server, který zavede common helpers.  
Aktuální repo ukazuje něco jiného: `CanDoItAll.Mcp.DotNetWatch` už tyto helpery ve velké míře implementuje.

Bez shared foundation by vznikl tento problém:

- `DotNetWatch` by měl jednu verzi common helperů,
- `SshOps` by měl druhou verzi common helperů,
- třetí MCP server by si musel vybrat, kterou kopii použije nebo zkopíruje třetí.

To je architektonicky špatně.

## Cíl sekce 20

Tato sekce definuje:

1. co přesně je už dnes v `CanDoItAll.Mcp.DotNetWatch`,
2. co z toho má jít do shared knihoven,
3. co naopak musí zůstat server-specific,
4. jaké shared projekty mají vzniknout,
5. jak migrovat existující `CanDoItAll.Mcp.DotNetWatch`,
6. jak teprve poté stavět `CanDoItAll.Mcp.SshOps`.

## Povinná zásada

Shared foundation se má navrhovat z **reálného repozitáře**, ne jen z ideálního diagramu.

Proto je první dokument této sekce `01-current-state-analysis.md`.
