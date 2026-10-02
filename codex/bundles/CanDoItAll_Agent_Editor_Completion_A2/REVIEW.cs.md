# Revize A1 a návrh dokončení technického editoru A2

## Závěr

Architekturu A1 bych zachovala. Předchozí oprava Projects Files je v prověřeném kódu přítomná.
Následující bundle nemá znovu otevírat Workspace nebo Projects. Má nejprve opravit konkrétní
zdrojově odvozenou chybu Verify v dosud neoddělené Capabilities sekci a potom dokončit zbývajících
šest sekcí technického editoru, skutečné Memory/external-root renderery a malé potvrzovací dialogy.

A2 je větší, ale ohraničený celek. Nezahrnuje plošnou providerovou administraci, celý capability
wizard, Workflow authoring, Workbench ani Processes. Dokončení deseti sekcí neznamená dokončení
všech obrazovek AgentFrameworku nebo potvrzení bezchybnosti celé aplikace.

## Kontrolované verze a historie

Produktová revize: `ed64d4edf868cb26c749c31a94ff918683e0f4a0`.
Pozorovaný HEAD: `0aad5360b4ac037ed4471ed44fc6083b7f09fb85` na `components-decoupling`.
Poslední commit je přímým potomkem implementace a přidává pouze 44 souborů původního A1 bundle.
Nejde o novou produktovou změnu. Components development byl při kontrole dostupný na
`4a858412d2c2a3f6123bf23d8c4584f05b47627d`.

Commity jsou původem zjištění, nikoli povinným checkoutem. Historické bundles mají zůstat během
práce zachované; jejich odstranění před mergem je pozdější krok operátora. Nové zadání neobsahuje
push, merge, vydání balíčku ani odstranění cizích nebo úmyslně necommitnutých souborů.

## Co je správně

A1 má skutečnou lehkou renderovací knihovnu a vlastní sandbox; používá původní session, celý
agentový draft a jeho EditContext. Model a thinking-effort selektory dostávají neutrální prezentaci
místo Core/Voice/Canvas implementací. Sdílená změna Immediate je opt-in, aby neměnila dosavadní
chování ostatních spotřebitelů. Typy sekcí mají kompatibilní přesměrování starých assembly identit.

Provider refresh kontroluje původní session, provider ID a revizi výběru včetně A → B → A.
Oprava Files P2 navíc testuje identitu konkrétní operace i v chybových větvích, nejen identitu
celého otevřeného workspace. Zdroje: R01–R05, R14 a R16.

Nativní core-only round-trip test zahrnuje běžného agenta i template: zachovává projektové
životnosti, skutečné externí root bindings, storage restrikce, Memory, secret references,
capabilities, Favorite, skryté parametry a neznámé JSON rozšíření. Ověřuje konflikt staré verze
a nezměněného druhého agenta. Je to užitečný důkaz implementátora, nikoli test spuštěný při této
revizi. Zdroje: R01 a R16.

## A2-R1: Verify není Save rozepsaného editoru

Uživatel načte agenta, upraví například Name a Instructions, neuloží je a stiskne Verify u
přiřazené capability. Handler zachytí aktuální draft a zavolá diagnostiku pouze podle agent ID
a capability ID. Po dokončení použije stejnou rekonciliaci jako běžný Save.

Když uživatel po kliknutí nic dalšího nenapsal, HasLaterEdits vrátí false: porovnává totiž draft
s okamžikem kliknutí, ne s poslední uloženou verzí. Následné owner.Load(refreshed.Draft) tak
nahradí rozepsaná data původně uloženými hodnotami a vytvoří nový EditContext. Zdroje: R04,
R10–R13. Jde o zdrojově odvozenou chybu, která předchází A1; runtime reprodukci jsem zde neprovedla.

Oprava nesmí před Verify potichu uložit celý formulář ani Verify zakázat jen proto, že je dirty.
Má zachovat veškerou neuloženou práci a aktualizovat skutečný výsledek diagnostiky. Důležitý detail:
nativní publikace proof současně mění UpdatedAtUtc agenta. Nesmí se proto slepě převzít libovolná
nejnovější verze a tím skrýt konkurenční změnu. Zadání vyžaduje přisouzení verze skutečné vlastní
publikaci nebo zachování konfliktu a draftu; využívá již existující receipt/outcome koncepty.

Testovací matice zahrnuje úpravy před i během Verify, selhaný read-back a jeho retry bez nové
diagnostiky, dva editory, konkurenční změnu, zrušení dialogu a skutečně neznámý výsledek publikace.
Nejde o prokázané obcházení oprávnění nebo poškození uložené konfigurace.

## Obsah většího A2

1. Project Structure Access, Secrets a Process Access: skutečné renderery, přesné identity,
   oddělené stavy referencí a zachování existující nedostupnosti výběru procesních definic.
2. Memory a Workspace Tools: binding list, aliasy, módy, skutečná způsobilost providerů,
   odebrání navazujících referencí, externí cesty a existující Storage picker.
3. Capabilities a malé potvrzovací dialogy: stage versus Save, skutečné Verify/outcomes,
   původní cíl potvrzení, přesný vytvořený capability ID před následným čtením.
4. Úplný sandbox deseti sekcí, nativní ověření, produkční agentové/workflow/souborové scénáře,
   samostatný publish a vývojový cyklus.

Memory dnes přímo injektuje profile store a drivery; externí root komponenta vytváří hostový
registr. Pouhý přesun souborů by ponechal těžký graf. Tyto operace musí zůstat u vlastníka a
renderer dostane bezpečnou projekci. Již oddělený Storage picker a AgentCapabilityList se mají
skutečně využít, ne kopírovat. Zdroje: R06–R09, R15, R18–R20.

Preferuji rozšíření současného Editor.UI a sandboxu, nikoli nový projekt pro každou sekci.
Důležité jsou skutečné tranzitivní reference a vlastníci, ne počet interfaců. Ostatní hotová UI
nesmějí získat zpětné nebo implementační reference. Význam oprávnění, schéma ani runtime se
kvůli extrakci nemění.

## Výsledky A1 a jejich omezení

Zaznamenané finální výběry: 194 komponentových případů, 144 policy/command unit případů,
12 nativních adapter případů, 16 neutrálních leaf případů, 15 Simple Chat consumer případů
a 3 browserové případy. Výběry se nesčítají jako jistě unikátní pokrytí. Původní browserový
pokus 7 pass / 1 fail zůstává smíšený; následná oprava prázdného jména má vlastní důkaz.
Šest externích odpovědí v pozitivním agentovém scénáři bylo skriptovaných, nikoli placená live AI.
Zdroj: R01.

A1 neprovedlo nový celý Stable; uvádí ohraničený zásah a aktuální owning/caller proof. Pro A2
je potřeba rozhodnutí posoudit znovu podle skutečných zásahů, zejména do verification result
nebo společného owner kontraktu. Není důvod opakovat celou mnohahodinovou suite po každé záložce,
ale starý výsledek také není automatický důkaz nové společné změny.

Implementátor zaznamenal 11 projektů v Editor.UI closure, 12 v sandboxu, 534 sledovaných cest
sandboxu a nezměněné closure 41 starších kořenů. Některé Web/Razor vzorky se nezrychlily. Přínosem
je doložená izolace; nelze tvrdit univerzální zrychlení všech typů editace. Nové měření má opět
zahrnout skutečný graph/watch set a vrácení přesných bytů sond. Zdroj: R01.

## Roadmapa po této revizi

| Oblast | Stav / další krok | Relativní náročnost |
|---|---|---|
| Projects P1/P2 a Workspace | Vybrané renderovací hranice dokončené, dále regresní ověřování | Bez nové plošné extrakce |
| Technický editor agenta | A1 hotové; A2 dokončí šest zbývajících sekcí a malé související renderery | 4/5 tento větší bundle |
| Providerová administrace | Profilové sekce, thinking/test/history/model maintenance a sdílená spojení | 4/5, zhruba 1–2 následné řezy |
| Capability authoring a týmové dialogy | Skutečný MCP/skill/tool wizard, definice a další editory mimo A2 | 3–4/5, zhruba 1–2 řezy |
| Chat, usage a globální voice zbytky | Nejprve audit skutečného renderování; některé hosty už skládají hotové UI | 2–3/5, zhruba 1–2 ohraničené řezy |
| Workflow authoring | Canvas, toolbox, inspector a související nastavení/run prezentace | 4/5, zhruba 2–4 řezy |
| Workbench | Nejprve menší kalendář/přehledy, potom úkoly/assignment, Structure a runtime/file integrace | 5/5 celek, zhruba 4–6 řezů |
| Processes | Katalog/definice, launch/schválení, live monitoring, recovery | 5/5, zhruba 4–6 řezů; poslední velká oblast |
| Web Home/runtime/shared zbytky | Závěrečný audit opakovaně použitelných ploch versus legitimní Web kompozice | 2–3/5 podle nálezů |

Odhady nejsou časový příslib ani odvozené procento dokončení. Zdroje R21–R25 a aktuální soupisy
cest v review-provenance.json podporují rozsah rodin; budoucí velké moduly nebyly nyní řádek po
řádku behaviorálně auditované. Celé původní pořadí AgentFramework → Workflows → Workbench →
Processes je stále rozumné. Původní odhad 2–4 řezů AgentFrameworku byl hrubý: nemá vést ke sloučení
všech providerových a runtime obrazovek do jedné rizikové změny.

## Podporovaná obrazovka a testovací bezpečnost

Nové UI ověření je 1920 × 1080; druhý velký rozměr jen kvůli konkrétní funkční otázce.
Žádná nová small/medium/mobile/responsive ladicí kampaň. Stávající obecné testy se nemažou.
Důkaz musí ověřit reálné uložení oprávnění, agenta nad Project Structure, file write/attach/read-back,
obsah souboru a odpovídající odmítnutí; ne pouze toast nebo zelený mock. Celá externí AI část může
být skriptovaná, vlastníci a schvalování nikoli. Nový placený rozpočet není udělený.

## Co tato revize neprokazuje

Pročetla jsem zdrojové soubory, aktuální soupisy a implementační zprávu. V tomto prostředí není
dotnet; nesestavovala jsem produkt ani nespouštěla jeho C#, PostgreSQL, browser nebo watch testy.
Původní privátní TRX, logy a screenshoty nebyly přiložené. Integrita tohoto předávacího ZIPu a testy
jeho Python nástrojů jsou samostatná věc a nesmějí se zaměňovat za úspěch aplikace.

Podrobné implementační instrukce jsou anglicky v [prompt.md](prompt.md), průkazy v
[VALIDATION_MATRIX.md](VALIDATION_MATRIX.md) a aktuální zdroje v [SOURCES.md](SOURCES.md).
