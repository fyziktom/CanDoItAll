# Bezpečný vývoj, instance 5032 a povinné předání

## Pracovní prostředí

Zkontroluj aktuální AGENTS, copilot instructions, SharedInfo a vývojový runtime runbook. Zaznamenej branch/HEAD a předexistující změny; neprováděj reset, automatický checkout jiné větve, clear pracovních artefaktů, reinstalaci celé infrastruktury nebo Docker prune. Použij skutečnou source sibling konfiguraci Components/FileTools. Sdílené komponenty uprav v jejich repo, ne kopií v hlavní aplikaci.

CodeAnalytics MCP poskytne change impact/test reference mapu. Dotnetwatch/manager MCP, pokud je dostupné, použij pro vlastněný dev host. Nedostupnost MCP není důkaz hotové kontroly; použij repo-dokumentovaný lokální CLI/process inspect a zapiš omezení. Názvy tool akcí zde nejsou vymyšlené; discover skutečně dostupné schema.

## Test versus uživatelská instance

Automatizované DB fixtures používají izolovaný PostgreSQL runtime podle docs/testing, ne datastore hlavního 5032. Nezapisovat testovací connection string do production appsettings ani do reportů. Reuse existující izolovanou test DB infrastrukturu podle repo policy; nepřipravovat zbytečně nový cluster pro každý test. Cleanup pouze task-owned resources a až po prokázání vlastnictví.

User 5032 je cílová instance ručního přijetí. Její současné konfigurační soubory, secrets, data, API access mode, MCP nastavení, ports a sibling reference se zachovají. Žádný kopírovaný „čistý“ appsettings, který by ji sice rozběhl, ale nad jinými daty.

## Rebuild a restart bez falešného zeleného výsledku

1. Identifikuj proces poslouchající na 5032, jeho command line, working directory, parent manager/watch a aktuální runtime diagnostics. Read-only diagnostika může použít `/_dev/runtime`, `/_dev/database/selection`, `/health` podle aktuálního dev zabezpečení; výstupy s citlivými údaji nereportuj veřejně. S04, S41.
2. Ověř, že jde o správný hlavní host a máš k restartu jeho oprávněný vývojový způsob. Nezabij jiný proces jen proto, že má otevřený dotnet port. Nespouštěj druhou kopii na jiném portu a neprezentuj ji jako 5032.
3. Obnov potřebné změněné project/assets dependencies a build affected production roots, nakonec skutečný `src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`, standardní configuration podle běžícího hosta. Buildy se společnými outputs serializovat. Tailwind/static assets a sibling build patří do skutečného source dependency procesu, ne do náhodné ruční kopie DLL.
4. Přes stávající manager/watch kontrolovaně restartuj vlastněnou instanci. U změny reference/schema/assets nespoléhej na to, že hot reload určitě stačil. Preserve config a databázovou selection; potřebná owner migration se provádí jen podle výslovného runbooku, ne destruktivním resetem.
5. Ověř health/readiness, skutečný bind 5032, novou assembly/build identity (runtime diagnostika nebo log evidence) a načtení nových static assets. Reload browseru nesmí zůstat na stale cached JS/CSS. Otevři reálné `/prompt-gallery`, native modal, merge a toast. Zkontroluj console/asset errors.
6. Host ponech běžící pro uživatele. Nesnižuj zabezpečení a nevymaž testovací obsah vytvořený pro jeho kontrolu dříve, než jej uvidí. Automatické fault testy nebyly proti této databázi.

Pokud restart blokuje oprávnění, obsazený jiný proces či rozbité prostředí, přesně ohlásit `BLOCKED_FOR_MANUAL_VALIDATION` a důkaz. Není dovoleno označit gate za splněný nebo pokračovat ostatními moduly „než si to uživatel vyřeší“.

## Předávací zpráva Codexu

Krátce uvést: implementované pilot IDs; skutečné hlavní/sibling SHA nebo jasné dirty-diff identifikace; jaké tests byly discovered/executed a výsledek; real owner/browser proof; výkonová baseline/delta; ověřený host/port/assets; známá omezení; odkaz na M01–M16 a připravený testovací záznam.

Pak explicitně **`AWAITING_USER_APPROVAL_PROMPT_GALLERY`** a konec implementace. Žádný automatický sleep/watch, plánovaná background adopce modulů, implicitní souhlas ani další commit CRM.

Uživatelova připomínka spustí pouze pilot correction loop. Schválení musí jasně dovolit pokračování mimo Prompt Gallery. Stav a obsah schválení zapiš do běžného task progress dokumentu v repu; netřeba nový workflow engine ani umělé role/stage manifesty.

## Minimalní dlouhodobá orientace pro schopný model

Udržuj jeden stručný progress soubor: current HEADs, dokončené scope IDs, ověřené test filters a evidence paths, otevřené konkrétní problémy, stav manuálního gate a další schválený scope. Zásadní odchylku zdůvodni. Nepřepisuj tento vstupní balík při každém kroku a nepřidávej závislost na starém shared workflow bundle.
