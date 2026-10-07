# Revize WB2 a návrh Workbench Structure Authoring WB3

## Verdikt a rozsah revize

Kontrolovala jsem `fyziktom/CanDoItAll`, `components-decoupling`,
`bbd9e8de96dc7895abdecc04406766f7aea94c8e`, proti předchozímu WB1
`be2045c312ee5fa99fb5b1f8526f1825ecf8352c`. Pět navazujících commitů zahrnuje
historické zadání WB2, implementaci reportů, výběrové plochy, testy a finální zprávu.
Aktuální HEAD je dokumentační uzavření po produktových změnách, nikoli jediný
podklad hodnocení. Components development je `24d182c664d0b1f293098643e52caed7384a5d50`.

WB2 bych zachovala jako dokončenou vymezenou UI rodinu. V prověřených cestách jsem
nenašla nový blokující problém vyžadující samostatnou WB2 closure. To není důkaz
bezchybnosti všech funkcí aplikace ani schválení release. Revize je zdrojová:
nespouštěla jsem produktové .NET/Docker/PostgreSQL/browserové testy a neměla jsem
privátní původní TRX, logy a screenshoty. Zaznamenané výsledky níže patří Codexu.

Odkazy, přesné soubory a rozsahy načteného kódu jsou v [registru zdrojů](SOURCES.md).

## Co je udělané správně

Reportovací session odděluje rozepsané volby od skutečně přijatého reportu. Čtení,
progress, chyba a finally jsou svázané s konkrétní operací; stará operace nevyčistí
novější CTS. Activity má vlastní session a původní reportový cutoff. Nativní source
si ponechává cursorovou a aggregate cache pro své otevření, kontroluje profil,
původní projektové životnosti i potomky před a po čtení. Retence uchovává hotové
snapshoty a volby, nikoli společnou rozpracovanou session několika panelů. [S04–S08]

Selection, Index a Signals předávají skutečný původ výběru, přesné cíle a nabídnutou
akci. Host před mutací ověřuje původní admission a dokončený efekt zaznamená před
následným refreshem. Nativní testy pokrývají mimo jiné A–B–A, podvržený cíl,
znovuvytvořený projekt, přesné potvrzení mazání, aditivní markery a nezměněné
sousední uzly. Nejde jen o render-only testování. [S09, S11]

Insights.UI odkazuje na BaseLib, CanvasLib, Charts a malý Insights.Contracts.
Nemá přímou implementační závislost na Workbench ani na databázi. Report uvádí
vyhodnocený graf sedmi projektů UI a osmi sandboxu a nezměněných 21 chráněných
kořenů. Tyto počty jsou jeho měření; sama jsem MSBuild graf znovu nevyhodnocovala.

Předchozí oprava Ganttu je přítomná i ve vzdáleném Components. Dispose nezávisle
zkusí úklid po chybě čekající aktualizace a zachová oba problémy, pokud selže také
úklid. Identita JS instance umožňuje uvolnění po odstranění původního DOM elementu.
Samostatné testy pokrývají zrušení, chyby a nezávislé instance. Historická poznámka
„local-only“ už není současným blokátorem; neopakovat opravu ani požadavek na push.

## Testy a jejich kvalifikace

| Vrstva | Zaznamenaný výsledek a hranice |
|---|---|
| Shared Gantt | 102 úspěšných případů a source/published/native close-remount důkaz. |
| Zaměřené reportovací native výběry | 54 Unit a 71 PostgreSQL Integration; dvě další perzistentní paging cesty přes 20 + 5 záznamů. Výběry se nesčítají jako jistě unikátní pokrytí. |
| Mezikontejnerový protokol | 19/19 na deklarovaném image W2 + opravené Components; samostatné defaults/custom katalogové browserové případy. |
| Broad Components | 2 649 provedených: 2 646 pass, tři event-dispatch chyby; následný čerstvý třípřípadový opravný výběr má vlastní úspěšný výsledek. |
| Zmrazený Stable | 16 662 provedených: 16 658 pass, čtyři fail, nula skip; 16 607 discovery + 55 deferred theory expansions, 29 assemblies. |
| Native Integration uvnitř Stable | 3 289 úspěšných případů; je to podmnožina, ne další číslo k přičtení. |

Zbývající široké selhání je identifikovaná historická syntetická kontrola secret scanu.
Tři komponentové chyby mají konkrétní opravu synchronizace testů. Původní Stable
zůstává smíšený; úspěšný follow-up ho zpětně nepřepíše. Tentokrát report uvádí
předběhové source i assembly manifests a následně potvrzenou shodu 29 hlavních DLL.

Dvě původní celé Insights consumer cesty neprošly kvůli nevhodným očekáváním
hierarchie a hlavičky. Přijaté efekty se zachovaly a pozdější pokračování skutečně
ověřilo zbývající reportové/History kroky bez opakování agentových mutací. Není to
jeden původní čistý all-green běh. Stejné rozlišení platí pro časování floating
odpovědi, inicializaci odvozeného Usage indexu a opravený fixture helper credential
rotation. Nemám podklad tyto původní neúspěchy jednoduše odstranit z hodnocení.

Vývojová smyčka malého hostu funguje, ale není univerzální zrychlení všech cest.
Native CSS hot reload havaroval v SDK; úspěšná native CSS měření použila rebuild/restart
přes `--no-hot-reload`. Managed hot reload a některé automatické browser navigace
jsou vykázané odděleně. Nový bundle tuto kvalifikaci zachovává a neobjednává
neomezenou opravu celého SDK nebo mobilního vzhledu.

## Doložené riziko dalšího řezu: WB3-H1

Ve starším `ExecuteProjectHierarchyCommandAsync` se po čekání na Add/Reconnect
znovu dereferencuje živé `projectHierarchyDialog`. Skutečný dialog mezitím dovoluje
Cancel či další interakci. Když A zanikne, dokončení může dereferencovat null;
když jej nahradí B, A může zapsat chybu do B nebo B zavřít a použít jeho výběr.
Otevření dialogu také přijímá pozdní načtení seznamu bez samostatné identity otevření.

To je závěr z konkrétního toku [S16, S17], ne mnou spuštěná runtime reprodukce a ne
chyba přisuzovaná WB2. Nativní změna A už mohla být uložená. S0 vyžaduje nejprve
regresní test přes skutečné UI a řízeného nativního vlastníka, pak zachycení původního
otevření, submission, cíle a výsledku. Obdobná místa jsou ve zvolené rodině block
conversion a subtree transfer [S18]; jejich testování patří do stejného řezu.

Oprava nesmí místo toho zavést globální zámek dialogů, automatické znovuvytváření
projektů, slepé opakování neznámého výsledku ani převzetí nové životnosti podle ID.

## Větší navazující celek

WB3 zahrnuje hlavní Structure shell, skutečný CanvasWorkbench, toolbar, toolbox,
běžný create/edit composer, grafová gesta, clipboard, hierarchické a konverzní
dialogy, přesun větve do podprojektu a prezentaci původních delete/cleanup výsledků.
Výstupem bude skutečná lehká knihovna a nezávislý sandbox, ne jen rozdělení
`ProjectStructurePage` do dalších partial souborů.

Preferované projekty jsou `CanDoItAll.Workbench.Structure.UI` a
`CanDoItAll.Workbench.Structure.UiSandbox`. Nový malý Contracts projekt je možnost
pro konkrétní hranici. Smíšené EF entity/služby ani všechny Workbench modely do něj
nepatří. Nativní metadata projekce, dynamic choices, autorita, skuteční writery,
kompenzace a runtime zůstávají u vlastníků.

Hotové WB1/WB2 rodiny se použijí přes skutečnou kompozici, ne zkopírují. Specializované
party/meeting a přiřazovací plochy, secrets, media/transcript, souborový browser a
runtime/web/terminal, Workflow/Process launch a recovery zůstanou přiznanými funkčními
hosty. Jejich kouřové integrační testy neznamenají dokončení jejich extrakce.

## Co je potřeba ochránit

Běžná změna názvu uzlu nesmí odstranit neviditelná metadata, reference, časovou
přesnost ani legitimní nulové souřadnice. `TryApplyNodeEditAsync` dnes hledá cíl
v aktuálním surface; konkrétní stale-composer cesty se musí nejprve ověřit proti
skutečnému shared composeru a přenést původní admission až ke writeru. [S13, S14]

Clipboard dál nepodporuje Duplicate a nedovoluje libovolné cross-project paste.
Cut zachovává původní identity; copy musí mít ověřenou mapu a původní omezení
projekcí a cyklů. [S15] Přesun do nového podprojektu je posloupnost vytvoření,
transferu a případné přesné kompenzace s existujícími receipts. Nejde o novou
atomickou transakci a nesmí se tak prezentovat. [S19]

## Provedení a pokračování

32 skupin ověření pokrývá skutečný graf, ovládací prvky, původní lifecycle,
raw/fidelity, hierarchical a partial výsledky, source/published sandbox a relevantní
operator/Agent/file/Workflow/Scheduler/History průchody. Nástroje, schvalování,
identita a uložené bajty zůstávají reálné; skriptovat lze externí modelovou odpověď.

Pouze large desktop 1920×1080 DPR1. Podepsané commity po větších etapách, včasné
nativní PGP odemčení, bez push/merge, mazání historických bundles nebo placené
inference. Široký Stable jen při pojmenovaném důvodu po ustálení změn, nikoli po
každém panelu; povinné statické kontroly zůstávají.

Po WB3 zbývají specializované Workbench integrace a jejich konkrétní UI; Processes
jsou nadále poslední velká rodina. WB3 nemá předstírat dokončení celého Workbenche.
