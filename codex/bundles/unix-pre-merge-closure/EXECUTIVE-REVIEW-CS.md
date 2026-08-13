# Výkonná revize

## Aktuální stav

Větev `unix-adoption` na commitu `af9206caf3c09dc25088e388727fda0e1b404833` už obsahuje podstatnou část
správně navržené portability vrstvy:

- explicitní logické a fyzické cesty;
- platformní storage, secrets a headless profily;
- jeden kanonický process host;
- Windows Job Object a Unix process-group ownership;
- Workbench a Manager lifecycle registry;
- fail-closed MCP a Docker adaptéry;
- process host-capability sealing;
- reprodukovatelnější package/source dependency režim;
- Docker app + PostgreSQL stack;
- MAF 1.17 a opravy approval continuation;
- agent authority provider potřebný pro běžné agentní chaty.

Manuální Docker sonda uživatele navíc prošla přes vytvoření agenta, čtení
Project Structure a vytvoření asset nodes. Je to hodnotný integrační signál,
ale nepokrývá staré persistované záznamy ani vzácné chyby při připojování
procesu k OS ownership boundary.

## Co již není důvod blokovat

- macOS actual-host build a runtime testy;
- Keychain actual-session validace;
- Azure Key Vault a HashiCorp Vault;
- finální enterprise secret governance;
- NuGet publikace opraveného FileTools, pokud package mode zůstává pravdivě
  omezený;
- úplné opakování všech testů při každé změně.

## Tři skutečné blokery

### F-001 — časová klasifikace legacy process plans

Aktuální mapper a PostgreSQL migrace používají datum
`2026-08-11T18:53:52Z` jako část rozhodnutí, zda je plán legacy. Starý plán
vytvořený po tomto okamžiku, ale ještě starým kódem z `development`, může
skončit jako `Unknown` nebo vyvolat chybu „missing bounded hash algorithm
version“.

Správnou autoritou je struktura persistovaného JSON payloadu, ne hodiny.

### F-002 — únik procesu při selhání attachmentu

`Process.Start` může uspět a `ownershipStart.Attach(process)` následně
selhat. Tato výjimka je zabalena jako start failure ještě před vstupem do
pozdější cleanup větve. Na Windows může zůstat proces mimo Job Object; na
Unixu může zůstat bootstrap proces zastavený nebo běžící.

Start musí být atomický z pohledu vlastnictví: buď se vrátí plně vlastněná
session, nebo po něm nezůstane žádný proces.

### F-003 — schema-1 Manager registry bez Boundary

`WorkspaceOwnedProcessIdentity` nyní vyžaduje `Boundary`, ale Manager registry
stále deklaruje schema 1 a validace boundary neověřuje. Starý JSON se může
deserializovat s chybějící boundary, projít první validací a selhat až při
recovery.

Legacy záznam bez boundary nesmí autorizovat ukončení procesu. Musí být
bezpečně převeden do `OwnershipUnverified` s jasnou diagnostikou.

## Doporučená merge hranice

Po uzavření F-001 až F-003 a po cíleném exact-head gate je vhodné větev
sloučit do `development`. Další odkládání merge kvůli macOS nebo enterprise
vaultům by už nebylo přiměřené a zbytečně by blokovalo navazující Simple LLM
Chats a další aktivní vývoj.
