# Shared foundation gap review

## Role
Přísná senior QA / engineering manager review.

## Co v původním SSH balíku chybělo
Původní SSH balík byl kvalitní, ale z pohledu multi-server architektury měl tyto mezery:

1. **Chybějící explicitní analýza aktuálního `CanDoItAll.Mcp.DotNetWatch`**
   - hrozilo, že se SSH server začne psát nad hypotetickým designem místo nad reálným stavem repozitáře.

2. **Chybějící shared-library gate**
   - nebylo explicitně řečeno, že common helpery se musí extrahovat dřív, než vznikne další MCP server.

3. **Chybějící dependency rules**
   - bez nich by shared layer snadno zdegenerovala v dumping ground.

4. **Chybějící dotnetwatch regression gate**
   - hrozilo, že se chyba objeví až při implementaci SSH serveru.

5. **Chybějící extrakční matice**
   - bez ní by Codex improvizoval, co přesunout a co nechat lokálně.

## Verdict
Původní SSH pack byl silný pro doménu SSH/DevOps, ale neřešil dostatečně MCP platformizaci napříč servery.

**Schválení pouze po doplnění shared foundation sekce.**
