# Sdílený architektonický bundle pro rozplétání UI komponent

**Reference:** `CDA-UI-SEAMS-BASE-v1`  
**Charakter:** dočasný, neimplementační a neexekuční základ pro navazující bundly

## Smysl

Tento bundle sjednocuje důvody, cílovou architekturu a rozhodovací pravidla pro postupné
rozplétání Blazor komponent v hlavním CanDoItAll. Neobsahuje refaktor konkrétní oblasti.
Agents, Providers, Processes, Projects, Workbench a další části dostanou vlastní menší
implementační bundly, které se na tento základ odkážou.

## Potvrzená rozhodnutí

- `Components` a `FileTools` zůstávají při vývoji připojené živě jako sibling source
  projekty. Lokální NuGet balíčky se nyní neřeší.
- Nejprve se komponenty rozpletou na současném místě. Fyzický přesun do
  `AppComponents` nebo budoucího `Modules.<Feature>.UI` přijde až po ověření hranice.
- `AppComponents` vlastní jen aplikačně obecné UI bez závislosti na konkrétních modulech.
- Feature komponenta zůstává ve svém modulu, i když se používá na více místech, pokud
  nese význam dané feature.
- Nevytváříme wrapper, interface ani další view model automaticky. Nová vrstva musí
  odstranit konkrétní problém s vlastnictvím, I/O, hostem, stavem nebo testovatelností.
- Routing se implementuje později, ale už nyní musí routovatelná page/workspace vlastnit
  stav, který bude jednou reprezentovaný v URL. Child komponenty dostávají typed state a
  emitují typed intent.
- Neudržujeme testy, které pouze fixují počet partial souborů, přesné názvy privátních
  metod nebo aktuální rozložení zdrojáků. Implementační bundle má takové testy v dotčené
  oblasti odstranit nebo nahradit testem skutečné hranice či chování.
- Tento bundle nebude před mergem zachován. Dlouhodobé poznatky se nejprve promítnou do
  dokumentace nebo `CanDoItAll.SharedInfo`.

## Co se nyní nedělá

- žádný konkrétní Agents refaktor;
- žádné přesuny komponent;
- žádné nové UI projekty;
- žádný sandbox host;
- žádné URL codec/navigator změny;
- žádný zásah do Manageru nebo `dotnet watch`;
- žádný redesign;
- žádné produktové testy vázané na tento základní bundle.

## Základní umístění

```text
CanDoItAll.Components
  obecné komponenty použitelné mimo hlavní aplikaci

CanDoItAll.AppComponents
  shell, navigace, overlays, obecné record browsery, filtry,
  tuning a host adaptéry specifické pro CanDoItAll jako celek

CanDoItAll.Modules.<Feature>.UI
  budoucí domov feature komponent, jejich presentation state,
  intentů a úzkých UI-facing kontraktů

CanDoItAll.Web / Composition
  routing, host wiring a konkrétní registrace implementací
```

## Povinnost navazujících bundlů

Každý implementační bundle musí uvést referenci `CDA-UI-SEAMS-BASE-v1`, znovu ověřit
aktuální `development`, vyplnit mapu stavu a závislostí, určit skutečné cílové švy,
vlastnit své testy a na konci vyhodnotit:

```text
route-ready?
sandbox-ready?
project-extraction-ready?
```

Povinný formát je v `plan/01-child-bundle-contract.md` a šablony jsou v `templates/`.
