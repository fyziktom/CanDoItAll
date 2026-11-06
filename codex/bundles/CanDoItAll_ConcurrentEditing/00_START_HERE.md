# CanDoItAll — bezpečné souběžné editace a srozumitelný merge

**Architektonické zadání pro Codex GPT 6.1 Sol max • 30. října 2026**

## Výsledek, který má vzniknout

Hlavní Blazor UI a jeho engine mají zabránit přepsání cizích změn zastaralým formulářem. Uživatel má vidět změnu, zachovat rozpracovaný obsah a vědomě převzít nebo sloučit novou verzi. Seznamy mohou průběžně aktualizovat položky bez rozbití kontextu; grafové editory mají explicitní pull podobný Project Structure. Všechny zápisy musí být bezpečné i bez doručené notifikace.

Nejde o nový workflow bundle, generickou náhradu všech modulů, Google Docs spolupráci ani zásadní přestavbu aplikace. Použij stávající refaktorované hosty, vlastníky a renderery. Způsob implementace, názvy typů a rozdělení jednotlivých commitů můžeš upravit, pokud doložíš ekvivalentní bezpečnost, lehký dependency graph a testy. Neměň následující závazné výsledky a schvalovací hranice.

## Nejdůležitější hranice

**Implementuj společný základ a pilot Prompt Gallery. Poté proveď cílené testy, rebuildni správnou existující instanci na portu 5032, ověř skutečně načtené nové binárky a assets a ZASTAV SE. Další moduly smíš upravovat až po výslovném schválení pilotu uživatelem.**

Stav předání: `AWAITING_USER_APPROVAL_PROMPT_GALLERY`. Úspěšné testy, mlčení uživatele, požadavek na opravu pilotu ani tvé vlastní hodnocení nejsou souhlas s rolloutem. Při zpětné vazbě oprav pilot a zastav se znovu. Globální oprava sdílené vrstvy notifikací je součástí pilotu; převod CRM a dalších editorů nikoli.

## Doporučené pořadí čtení

1. [Nálezy a předřazené změny](01_AUDIT_FINDINGS.md), [architektura](architecture/01_BOUNDARIES.md), [protokol merge](architecture/02_MERGE_PROTOCOL.md).
2. [Pilot a konkrétní soubory](pilot/01_IMPLEMENTATION.md), [manuální testy](pilot/02_MANUAL_ACCEPTANCE.md), [5032 a schválení](operations/01_EXECUTION_AND_5032.md).
3. [Odběry a seznamy](architecture/03_CHANGE_OBSERVATION.md), [okna a notifikace](architecture/04_OVERLAYS.md), [výkon](architecture/06_PERFORMANCE.md), [oprávnění a Undo](architecture/05_AUTHORITY_AND_HISTORY.md).
4. [Inventář ploch](inventory/01_SURFACES.md), [zápisový obvod](inventory/02_WRITE_PERIMETER.md), [plán po schválení](operations/02_ROLLOUT.md).
5. [Testy](tests/01_TEST_STRATEGY.md), [úklid testů](tests/02_TEST_CLEANUP.md), [zdroje](evidence/README.md), [kontrola požadavků](review/01_REQUIREMENTS.md).

Pomocné read-only nástroje a jejich omezení jsou popsány v [scripts/README.md](scripts/README.md).

## Neměnné invarianty

- Ochranu zajišťuje **serverový podmíněný zápis proti původnímu pozorování**, ne toast, busy flag, lease nebo načtení nejnovější verze těsně před zápisem.
- Zpráva o změně je pouze hint. Ztracený hint nesmí umožnit lost update.
- Třícestné porovnání používá původní **B**, místní **L** a souvislou současnou **R**. Sloučení vytváří návrh v lokálním draftu; samo nic neukládá.
- Potvrzení vlastního uložení používá jinou trojici: odeslané **S**, nynější draft **L**, přijaté **A**. Nesmí zničit úpravy vzniklé během await.
- `Unknown` výsledek není konflikt ani neúspěšný zápis. Zachovej současné recovery protokoly a nezaváděj slepé retry.
- Neztratit raw nevalidní vstupy, EditContext, vložené řádky, fokus ani rozpracovaný obsah při konfliktu, neúspěšném čtení nebo pokusu zavřít modal.
- UI knihovny nesmějí získat EF, konkrétní moduly, jejich služby ani těžký shell jen kvůli merge. Žádná duplicita existujících DTO a obecná reflexe nad celým doménovým modelem.
- Feed musí být cílený a omezený. Žádné plošné polling smyčky, global-broadcast-per-keystroke, kompletní JSON diff při renderu ani sken celého katalogu pro každý toast.
- Vlastnictví dat není totéž co projektová lifecycle admission. Zachovej autoritu na serveru a nepovažuj dodaný user/session ID za oprávnění.

## Co je závazné a co je doporučení

Závazné jsou chování, ochrany, rozsah pilotu, testovatelnost, zachování výkonu a stop-gate. Navržená jména projektů a API jsou kandidáti, nikoli požadavek na mechanické vytvoření všech typů. Potřebný menší seam dolož konkrétním problémem. Neodkládej však skutečnou mezeru za obecný TODO.

Před každou implementační vlnou uzavři její inventář volajících přes CodeAnalytics MCP a lokální zdroje. Podklady rozlišují přímou inspekci od historických vodítek; nezaměňuj jejich stav. Neexistuje zde tvrzení, že aktuální test suite nebo celá codebase už byly v tomto úkolu ověřeny spuštěním aplikace a testů.
