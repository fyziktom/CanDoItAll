# Revize PP2 a pokračování: Request History PP3

## Závěr

PP2 je tentokrát dokončené i strukturálně: skutečné Sharing, Sources a refresh komponenty mají
lehkou knihovnu a nezávislý sandbox. V prověřených implementačních cestách jsem nenašla další
blokující chybu, kvůli níž by bylo nutné opakovat celé PP2. Je vhodné pokračovat historií požadavků.
Na začátek nového bundle patří ohraničená oprava Tooltip životnosti a aktualizace mapy modulů.
Nejde o potvrzení celé aplikace jako release-ready. [S01–S06,S32]

Kontrolovala jsem hlavní větev `components-decoupling`, HEAD
`643a295e112ca29835323501907bf1d8920a5945`, a Components `development`
`4a858412d2c2a3f6123bf23d8c4584f05b47627d`. HEAD má podle GitHub platný podpis.
Commity určují původ revize, nikoli povinný checkout pro další běh.

## Co je správně

Původní problém s lokálním aliasem má skutečnou opravu: draft drží původní lokální hodnoty a jejich
verzi odděleně od nového vzdáleného snapshotu. Čistý formulář přijme novější uložené údaje; upravený
formulář při cizí lokální změně vyžaduje explicitní vyřešení konfliktu. Save používá původní tokeny
a immutable submission, nikoli libovolně nejnovější tokeny k zastaralému textu. Nativní testy přes
dva vlastníky navíc ověřují, že beze změny zůstanou nedotčená pole a jiný provider. [S07–S09]

Sharing/Sources používají skutečné renderery, typed origins a konkrétní potvrzení. Nativní management,
recovery a potvrzení doručení zůstávají u dosavadních vlastníků. Oprava čekání na konec aktivního
doručení brání tomu, aby jej zrušil vlastní navazující refresh. Workflow selector dostává bezpečný
modelový katalog a informaci o source-managed původu; názvy se zobrazují lidsky, skutečné opaque ID
se zachovává pro uložení a volání. Tyto části nemá další úloha přepisovat. [S06,S08,S10,S11]

## Testy mají výrazně lepší pokrytí, ale kvalifikovaný výsledek

Zpráva uvádí 19/19 protokolových scénářů nad finálním image a úspěšné nativní porovnání modelů zdroje
s oběma klienty. Finální fixture už nepřejmenovává rezervované bootstrap profily: nejprve zachytí
jejich skutečný katalog, potom podporovaně vytvoří operator-owned profily a ověří původní seedy.
Očekávání nevyžaduje pevný počet modelů z tohoto zadání. [S04,S31]

Původní devítipřípadový consumer běh měl šest úspěchů a tři selhání. Následná přesná ověření uzavírají
History/rotaci credentials, Agent/file a image/vision. Celkem je tak doložených devět různých cest,
ale nikoli nový jediný čistý devítipřípadový běh. Zdroj testu Agent/file skutečně ověřuje skrytý canary,
konkrétní schválení/zamítnutí, native identities, bajty souboru a nezměněného souseda. [S05,S33]

Frozen Stable: 24 assemblies, 16 159 provedených případů, z toho 16 144 pass, 15 fail, nula skip.
Deset selhání souviselo s naplněným jednogigabajtovým PostgreSQL tmpfs; stejná assembly má následný
102případový úspěšný výběr na větším izolovaném prostředí. Následovaly přesné opravy guardů a
HostPlatform klasifikace. Source-only kontrola secretů je poctivě oddělená od původního checkout
skenu i od exportních artefaktů. Původní smíšený běh se nepřepisuje na all-green. [S05]

Všechna čísla jsou výsledky zaznamenané Codexem. Při této revizi jsem .NET, Docker, browser ani watch
nespouštěla a neměla privátní TRX, kompletní logy nebo screenshoty. Četla jsem zdroj a testovací zprávy;
nejde o nezávisle zopakovanou kampaň.

## Malé dočištění: Tooltip

Zpráva zachytila dvě zrušená tooltip disposal volání u odpojených circuitů. Příslušná Components
implementace zachytává JSDisconnectedException a ObjectDisposedException, ale nikoli cancellation
při uvolnění modulu. Existující testy pokrývají úspěšný import a souběh s Dispose, ne zrušené uvolnění.
To odpovídá konkrétní neobsloužené cestě; není tím prokázáno, že ji zavedlo PP2 nebo který konkrétní
browserový požadavek byl původcem historického logu. [S05,S12,S13]

Nové S0 požaduje nejprve deterministickou reprodukci, potom malou opravu v Components a ověření
skutečných spotřebitelů. Žádné plošné potlačení JS chyb, prodlužování timeoutů ani kopie tooltipu
v aplikaci. Musí být otestováno i normálně fungující hover/focus, zrušení během importu, frontované
volání a dvě nezávislé komponenty. Výsledek se musí doručit přes přesnou podepsanou verzi sourozeneckého
repozitáře, nikoli jen existovat v lokálním adresáři.

## Co bude PP3 obsahovat

Celou Request History: globální pohled i History jednoho provideru, všechny základní a pokročilé
filtry, applied query, výsledky, stránkování, coverage, metadata detail a výslovně načítaný obsah.
Existující výsledkové, metadata a content komponenty už jsou v AgentFramework.UI/History; zadání je
využije a doplní zbývající skutečné renderery. Nový projekt není povinný, pokud existující lehká
hranice vyhovuje. Samostatná funkční sandboxová cesta povinná je. [S14–S26]

Hlavní pravidla jsou nulové automatické dotazy po otevření záložky, oddělení rozepsaných filtrů od
skutečně odeslaného dotazu, přesný kurzor a UTC interval a samostatné oprávnění pro metadata a obsah.
Přepnutí databázového profilu nebo uživatele musí ukončit správné pohledy; opozdilá odpověď nesmí vrátit
starý obsah. Nativní kontrola canonical ownera a jeho verze zůstává tam, kde je nyní. Žádný nový
History backend, kopírování konverzací do druhého úložiště ani změna retence. [S15,S19,S20,S24,S25]

PP3 znovu otestuje skutečnou historii požadavků agentů, Simple Chatu a Workflow, včetně tří instancí,
rotace credential a odmítnutí veřejného či omezeného přístupu. Finální image musí obsahovat novou
History i otestovaný Components commit. Celý nezměněný 19případový protokol není potřeba opakovat před
každou etapou; změna jeho kontraktů/runtime jej ale znovu vyžádá. O širším Stable rozhodne konkrétní
invalidation analýza, ne automatické pravidlo po každém souboru.

## Co zůstane potom

Providerové provozní dialogy (test chat, model maintenance a zbytkové diagnostické plochy), capability
wizard/týmové editory, chat/usage/Voice audit; potom Workflow authoring, Workbench a nakonec Processes.
Workspace, Projects a desetisekční editor agenta se znovu plošně neextrahují. Náročnost History je
přibližně 3–4/5 kvůli autorizaci a životnostem, nikoli kvůli počtu Razor souborů.

Nové vizuální testování zůstává large-screen only, především 1920 × 1080. Codex má včas požádat o nativní
PGP odemčení, držet existující hostové signing prostředí a dělat větší podepsané checkpointy. Historické
bundles se nemají mazat a push/merge zůstává mimo tento úkol. Placené modelové požadavky nejsou nově
autorizované.
