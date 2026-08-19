# Shrnutí revize pro architekta

## Verdikt

Větev `unix-adoption` na připraveném commitu `e282446daa2b775b93f2d70ea7fc0e282e26d802` obsahuje kvalitní a rozsáhlou implementaci Core i Runtime portability. Základní architektura není potřeba přepisovat. Před merge však zůstávají tři skutečné P0 blokery a několik P1 hardening úkolů.

## Co je provedeno správně

- Kanonické logické cesty jsou oddělené od host-bound fyzických cest.
- Filesystem policy řeší case sensitivity, containment, link/reparse traversal, atomické zápisy, locking a Unix permissions.
- Windows používá DPAPI; Unix má poctivě označený `LocalUserFile` fallback s úrovní `BasicLocal`.
- Silnější explicitní providery failují uzavřeně a `ExternalWrappingKeyFile` poskytuje použitelný headless základ.
- Host capabilities rozlišují povinné a volitelné funkce; headless web nevyžaduje desktop.
- Runtime execution má jeden nízkoúrovňový process host, exact process identity a oddělené ownership registry.
- Workbench, Manager, MCP, Docker a Process drivers mají zavedené capability/preflight hranice.
- B07 obsahuje rychlý runtime gate: 422 unit, 33 integration a 1 browser test bez opakovaného buildu.
- Nový lokální Docker stack používá non-root app container, read-only filesystem, interní síť a Docker secret file pro databázové heslo.

## P0 blokery

1. **Persistované process plans:** nový hash zahrnuje host capability data, která starý hash neobsahoval. Loader však používá jen nový hasher. Bez verzování hash algoritmu mohou starší uložené plány skončit na `Persisted plan hash mismatch`; prázdné defaulty capability polí navíc nesmí znamenat implicitní oslabení požadavků.
2. **FileTools provenance:** direct-source režim byl validován proti sibling commitu plus třem necommitnutým souborům. Čistý checkout tedy neumí reprodukovat ověřený graf, zatímco compile symbol označuje implementaci jako validovanou jen podle přítomnosti sibling repozitáře.
3. **Process tree termination:** na Unixu se SIGTERM posílá jen root PID. Pokud root skončí a child proces přežije, současná logika může vrátit úspěch bez odstranění potomka. Watchery, MCP servery a nástroje tak mohou zanechat orphan procesy.

## Hlavní P1 úkoly

- obsloužit server-side MCP `ping` a omezit velikost/počet příchozích JSON-RPC zpráv;
- odmítat neplatné Docker recipe boolean/int hodnoty místo tichého defaultu;
- zpřísnit `logs --since`, port mappings a argument budgets;
- ověřovat skutečnou Unix executable permission aktuálního uživatele;
- přesunout symlink/reparse kontrolu přímo do centrálního workspace guardu;
- opravit budoucí CI containers job tak, aby si vytvořil disposable Docker secret;
- ochránit `-SkipBuild` před použitím zastaralých assembly a přejít na FQN test catalog;
- sjednotit stale inventáře, handoffy, anchory, checksums a odstranit verzovaný `.local` artefakt.

## Testovací politika

- Po každé změně pouze konkrétní test class/FQN a dotčený projekt.
- Build jednou na začátku checkpointu; následné testy `--no-build --no-restore`.
- Po `M01`–`M03` jeden společný runtime gate; full stable Windows pouze jednou a jen pokud změny zasáhly sdílené persistence/process kontrakty.
- Po `M04`–`M06` runtime gate na Windows a Linuxu bez full suite.
- Na finálním kandidátu full stable přesně jednou na Windows a jednou na Linuxu.
- Dokumentační, checksum nebo evidence-only změny full suite neinvalidační.
- macOS testy proběhnou až na jednom neměnném kandidátu u kolegy.
