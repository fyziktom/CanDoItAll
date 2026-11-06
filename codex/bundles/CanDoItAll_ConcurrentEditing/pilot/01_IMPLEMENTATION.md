# Pilot Prompt Gallery — konkrétní implementační zadání

## Rozsah pilotu

Pilot zahrnuje `/prompt-gallery`, její běžný editor, stejný sdílený editor v Gallery pickeru, nutné owner/caller opravy Prompt operací, společné čisté merge jádro, lehký merge renderer, změnový observer a sdílenou opravu notifikací. Nested chat/workflow consumer regression je nezbytná integrační kontrola, nikoli povolení rozjet rollout jejich celých editorů.

Po schválení budou ostatní moduly jen adaptovat malé ověřené rozhraní. První skutečný second-domain adapter (CRM) může odhalit malou nutnou úpravu generického API; nepředvídat ji předem desítkami unused abstractions.

## A. Reprodukce a baseline

V izolovaném test hostu vytvořit dva skutečně nezávislé browser contexts a sdílený prompt. Zapsat chování stávajícího stale Save, reload/cancel/close, validation raw inputs a toasty za native dialogem. Změřit list query count, open editor reads, input/render chování a baseline build loop. Nevykonávat load/fixture testy na uživatelově databázi 5032.

Přečíst `PromptGalleryLimits`, aktuální owner contracts, skutečný search driver, import/curator callers a aktuální host/modal composition. Zdejší source audit neotevřel každého calleru, P0 musí uzavřít celý prompt write perimeter a hlavní hosty před změnou kontraktů.

## B. Coherent editable snapshot

V `PromptsService.Persistence.cs` upravit read pro edit/merge tak, aby všechny mutable údaje a root expectation patřily k jedné DB snapshotové revizi. Preferovat krátkou owner-owned read transakci s odpovídající izolací při vícedotazovém čtení; respektovat případnou vnější transakci/coordinator. Alternativou je jedna konzistentní query nebo validovaný bounded read-before/after revision pattern pouze pokud každý relevantní writer revision opravdu mění. Bez prokázaného perimeteru druhá varianta nestačí.

Nedotahovat zbytečně všechny historické verze pro každé freshness ověření. Reuse/minimálně rozšířit existující DTO místo nového shadow domain modelu; editable snapshot může být lehčí owner read, zatímco historie zůstane lazy/read-only. Capture source/hidden metadata nutných pro lossless chování a lifecycle guard, ne jen viditelné labely.

Očekávané timestampy zachovat neprůhledně a v DB přesnosti. Současný `NextUpdatedAt` už normalizuje mikrosekundy. Nesrovnávat „větší timestamp“ namísto přesné expected equality; nezměnit UTC normalizaci.

## C. Přesný accepted stav

`SaveDraftAsync` dnes vrací ID a accepted timestamp a session drží normalized submitted content. Ověřit, zda to plně pokrývá skutečnou normalizaci všech dětských kolekcí a nullable doporučení. Preferovat exact accepted editable snapshot vytvořený ze stejného committed owner stavu; případně zachovat stávající tvar, pokud lze prokázat stejnou přesnost bez dodatečného current read. Nedělat z každého receipt generic runtime framework.

Own readback s aktuálnějšími child rows nesmí povýšit tyto child rows na původní accepted A. Dosavadní mixed read-back regresní testy S16 se zachovají; coherent read zmenší možnost vzniku problému, ale nezruší rozdíl mezi A a pozdějším R.

## D. Sdílené jádro a prompt adapter

Realizovat scalar, membership set, keyed collection, bounded text diff a explicitní group validation. Prompt schema:

| Pole | Výchozí merge/render |
|---|---|
| Title, Summary, Kind, Phase | popisky uživatelského formuláře; scalar/multiline podle délky |
| Content | víceřádkový řádkový diff, editovatelný výsledný prompt, overlap ručně |
| Tags | normalizované tag membership; ukázat přidáno/odebráno |
| SupportedModels | dvojice provider/model, explicitní preferred conflict; řádkový editor |
| SupportedConsumers | množina jmen spotřebitelů; po merge compatibility revalidation |
| Temperature, MaxOutputTokens, TopP | čísla s původním raw inputem a nullable významem |
| Project/Collection linkage | předem určit editable owner; nepřenášet starou vazbu neviditelně; změna lifecycle/context may retire |
| IsArchived / Status / version | samostatný lifecycle panel, ne prostá scalar auto-volba |
| IsFavorite / suppression | oddělené narrow intents/preferences; nezahrnout je do whole-content replacement nechtěně |

Adapter nemá přijímat celé EF entity. Pro text automaticky spojovat jen prokazatelně nekolidující hunks; u malého pilotu je přijatelnější přehledný explicitní text konflikt než agresivní chybně automatizovaný merge. Potřebný bounded algoritmus a jeho licence/dependency musí být zhodnocené; nepřidat celý editor framework kvůli jedné funkci.

## E. Existující form a session

`PromptGalleryEditorForm.cs`: doplnit minimální capture/apply/dirty mechanismus. Zachovat CurrentForm, validaci a typed submission. Track změny kolekcí i raw input, ne jen DataAnnotations model values.

`PromptGalleryItemEditorSurface.razor`: vystavit úzké typed capture/apply/dirty callbacky nebo přirozený adapter seam. Same-target foreign hint/review nesmí vytvořit nový Source a tím nový EditContext. Reviewed patch aplikuje do téže instance; `Source` replacement patří až explicitnímu discard reload nebo jinému targetu.

`PromptGalleryEditorSession.cs`: doplnit original B/current R a session freshness, per-opening merge generation a sticky unknown guard kompatibilní se současným výsledkovým modelem. Znovupoužít dosavadní capture před awaits, busy guard, callbacks, exact receipt a retirement. Rozlišit external conflict od vlastního accepted readback warning.

Zpracování intentů musí kontrolovat generation a Save eligibility i bez UI. Po aplikaci merge zůstává obyčejný Save. Cancel merge nic nemění, Retry current pouze čte. Během review lze parent fieldset zmrazit; pokud se objeví draft změna z jiné cesty, Apply musí revision mismatch odmítnout bez ztráty L.

## F. Změnové hinty a seznam

Publikovat post-commit pro každý relevantní prompt writer včetně content save, version/finalize, archive/restore, favorite a případných links/import/curator změn. Explicitně oddělit content/lifecycle/metadata kategorie. Staged `PromptGalleryMutationService` event musí respektovat vnější commit. Rollback nic neoznamuje. Pokud secondary projection selže, canonical změna je stále přijatá a editor ji má dostat.

Page a embedded host subscribe podle vlastní opening/scope. Page list subscribe katalogový scope, ne jednotlivé ID. `PromptGallerySearchSession` dostane režim quiet refresh s retained rows, coalescing a existujícími generation fences. Query index se při delete poslední položky korektně srovná. Aktivní editor nezmění target při změně sort/order nebo filtru.

`editorDirty` ve stránce přejmenovat/sémanticky oddělit na **catalog invalidated by own commit** a **local editor dirty**. Nemíchat je. Po vlastním accepted save může list refresh proběhnout i při stále otevřeném modálu; musí zůstat izolovaný od form instance.

## G. Close a overlay

Stejný close guard propojit na page i picker. Změna `promptId`, back/forward, programmatic cancel a parent modal close musí projít rozhodnutím. Žádný „zavři a potom zkontroluj dirty“. Přidat trvalý inline status i doprovodný toast. Globální toast opravu provést ve sibling BaseLib/overlay mechanismu, ne stylem v Prompt page. Specifikace v [overlays](../architecture/04_OVERLAYS.md).

## H. Minimální souborový plán

| Stávající místo | Zamýšlená změna |
|---|---|
| `src/Modules/CanDoItAll.Modules.Prompts/PromptsService.Persistence.cs` | coherent edit read + zachování normalizace a children |
| `.../PromptsService.Gallery.cs` | accepted snapshot/observability boundary, event publication, lifecycle safeguards |
| `.../PromptGalleryMutationService.cs` | post-outer-commit hint pro staged writes |
| `.../Components/PromptGalleryEditorSession.cs` | typed baseline/merge lifecycle, handler guards, retained unknown |
| `.../Components/PromptGalleryItemEditorHost.razor` | native adapter, subscription lifetime a merge overlay composition |
| `.../Components/PromptGalleryPickerDialog.razor` | nested close/target guards a sdílený editor behavior |
| `.../Components/PromptGallerySearchSession.cs` / `SearchHost.razor` | quiet invalidation/coalescing bez source/draft resetu |
| `.../Pages/PromptGalleryPage.razor` | dirty semantic split, route/close guard, page-level list subscription |
| `src/UI/CanDoItAll.Prompts.UI/Editor/*` | stable draft patch/capture a uživatelsky čitelný status/intent seam |
| lehká shared pure/UI rodina | merge model/algoritmus/renderers bez modulů a EF |
| shared Components Notification/Dialog/overlay infrastructure | layer-aware outlet, jeden lifecycle, správná interaktivita |
| owning tests a sandbox scenarios | nové stavy, failing-first regressions, bounded local test lanes |

Tři tečky označují stejný module root, ne neexistující doslovnou cestu. Přesné nové názvy a případné drobné přeuspořádání jsou na Codexu. Žádné čistě kosmetické rozdělování unrelated souborů.

## Pilot hotov, jen když

Všechny PG01–PG04 a SH01 jsou pokryté; vybraný systém ochrání různé i stejné pole, text/kolekce, invalid raw input, deleted/archived target, třetího editora, vlastní accepted save, unknown result, close/navigation a overlay pointer/focus. Nezávislé circuits vidí změny; own-source suppression nepotlačuje druhé okno stejného člověka. Oba režimy list/form fungují bez trvalého polling. Jsou doložené cílené testy a výkonnostní delta.

Následuje [manuální scénář](02_MANUAL_ACCEPTANCE.md) a [5032 stop-gate](../operations/01_EXECUTION_AND_5032.md). Tato hranice je závazná i tehdy, když má Codex ještě kapacitu implementovat další moduly.
