# Hranice architektury a rozhodnutí

## ADR-01: Ponechat autoritu v doménových ownerech

Nezavádět „ConcurrencyService“, který by znal CRM, Projects, agent definice a všechny EF tabulky. Taková služba by svázala moduly a musela by duplikovat jejich validační a lifecycle pravidla. Owner nadále rozhoduje, co je platný zápis, jaké původní očekávání vyžaduje, jaké zámky/unikátní podmínky platí a co přesně bylo přijato.

Sdílená část poskytuje pouze čisté třícestné porovnání, srozumitelnou prezentaci a bezpečný lifecycle pozorování. Typy jsou návrh, ne vynucený seznam interface.

```text
Native Blazor page / feature host
  ├─ existing feature session + draft / EditContext
  ├─ feature-owned editable adapter and field descriptors
  ├─ feature owner (read / conditional write / exact acknowledgement)
  ├─ neutral change-observation adapter
  └─ lightweight merge surface in existing overlay
        ├─ pure merge model / algorithm
        └─ shared BaseLib / existing overlay primitives

Feature owner ──after durable commit──> neutral change-hint publisher
No arrow: pure merge -> EF / module / Web / authenticated principal / service locator
No arrow: feature renderer -> concrete owner / database / sibling module
```

## ADR-02: Maximálně malé nové sdílené rodiny

Preferovaný kandidát je čistá .NET knihovna `src/Foundation/CanDoItAll.Editing` a lehká renderer rodina `src/UI/CanDoItAll.AppComponents.Editing`. Je přijatelné použít existující stejně lehký a správně vlastněný projekt, pokud jeho reference nezatáhnou zbytek aplikace. Čistá část nepotřebuje Blazor, EF, JSON serializer ani DI. UI část potřebuje pouze čisté modely a vybrané shared UI primitives.

Feed kontrakt umístit do již vhodné lehké infrastrukturní abstrakce, implementaci do Infrastructure/host composition. Nevkládat business field definitions do SharedKernel a nedělat referenci na Workbench jen kvůli jeho feedu. Nezavádět třetí abstrakční projekt, pokud nepřináší skutečně jiného vlastníka či závislostní oddělení.

Prompt UI má dnes velmi lehký boundary a sandbox explicitně vylučuje těžký AppComponents (S16). **Nepřidávat referenci na celý `CanDoItAll.AppComponents` do `Prompts.UI`.** Merge může host složit v samostatném overlay vedle původního rendereru přes typed slot/adapter. Má-li feature UI přímo potřebovat shared merge surface, musí jít o novou lehkou rodinu, nikoli o shortcut přes Web nebo shell.

Před přidáním projektu vyhodnotit skutečný MSBuild dependency closure a historické guards. Guard se smí přesně aktualizovat na oprávněnou novou lehkou hranu, ne změnit na „neřeš reference“. Source sibling Components zůstává vlastníkem generických dialog/notification oprav. S02, S05, S06, S16.

## ADR-03: Nahradit jen chybějící schopnost, ne všechny editory

Již existující session zůstává vlastníkem běžného chování. Jednoduchý editor může vlastnit mutable form v rendereru, jiný používá host-owned draft a view contract. Nevynucovat přesun celého formuláře kvůli merge. Přidat úzký adapter, který zachytí stávající form, vrátí jeho revision a aplikuje ověřený plán do stejné instance.

Doporučené schopnosti adapteru:

| Schopnost | Význam |
|---|---|
| CaptureBase | immutable coherent editable baseline + native expectation |
| CaptureDraft | raw i typed values, stable collection identities, draft generation/revision |
| Describe | explicit field labels, groups, equality, type renderer, sensitive/read-only flags |
| ReadCurrent | authorized owner call; returns exact coherent R or deleted/denied/retired |
| BuildMerge | pure calculation B/L/R; no reads or writes |
| ApplyReviewed | checks target/opening/revision, patches existing draft, adopts R as baseline |
| CaptureSubmission | current ordinary validated save command with correct native expectation |
| Acknowledge | existing S/L/A logic; keeps edits made after submission |

Nepřidávat callbacky typu `Func<string,object,Task>` pro veškeré vlastnosti. Module adapter může použít typed delegates a generické scalar helpers, ale musí být jasná vlastnictví hodnot a bezpečná konverze. Cílem není serializovat entity a reflexí kreslit property grid.

## ADR-04: Identita má několik různých úrovní

Rozlišovat stabilní adresu dat a dočasný kontext otevřeného UI:

- **ResourceAddress:** instance, scope kind (`Instance` versus `Dataset`), profile/dataset incarnation dle ownera, owner kind, record identity, případně projekt a jeho lifetime. Instance-level API/security/storage config se nesmí násilím tvářit jako dataset record.
- **OwnerWitness:** původní verze nebo původní field/set observation. Může být timestamp, GUID, workflow version, Process observation či tuple; sémantiku definuje owner.
- **ViewContext:** aktuální profile activation generation, principal/security generation, opening generation, editor instance. Je to fence lokálního lifecycle, ne persistent resource ID sdílené mezi všemi instancemi.
- **OperationIdentity:** skutečný native operation ID tam, kde existuje receipt; jinak alespoň korelační ID bez předstírání idempotence.
- **FeedCursor:** stream epoch + sequence pouze pro doručování změnových hintů. Není to content version, auth token ani důkaz výsledku zápisu.

Porovnávat GUID nebo neprůhledné tokeny pouze podle owner-defined equality. Nikdy je neuspořádávat lexikograficky jako „novější“. Switching profile A→B→A vytváří nové otevření; starý callback se nesmí znovu stát platným jen proto, že ID profilu je stejné.

## ADR-05: Stav draftu a výsledek operace jsou ortogonální

Nepřidat stovky kombinací do jednoho enumu. Prakticky oddělit:

```text
Lifecycle:  Open | Retired | Disposed
Freshness:  KnownBaseline | ChangeHint | CurrentReadPending | Stale | Deleted | AccessLost
Draft:      Clean | Dirty | InvalidRaw
Mutation:   Idle | Submitted | Accepted | AcceptedWithWarning | Rejected | Unknown
Merge:      Closed | Loading | Reviewing | ReadyToApply | Invalidated
```

Ne všechny kombinace jsou povolené, ale jedna osa nevymaže jinou. `Unknown + ChangeHint + Dirty` je reálná situace: hint nesmí vyřešit unknown a merge nesmí povolit nový Save, dokud owner nevyřeší předchozí operation nebo bezpečný explicitní recovery postup.

Save admission se zkontroluje v handleru i na ownerovi, nejen na disabled tlačítku. Běžný guard vyžaduje aktuální opening, platné parsování/validaci, žádnou rozpracovanou operaci, žádný nerozhodnutý merge a odpovídající původní expectation. Opožděný event stále může přijít až po kliknutí; poslední slovo má atomická serverová podmínka.

## ADR-06: No forced overwrite / no silent automatic retry

Tlačítko „Použít moje“ znamená volbu hodnoty v konfliktu, ne obcházení CAS. Po výběru se vytvoří nový lokální návrh nad konkrétním R a další Save znovu ověří R. Žádný `Force=true`, wildcard token, automatické get-latest-token-and-replay ani retry loop přepisující výsledky dalšího editora.

Původní rejected command je neměnný. Pokud native owner má durable identity, nové vědomě sloučené odeslání dostává novou operation identity. Replay staré identity se smí použít pouze pro původní přesně stejnou submission při předepsaném recovery, ne pro jiná data.

## ADR-07: Minimální bezpečnostní příprava nyní, nové produktové funkce zvlášť

Teď: trusted actor, scope fencing, authorized hint subscription/read/write, policy hook, revocation a local draft history seam. Později po samostatném návrhu: plné project membership, ownership migration, grant UI a persisted collaborative Undo. Obojí je rozpracováno v [autorita a historie](05_AUTHORITY_AND_HISTORY.md).
