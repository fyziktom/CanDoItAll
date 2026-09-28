# Sdílený základ pro UI decoupling · shrnutí revize

**Balíček:** `CDA-UI-DECOUPLING-SHARED-v3`, 28. září 2026.

Výsledkem je jeden společný základ pro další modulové úkoly, nikoli sloučení tří historických postupů za sebe. Technické instrukce, šablony a audit jsou anglicky. Tento dokument slouží jen jako český přehled pro zadavatele.

## Architektonický závěr

Pokračovat v postupném oddělování rendererů od backendu a zachovat modulární aplikaci. Produkční host může dál volat vlastní aplikační služby v procesu. Není nutné zavádět HTTP mezi každé UI a backend; existující HTTP control plane u Processes / Project Structure se ovšem nesmí obejít. Cílem je skutečně lehčí závislostní graf pro danou UI práci, ne jen přesun `.razor` do jiné složky.

V repozitáři už existuje kanonický `docs/architecture/ui-component-seams.md`. Nový bundle na něj navazuje a nevyhlašuje druhou konkurenční autoritu. Užitečná pravidla z Foundation, UI Seams a původní integrace jsou sjednocena; jejich jednorázové merge kroky, staré pinování, požadavek nejprve znovu dokončit tehdejší Agents checkpoint a historická testovací evidence nejsou novými úkoly.

## Co bylo proti starému základu upřesněno

Zachovány jsou oba současné modely rozhraní: prezentační data s typovanými událostmi i workspace view kontrakt implementovaný hostem. `EditContext` nemusí fyzicky ležet v hostu; musí mít správnou životnost vůči draftu. Nevyžaduje se univerzální controller ani určitý počet rozhraní/projektů.

Mapa zahrnuje současný CRM/HR renderer, sedm jeho pracovních ploch, RecordBrowsing i UI knihovny Workflows a Simple Chats mimo `src/UI`. Samotná existence contracts projektu není označena za hotový decoupling celého modulu; samotný backendový projekt naopak nepotřebuje umělý UI sandbox.

Zvlášť jsou posuzovány build graf, runtime závislosti, skutečný strom rendererů a asset/watch graf. To řeší i současný rozdíl mezi Parity a Fast. Obsahový odkaz na produkční CSS není totéž jako závislost sandboxu na projektu Web. Zrychlení se musí měřit ve srovnatelných podmínkách; není odvozováno z počtu přesunutých souborů.

Testovací pravidla odpovídají současnému rozdělení testovacích solutions, izolovanému PostgreSQL 18 a povinnému portability-static gate. Velká suite se nepouští po každém malém kroku, ale není ani nahrazena samotným sandboxem. Konkrétní modul musí ověřit potřebné reálné produkční cesty.

## Dvě konkrétní poznámky ke kódu

`PromptGallerySearchSession` v kontrolované verzi ruší nahrazované cancellation token sources, ale neuvolňuje je. Jde o konkrétní dluh v životnosti zdrojů, nikoli zde naměřený únik nebo provedenou opravu.

`CrmHrUiBoundary` při průchodu referencemi ignoruje chybějící assembly. Takový průchod není úplný důkaz transitivní izolace. Nové instrukce proto vyžadují i vyhodnocený build graf a poctivé zacházení s nevyřešenými referencemi. Zjištění a přesné zdroje jsou v [auditu](audit/REVIEW.md).

## Použití

K příštímu modulovému úkolu přilož tento bundle a konkrétní rozsah podle [šablony](templates/module-slice.md); vstupem pro Codex je [prompt.md](prompt.md). Další úkol pracuje na skutečné aktuální větvi, ne na pevně předepsaném historickém commitu. Nástroj `tools/check_review_drift.py` může lokálně ukázat změny proti kontrolovaným souborům, aniž mění Git nebo spouští aplikaci.

Revize vycházela z `development` na `7db3543ab437376baeca55089cb331fbe1b30483`; větev byla na konci čtení znovu ověřena. Audit je založen na kódu, projektových souborech, sandbox startupu a testovacích/CI pravidlech načtených přes GitHub. Není to úplný audit všech metod aplikace. V tomto prostředí nebyla sestavena aplikace, spuštěny její testy ani změřen dotnet watch. Ověření integrity samotného balíčku je vedeno zvlášť v [záznamu validace](audit/package-validation.md).
