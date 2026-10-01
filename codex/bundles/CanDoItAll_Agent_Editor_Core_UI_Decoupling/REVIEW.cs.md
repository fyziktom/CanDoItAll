# Revize Projects Files P2 a pokračování Agent Editor A1

## Verdikt

Architekturu Projects P1/P2 bych zachovala. Dřívější opravy rozlišení odmítnuté životnosti a
uvolňování JS importu jsou zapracované. V novém Files controlleru jsem našla jednu ohraničenou
chybu: pozdní chyba staršího náhledu se může promítnout do novějšího náhledu stejného otevření.
Zařazuji ji na začátek dalšího běhu, po ní následuje Agent Editor Core A1. Nejde o důvod znovu
přestavovat Workspace nebo vracet provedenou extrakci.

Základ revize je hlavní `components-decoupling` na
`92a3c373c742537608fded3c483b685291339853`. Components development bylo při revizi znovu ověřené na zveřejněné opravené verzi `4a858412d2c2a3f6123bf23d8c4584f05b47627d`.
SHA jsou původem zjištění, nikoli pokynem resetovat checkout. Přesné soubory a rozsah čtení jsou
v SOURCES.md; vyhledávání defaultní větve jsem použila jen k nalezení cest, ne jako stav této větve.

## Co je správně

P2 přesunulo oba skutečné souborové renderery do samostatné knihovny. Efektové hosty používají
společnou session, nezávisle připravené workspace a původní vlastníky souborových oprávnění.
Projects P1 nepřibyla implementační souborová reference. Read-only náhled používá skutečně předané
složení viewerů, nikoli náhodný textový fallback. To odpovídá cíli této vlny. [R01, R03, R24, R27]

P1 nyní odliší konkrétní ProjectWriteAdmissionRejectedException pro původní seed nebo Delete od
neznámého výsledku. Uvolní pouze vlastní blokaci, starý draft zůstane odmítnutý a potvrzené uložení
se neztratí. JS validace má oddělený pending import a získaný modul, takže zavírání znovu nevyvolává
již zpracovanou chybu importu. Tyto části bych nepřepisovala. [R08, R09]

## P2-R1: stejná aktivace není stejný požadavek

V jedné otevřené souborové ploše začne náhled A. Zatímco čeká, uživatel vybere B. Nová operace
zruší token A, ale ponechá stejné otevření a workspace. B načte svůj obsah. Když A poté skončí
obyčejnou výjimkou, jeho catch testuje jen IsCurrent(origin), ne konkrétní aktivní operaci, a
přepíše ActivationError. Chyba A se tak zobrazí u B; může také nahradit vlastní aktuální chybu B.
Úspěšná cesta naproti tomu token a context ověřuje správně. [R03, R04]

Zkontrolovala jsem i skutečný renderer a FileBrowser dispatcher: při čekajícím náhledu zůstává
seznam dostupný a dispatcher předání host callbacku neserializuje. Existující testy řeší zejména
přechody mezi různými aktivacemi, nikoli toto pořadí uvnitř jedné aktivace. [R05–R07]

Není to prokázaný únik dat, obcházení oprávnění nebo poškození uloženého obsahu. Oprava se týká
vlastnictví výsledku a chyby. Codex ji má nejprve reprodukovat regresním testem, potom doplnit
kontrolu konkrétní operace a zachovat správný úklid, současné chyby i nezávislé druhé otevření.
Nemá kvůli tomu zavést globální frontu nebo přepisovat FileTools autorizaci.

Drobně se má aktualizovat modules.md: stále uvádí Files P2 jako odložené. U titulku Files je vhodné
ověřit skutečné bajty a případný chybný znak; nejde o zadání další vizuální ladicí kampaně. [R06, R10]

## Jak hodnotím testování

Údaje pocházejí ze zprávy implementátora, nikoli z testů spuštěných touto revizí. [R02]

| Oblast | Doložený záznam |
|---|---|
| P1 mutace | 14 úspěšných případů, před opravou čtyři prokazující chybu |
| P1 renderovací vrstva | 18 úspěšných, včetně sedmi interop případů |
| P2 neutrální vrstva | 17 úspěšných; později opakováno po finálních drobnostech |
| Širší vybrané unit testy | 158 úspěšných; už obsahují užší session/action výběr |
| Nativní složený výběr | 85 z 87 prošlo; dva později prošly po opravě očekávání skutečného Markdown vieweru |
| Sousední lehké moduly | Resources 70 a Workspace Core 52 úspěšných případů |
| Složený browser/consumer pokus | 8 z 11 prošlo; šest negativních kontrol prošlo |
| Finální menu/source/publish/production výběr | 3 z 3 prošly po konkrétní opravě CSS/pozorování |

Nesčítám překrývající se běhy do jednoho domnělého počtu. Dřívější smíšené výsledky zůstávají
smíšené; pozdější úspěch je samostatný důkaz. Nový široký Stable se pro tento ohraničený řez
nespouštěl. Považuji za rozumné neopakovat jej automaticky, pokud se skutečně nezměnily společné
vlastnické kontrakty; jeho platnost se ale musí znovu vyhodnotit pro A1 podle reálného diffu.
Historické P1 výsledky nejsou nové P2/A1 ověření. [R01, R02, R26]

Izolace má měřitelný strukturální přínos: zpráva uvádí 8/11 projektů pro Files UI/sandbox a 479
sledovaných cest; P1 zůstalo na 5/6 projektech a 300 cestách. Zaznamenané sandboxové Razor vzorky
jsou 1,33 / 0,83 / 0,83 sekundy. Nejsou to mnou zopakované benchmarky ani obecný příslib rychlosti.

## Další část: Agent Editor Core A1

Navrhuji přesunout formulářový shell a čtyři skutečné sekce: Identity, Runtime, Images, Voice.
Zachová se všech deset položek navigace; šest dalších sekcí a jejich vlastníci zůstane v produkci
připojeno přes explicitní hostované části. Memory, Project Structure Access, Workspace Tools,
Secrets, Process Access a Capabilities se v tomto běhu nebudou vydávat za dokončenou extrakci.
Samostatně spravované generování avataru a synchronizace provideru zůstávají přiznanými hostovanými
integracemi. Sandbox prokazuje skutečné čtyři vybrané sekce, nikoli jejich falešné náhrady. [R11, R12]

Nejdůležitější je ponechat jednu původní session, draft a EditContext. Uložení jádra nesmí sestavit
nového agenta pouze ze čtyř záložek a tím vymazat přístupy, capability IDs, tajné reference, procesy,
Memory, external-root bindings nebo neznámé JSON rozšíření. Přesně proto má nové zadání nativní
round-trip test celého záznamu a kontrolu druhého nezměněného agenta. [R13–R15, R20]

Technicky není správné zakázat všechny MAF typy jen podle jména. Models má deklarované reference
na abstractions a současné AgentFramework.UI jej již používá. Konkrétní bezpečné modely lze znovu
použít po ověření skutečného grafu. Naopak celý MAF Components projekt by kvůli jedinému selectoru
přinesl Core, Voice a Canvas; existuje již neutrální ConversationProviderModelSelector. [R16–R18, R22, R23]

Výstup má mít vlastní lehký Editor.UI a nezávislý sandbox. Existing katalog/Overview, Workspace a
oba Projects sandboxy nesmějí získat nové zpětné reference. Workflow authoring, Workbench a Processes
zůstávají na další samostatné kroky. AgentFramework po A1 bude stále částečně dokončený.

## Obecné funkce a omezení

Předepsané UI testy jsou pouze velký desktop, primárně 1920 × 1080. Nemá se plýtvat časem na mobilní,
tabletové ani small/medium ladění. Zachovají se funkční kontroly focusu, scrollu, celého menu,
čitelného obsahu, skutečné validace a správných dialogových životností.

Produkční test uloží agenta přes nové jádro, skutečně ho spustí nad testovacím projektem a ověří
provider/model a příslušnou souborovou/approval cestu. Skriptovaná je jen externí modelová odpověď;
vlastníci, nástroje, oprávnění, zápisy a read-back musejí být skutečné. Nepovoluje se nový placený
live rozpočet a původních 40/40 se neresetuje.

V prostředí revize není dostupný dotnet. Nesestavovala jsem produkt, nespouštěla jeho C# testy,
PostgreSQL ani browser. Původní soukromé TRX, screenshoty a manifesty nebyly předány. Revize je
zdrojová kontrola plus posouzení uložených výsledků, ne nezávislá certifikace celé aplikace.

Závěrečné ověření obou vzdálených HEAD nezjistilo změnu. Dotaz na GitHub Actions pro kontrolované
SHA vrátil nula běhů; samo o sobě to nevyvrací lokální testování uvedené implementátorem.
