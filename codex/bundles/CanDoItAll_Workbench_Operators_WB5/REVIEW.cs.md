# Revize WB4 a návrh Workbench Operators WB5

## Závěr

WB4 bych zachovala jako dokončenou obsahovou a souborovou rodinu. V prověřených cestách
jsem nenašla nový blokující problém WB4. Celý Workbench ale dokončený není: zbývají
participant/meeting a adresářové integrace, chráněné reference, runtime a návazné
Workflow/Process dialogy. Další WB5 dokončí první tři rodiny, nikoli Processes. [S02]

Kontrolovala jsem main `components-decoupling` na `42cd807dec761daa79fa8b48ff78c2330f615354` a Components `development`
na `a3fd4d22f2c4e0432cf389f194c6468b44ab7371`. Hlavní HEAD je závěrečný dokumentační commit nad produktovými změnami;
revize zahrnula i předchozí implementaci a testy. GitHub potvrzuje podpis hlavního HEAD.
Nové Components jsou skutečně dostupné vzdáleně, takže historická věta reportu
„local-only“ už nepopisuje jejich nynější dostupnost. Commity jsou původ revize,
ne požadovaný checkout pro další práci. [S01, S03, S30]

## Co je správně

Původní textový editor už zachycuje projektovou životnost, rodiče a otevření před
asynchronním ukládáním. Nový renderer používá původní dialogový receiver. Kolekce
souborů i přímý editor drží konkrétní session a původ jednotlivých callbacků. [S05-S08]

Nativní autorizace znovu kontroluje aktuální write/mutable-update capabilities při
vydání i použití oprávnění k zápisu. Dřívější grant proto není sám o sobě povolením
pozdějšího zápisu do již read-only úložiště. [S09]

Generování obrázku odlišuje providerový efekt, uložená média a připojení k původnímu
uzlu. Kontroly původního obsahu a providerové konfigurace zůstávají u nativních
vlastníků. Transkript zachovává neznámá metadata, původní reference a odlišuje
providerové dokončení od uloženého výsledku. [S10-S13]

Content.UI deklaruje jen neutrální Components/FileTools závislosti. Nový obecný
CanvasOverlayDialog je na správném místě v Components. Graf 12 projektů Content
UI a 14 sandboxu je měření uvedené implementátorem, ne moje nové MSBuild měření.
Nemá smysl tuto architekturu nahrazovat novou API vrstvou nebo znovu rozebírat WB1–WB3. [S02, S04]

## Jak hodnotit testy

Zpráva zaznamenává 40/40 finálních nezávislých Content případů, 53/53 nativních
content/image případů, 31/31 collection/editor komponentových případů a 7/7 skutečných
PostgreSQL kontrol revizí a ztráty oprávnění. Výběry se překrývají. Finální protokol
na zdroji a dvou klientech uvádí 19/19. [S02]

Zmrazený Stable provedl **16 819 případů: 16 817 pass, dvě selhání, nula skip**, napříč
31 assemblies. Jedním selháním bylo očekávání odstraněné CSS třídy v testu; následné
kompatibilitní ověření 5/5 zachovalo původní assertions. Druhé odpovídá zachovaným
syntetickým bezpečnostním kontrolám. Původní celý běh se tím zpětně nestal all-green.
Native Integration uvnitř téhož běhu má 3 293 úspěšných případů. [S02]

Providerové fixture obrázky JPEG/WebP byly opravené až po zjištění neplatných starých
bajtů. To není přepsání nativních uložených výsledků ani nový product image. Některé
browserové neúspěchy mají explicitní pokračování přes původní přijaté identity bez
opakování efektů. Usage zůstal nejprve pravdivě částečný; index byl odvozen samostatným
explicitním nástrojem pouze v testovacím scope. Tato rozlišení nový bundle zachovává.

Všechny uvedené produktové výsledky jsou výsledky Codexu. Tato revize nespustila .NET,
PostgreSQL, Docker, browser ani watch; původní privátní TRX, logy a screenshoty nebyly
přiložené. Zdrojová kontrola ani kontrola předávacího ZIPu nejsou release certifikace.

## Nový úvodní nález WB5-P1

Ve starém `CreateParticipantPartyAsync` se po await vytváření adresářového záznamu
znovu používá aktuální `partyEditor` a aktuální `ProjectId`. Dokončení A tak může
zapsat vybranou novou PartyId do mezitím otevřeného B, vyčistit jeho QuickCreate a
v `finally` ukončit jeho busy stav. Vstupní handler navíc nemá samostatnou ochranu
proti přímému opakovanému vyvolání. [S14, S15]

Je to zdrojově odvozený problém v odložené části, nikoli tvrzení o nové regresi WB4
nebo zde pozorovaném incidentu. Samotná chybná volba v draftu ještě neprokazuje
uložené nesprávné přiřazení. Codex jej má nejprve reprodukovat skutečným formulářem
s řízeně čekajícím nativním vlastníkem.

Oprava musí držet původní otevření a neměnný požadavek, přijmout skutečnou PartyId
před následným čtením a nedovolit opakované vytvoření jen kvůli chybě refreshe.
Quick create není Save participant sync. Pouhé přidání dalšího globálního busy
flag nebo zavření všech dialogů není řešení.

## Větší pokračování

WB5 zahrnuje celý participant/meeting formulář, lokální versus adresářové vazby,
quick create, projektové defaults a role; celý chráněný secret picker/create-and-use;
celé runtime quick actions, explicitní potvrzení, readiness a web preview.
Preferuje lehký `CanDoItAll.Workbench.Operators.UI` a samostatný sandbox. Tři rodiny
mají vlastní session, nikoli společný stavový a servisní kontejner. [S14-S24]

Nejsložitější je zachovat vícefázové výsledky. U přiřazení již existuje podmíněná
varianta nahrazení se snapshotem; nesmí se tiše nahradit nepodmíněným zápisem.
U secretu jsou uložení ve vaultu, načtení metadat a projektová reference různé fáze.
U runtime už registr má skutečnou identitu procesu: Close preview není Stop a starý
Stop nesmí zastavit novou session pod stejným node ID. Použije se původní vlastník,
ne druhý runtime registry. [S15, S16, S19-S21]

Starší rizika runtime/secret cest jsou požadavky pro cílené ověření a malou opravu,
ne předem prohlášené nové incidenty. Případná skutečně rozsáhlá změna autority nebo
schématu musí mít samostatnou mapu a zůstat explicitním blokátorem postižené funkce;
ostatní bezpečná práce může pokračovat.

## Testovací a provozní podmínky

Povinné jsou skuteční nativní vlastníci, dva editory, konkurence, původní životnosti,
bezpečné odmítnutí, uložené identity a nezměnění sousedé. Runtime se ověří neškodným
vlastněným procesem a canary. Navazuje agent nad Project Structure, přesná approvals,
souborové bajty, Workflow/Scheduler a History. Jen odpověď externího modelu může být
skriptovaná. Žádné placené volání ani reset starého rozpočtu.

Veškeré nové vizuální testování je **1920 × 1080/DPR1**. Žádná kampaň pro mobil/tablet.
Sandbox musí používat skutečné komponenty a fungovat i po samostatném publish.
Watch měření musí rozlišit in-place změnu, navigaci a restart. Větší Stable se volí
podle skutečného finálního zásahu, nikoli po každém formuláři. [S25-S29]

Codex má včas požádat o nativní PGP odemčení a dělat podepsané commity po větších
etapách; ověřit podpisy a zahrnout nepozměněný vstupní bundle do historie. Zachová
stejné signing prostředí, ale neslibuje neomezenou platnost cache. Bez push/merge a
bez mazání starých bundles. [F02]

Po WB5 ještě zbývá integrace Workflow/Process linkage/start/review/recovery a závěrečný
soupis Workbenche. Až potom samostatný velký Processes modul. Náročnost není možné
vyjadřovat pouhým počtem zbývajících Razor souborů nebo `.UI` projektů.

Podrobné instrukce jsou v angličtině v `prompt.md`; zdrojový registr je v `SOURCES.md`.
