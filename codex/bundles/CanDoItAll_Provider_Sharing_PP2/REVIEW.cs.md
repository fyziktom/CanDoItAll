# Revize PP1 a navazující Provider Sharing PP2

## Závěr a kontrolovaný původ

PP1 bych architektonicky zachovala. Samostatná renderovací knihovna, skutečné čtyři konfigurační
sekce a sandbox odpovídají směru refaktoringu. Neuzavírala bych ale správnost sdílení providerů
jen na základě lokálního PP1 ověření. Následující běh má nejprve provést síťově oddělené testy
sdílející instance a dvou klientů, opravit reprodukované problémy, teprve potom oddělit Sharing,
source connections a source-refresh renderery. Request History zůstává funkční a regresně
ověřované, jeho extrakce je další samostatná část.

Kontrolovaný commit hlavního repozitáře: `00c395ba0f4ce62c611c62718d8a510f9044f7b4`, větev
`components-decoupling`, zpráva `providers refactor`. Jeho rodič je
`46d745ad1a9803f3945523f656b4d1dd26044b85`; historické PP1 zadání je od produktových změn
rozlišené. SHA je původem revize, nikoli pokynem přepnout checkout. Historické bundles se
během práce zachovávají. Components z předchozího ověřeného páru je
`4a858412d2c2a3f6123bf23d8c4584f05b47627d`; skutečně použitý pár je nutné zaznamenat znovu.

Zdroje a přesný rozsah čtení jsou v [registru](SOURCES.md). Tato revize neprovedla produktový
build, C# testy, Docker, browser ani měření watch. V prostředí revize není dotnet/Docker.
Původní privátní TRX, logy a screenshoty implementátora nebyly předané.

## Co je dobré

R01 popisuje skutečnou leaf knihovnu a vyhodnocené grafy; R02/R03 potvrzují oddělení konkrétních
hostových efektů od rendereru. Reference secrets jsou projekce metadat, nikoli tajné hodnoty.
R06/R07 zavádějí průběžný vstup, revize polí a sloučení potvrzených hodnot vůči odeslanému stavu.
Aktuální hlavní default/modelová pole importu používají GetModelDisplayName, ne raw route ID.
Tyto části nejsou důvodem pro nový backend, DTO kopii všech objektů nebo plošný přechod na API.

## Dva konkrétní následné nálezy

### S0-R1: tooltip má stále interní model ID

R04 interpoluje do BuildTreeTooltip přímo `provider.DefaultModel`. R05 tento text předává do
skutečného stromu. U importovaného provideru může být default `sp1...`, zatímco operátor má vidět
modelové jméno zdroje. Oprava patří do zobrazení a jeho skutečného komponentového testu.
Není to důvod přepsat interní ID, uložený výběr či request na jméno. Jde o nejednotnou prezentaci,
ne o prokázaný únik tajných dat.

### S0-R2: obyčejný Refresh může smíchat dvě revize importu

R24 načte nový katalog a poté se pokusí SelectAsync stejného ID. Nový no-op pro úspěšně načtený
editor platí také pro read-only import. R03 pak čte starý draft default/modelů, ale mapuje jejich
názvy přes nový SelectedProvider. Kritická reprodukce je: druhý circuit stejného klienta přijme
nový source snapshot, původní editor zůstane otevřený a použije pouze Refresh. Nově přidaný
model se nemusí objevit a starý default se může zobrazit proti jiné mapě jmen.

Obvyklá lokální delivery může mít vlastní forced re-acquisition; proto netvrdím, že každý Sync
selhává. Zadání vyžaduje právě odděleného vlastníka/circuit a plain refresh. Oprava musí přijmout
koherentní remote-owned snapshot, ale zachovat dirty lokální editor a samostatné rozepsané
lokální alias/enabled nastavení importu. Ani jeden nález nebyl touto revizí runtime reprodukován;
nejprve jsou zadané regresní testy. Z těchto cest nevyplývá prokázané chybné směrování inference.

## Jména versus skutečné směrování

R19 stanovuje opaque routing ID; R20 publikuje původní upstream jméno jako DisplayName a R21
materializuje import. Zobrazení musí být stejné jako na zdroji, ale request klienta musí nést
původní route ID. Zdroj z něj vybere přesnou publikaci a upstream model. Dva provideři mohou mít
stejně pojmenovaný model a přesto nesmějí skončit na stejném cíli.

Porovná se úplný způsobilý katalog a zvlášť selector, který může záměrně ukazovat pouze suggested,
default a uloženou povolenou hodnotu. R20 a R23 obsahují zvláštní pravidla OpenAI. Klient nemá
aplikovat lokální OpenAI heuristiku na opaque ID ani přidávat své defaultní modely do importu.
Názvy a default se odvozují z přijaté konfigurace tohoto buildu, nikoli z pevného seznamu v promptu.

## Testovací sestava

Existující runner R10 a Compose R11 již obsahují central, client-a, client-b, oddělené databáze,
control-plane/vault kořeny a deterministické upstreamy. Je potřeba jej bezpečně použít a případně
parametrizovat: runner obsahuje pevné výchozí názvy a cesty, takže samotná environment proměnná
nemusí zajistit izolaci. Žádný reset starých manuálních fixture ani použití běžné aplikace 5032.

R08/R09 jsou cenné browserové testy, ale příprava přepisuje source modely na syntetické `e2e-*`
seznamy. To není důkaz běžných modelů defaultního OpenAI driveru. Nová samostatná lane nejprve
zachytí nedotčený nativní katalog a otestuje jeho sdílení. Runtime-only seed může být právem
nezpůsobilý k publikaci; musí se ověřit odmítnutí a poté podporovaná cesta persisted profilu,
ne obejít nativní eligibility.

Kampaň zahrnuje oba klienty, default i jiné modely, prices null/zero, Thinking, Agent Editor,
Simple Chat, Workflow, změnu seznamu a defaultu, druhý circuit, opakovaný import/304, restart,
výpadek, revoke/rotate credential, unpublish/reappearance a identity mismatch. Osobní provider
s kolidujícím modelem nesmí být použit jako fallback. Skutečné nástroje, HTTP, autorizace,
uložení a obsah souboru jsou produkční; nahrazená smí být jen externí odpověď upstreamu.

Nový požadavek autorizuje vytvoření dočasných testovacích credentials a publikací v izolovaném
prostředí. To je odlišné od externí publikace nebo použití reálné OpenAI credential. Původní
placený budget 40/40 se neresetuje.

## Co říkají dosavadní výsledky

R01 uvádí 751 různých vybraných případů: 361 unit, 111 components, 30 leaf, 196 integration,
30 A2 preservation, 18 sousedních boundary a 5 browser. Jde o tvrzení implementační zprávy,
ne mnou zopakované běhy. Externí dvě instance jsou výslovně BLOCKED; tento rozdíl je zásadní.
Historický A2 Stable zůstává smíšený s oddělenou opravou a PP1 jej znovu nevykazovalo jako zelený.

Zvlášť je otevřená vývojová zkušenost: opakovaný Razor hot reload sandboxu přestal ukazovat změny
na druhém vzorku. Úspěšná finální měření použila rebuild/restart bez hot reloadu. Nové zadání
obsahuje omezenou reprodukci na čistém zdrojovém páru; běžící stránka po restartu není důkaz
fungujícího hot reloadu. Neplánuje plošný upgrade SDK ani neomezené ladění cizího tooling problému.

## Další extrakce PP2

Vyčlení se skutečné Sharing renderery, local publish/unpublish, imported local settings/retire,
source list/editor, test/discovery/import/synchronizace, potvrzení a source refresh. Přednost má
samostatné SharedProviders.UI a sandbox, které produkce připojí přes slot; PP1 a Agent Editor
nezískají závislost na nové implementační vrstvě. Původní Recovery, native service, identita,
concurrency, síťová omezení a credential resolution zůstávají u vlastníků.

Request History, další provozní dialogy, capabilities authoring, týmy, Workflow authoring,
Workbench a Processes se v tomto běhu neextrahují. History ale musí dál fungovat a být testované.
S0 se provede před přesuny a nejdůležitější multi-instance scénáře znovu nad finálním image po nich.
Jeden rozumný závěrečný společný checkpoint nenahrazuje cílené testy během etap a neopakuje se
bezdůvodně po každé záložce. Nové vizuální testy pouze large desktop, hlavně 1920×1080.

## Commity a podpis

Codex má požádat o nativní odemčení existujícího PGP klíče hned při zahájení. Zachová stejného
uživatele, GnuPG home/agent a persistentní PowerShell prostředí. Heslo ani klíč nepatří do chatu,
logů, environmentu nebo testovacích kontejnerů. Zachování session neobejde maximální TTL cache;
případná dočasná omezená úprava vyžaduje souhlas, jinak znovu pinentry při expiraci.

Po větších ověřených celcích vytvoří podepsané lokální commity, přibližně S0, Sharing, Sources
s refresh a závěrečné ověření/dokumentace. Související části lze spojit. Všechny nové commity musí
projít git verify-commit. Žádný unsigned fallback, push, merge ani přepis uživatelské historie.

## Výsledek tohoto předání

Balíček stanovuje požadovanou implementaci a důkazy. Neobsahuje předvyplněné úspěchy produktu.
Ověření textů, lokálních odkazů, hashů a pomocných nástrojů je uvedené odděleně v
[PACKAGE_VALIDATION.md](PACKAGE_VALIDATION.md); není to build nebo test aplikace.
