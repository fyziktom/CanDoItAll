# Ověření vývojové instance 5032 — 7. října 2026

Tento záznam patří běžné vývojové instanci a databázi `candoitall_development`.
Samostatný zmrazený [kandidát WB6](architecture/workbench-wb6-validation.md), jeho
historické výsledky, obraz, zálohy a doručovací skripty zůstávají zachované.
Nové volání nakonfigurovaného OpenAI povolil uživatel pro ověření obrázků a Procesů.
Nejde o deterministické odpovědi testovacího poskytovatele.

Opravy CanDoItAll jsou v podepsaném commitu
`166f747afb3d5f98fbe552e03e81cedce2a480d8`; `git verify-commit` potvrdil podpis.
[Manifest zdrojů a důkazů](demo-5032-evidence.json) zahrnuje také hashe opraveného
vzorku Tetris3, který leží mimo Git checkout. Nativní obrázek a 53 souborů obou
nových Procesů jsou navíc zachovány v soukromém archivu `native-proof-preserved.zip`.
Databáze zůstává ve stávajícím trvalém PostgreSQL svazku; nová databázová záloha
není součástí tohoto navazujícího záznamu.

## Ruční kontrola

Otevřete [Tetris3](http://localhost:5032/projects/99d218dc-701a-4fac-9305-2e040f1fb3a7/structure).
HTTP adresa se standardně přesměruje na HTTPS port 7271. Použijte velký desktop;
ověření probíhá v Chromium při 1920 × 1080, DPR 1.

- V Object index vyberte `5032 Tetris image smoke 2026-10-07`. V Inspector otevřete
  `Expand preview`. Uložený PNG má 1024 × 1024 pixelů a 818 295 bajtů.
- Otevřete [dokončený jednoduchý Process](http://localhost:5032/projects/99d218dc-701a-4fac-9305-2e040f1fb3a7/processes/live?runId=61fc069e-65c5-4fb9-9c1c-4bc45c092dab).
  `Project structure note writeback` dokončil oba kroky a zapsal výstupy do projektu.
  Pro starší běh vyberte období `Last day`.
- [Nový běh Blazor app delivery](http://localhost:5032/projects/99d218dc-701a-4fac-9305-2e040f1fb3a7/processes/live?runId=cc75386b-e027-4661-8150-dfa6486fe2f7)
  patří úkolu `5032 Tetris3 desktop regression rehearsal — 2026-10-07`.
  Byl řízeně zrušen při přiblížení k limitu modelových volání. Architektonický krok
  skončil; vývojový krok sestavil aplikaci, opravil zobrazení nejlepšího skóre,
  úspěšně spustil tři testy a pořídil prohlížečové důkazy. Nezávislý QA krok ani
  konečné předání tohoto Procesu neproběhly. Stav `Cancelled` není úspěšné dokončení.
- V Live Processes ponechte `Last hour` a otevřete podrobnosti. Stránka nyní každých
  deset sekund načítá čerstvou projekci a aktualizuje také otevřené podrobnosti.
  Historická období používají ruční `Refresh`. Zavření detailu běh neruší.
  Opětovné `Start reviewed run` není náhradou za obnovu zobrazení.
- V Object index vyberte `Run Tetris3 client` a použijte `Run`, případně otevřete
  již spuštěný [vzorek na portu 5001](https://localhost:5001/). Klikněte do hracího
  pole a zkuste šipku nahoru. Rotace mění tvar, nepřidává body a neskrývá buňky.
  Nejlepší skóre odpovídá uložené hodnotě a přežije obnovení stránky.

## Spuštění a restart

Používejte již registrovaný sdílený DotNetWatch backend pro tento checkout.
`candoitall_workspace_info` ukáže vlastníka a aktivní relaci. Před restartem ověřte,
že na 5032 neběží důležitý Process nebo Agent. Potom zastavte pouze odpovídající
relaci přes `candoitall_app_stop` a vyčkejte na stav `Stopped`.

Pro `candoitall_app_start` použijte:

| Parametr | Hodnota |
|---|---|
| `logicalAppId` | `candoitall-development-5032` |
| `projectPath` | `<checkout>/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj` |
| `mode` | `WatchRun` |
| `launchProfile` | `https` |
| `configurationName` | `Debug` |
| `urls` | `http://localhost:5032`, `https://localhost:7271` |
| `reuseIfCompatible` | `true` |
| `conflictPolicy` | `Fail` |

Explicitní URL brání přidělení jiného vývojového portu. Zachovejte existující profil,
nastavení poskytovatelů a lokální PostgreSQL 18 kontejner
`candoitall-postgres18-5032`. Nevypisujte připojovací řetězce ani klíče.
Po časovém limitu požadavku nejprve zkontrolujte relace; start mohl pokračovat.
Nevytvářejte druhou instanci naslepo.

Vyčkejte na `Healthy`, ověřte `http://localhost:5032/health` a
`http://localhost:5032/_dev/runtime`, potom obnovte původní záložku. Úspěch health
kontroly sám nepotvrzuje funkční hot reload. Tento záznam ověřuje úplné sestavení
a řízený restart. Režim `RunOnce` při prvním pokusu narazil na délku cesty kopírované
šablony v izolovaném výstupu Windows; běžný `WatchRun` se sestavil a spustil.

## Důkazy a ochrana dat

Soukromé důkazy jsou v `artifacts/manual-5032-repair-20261007/`. Původní neúspěšné
pokusy o testy zůstaly uložené; konečný `live-refresh-04.trx` obsahuje sedm průchodů,
nula selhání a nula přeskočených testů. Chyby těchto mezikroků byly v synchronizaci
asynchronního testovacího hostu a v neúplném nastavení textu projekce ve fixture.

Před spuštěním Blazor Procesu byl vytvořen archiv původních 15 zdrojových souborů
Tetris3 a manifest SHA-256. Pracovní úkol zakazuje přepsání aplikace novou šablonou,
změny CanDoItAll či oprávnění a omezuje ověření na velký desktop. Proces použil
skutečné nástroje sestavení, testování a prohlížeče.

Samostatná kontrola Codexem reprodukovala nefunkční rotaci: klávesa ArrowUp přidala
deset bodů, ale souřadnice buněk tvaru se nezměnily. Po archivaci stavu zanechaného
Agentem opravil Codex `GameState.cs` v uloženém Tetris3 a přidal tři regresní testy.
Rotace nyní mění geometrii bez přidávání skóre; pohyb, rotace a dolní okraj respektují
skutečnou šířku a výšku tvaru. Samostatný build i všech šest testů prošly. Tyto důkazy
jsou označeny `tetris-codex-*` a nejsou výsledkem dokončeného modelového Procesu.
Archiv po Agentovi i finální archiv uchovávají rozdíl oproti původnímu vzorku.
Závěrečná vizuální kontrola také opravila roztažený rámeček hracího pole a barvení
celého náhledu místo jednotlivých buněk. Po nativním Stop/Run má pole 264 × 524 px,
náhled čtyři barevné buňky na průhledném pozadí, žádný vodorovný přetok a žádnou
chybu konzole. Ověření zůstalo výhradně na velkém desktopu.

CanDoItAll: přímý build modulu Processes i Workbench.Execution.UI prošel bez chyb
a varování. Sedm regresí živého přehledu a sedm testů rendererů/hranic prošlo.
No-write portability enforcement potvrzuje 15 306 nezměněných schválených nálezů.
Neproběhl nový široký Stable běh: opravy nezasahují sdílené sestavování, runtime,
databázové schéma, testovací bootstrap ani závislosti původního checkpointu.

`genuine-model-ledger.json` odděluje nové skutečné modelové pokusy od historických
rozpočtů WB6. Známá cena dvou kroků jednoduchého Procesu je USD 0,183907. Cena
samostatného souhrnu ani obrázku není tímto číslem pokryta. Nulová průběžná metrika
aktivního kroku není dokladem bezplatného volání. Dokončené kroky vracejí souhrn usage;
zrušený vývojový krok zachoval pouze částečná data. Jeho cenu ani počet odchozích
volání proto nelze vykázat jako úplné. Konzervativní sledování počtu nástrojových
interakcí vyvolalo zrušení běhu; rozšíření limitu vyžaduje odpověď na položený dotaz.
Žádný starší neúspěšný či blokovaný výsledek se tímto nepřepisuje.
