# CanDoItAll Agents UI Component Seams — shrnutí pro vlastníka

**Reference bundle:** `CDA-UI-SEAMS-AGENTS-01-v1`  
**Sdílený základ:** `CDA-UI-SEAMS-BASE-v1`  
**Cílová větev:** `components-decoupling`  
**Pozorovaný HEAD:** `c225bf2445835bf12fa5054bc15571d2ce23b4fe`

## Co tento bundle řeší

Jde o první implementační řez programu rozplétání komponent. Zaměřuje se pouze na:

- `AgentsHomePage`;
- `AgentCatalogPanel`;
- `AgentDetailsDialog`;
- jejich bezprostřední stavové, aplikační a testovací hranice.

Komponenty zůstávají na současném místě v `CanDoItAll.Modules.AgentFramework`. Bundle
nevytváří nový `.UI` projekt, sandbox ani nové routy. Neřeší ani provider panel, workflow,
Manager nebo `dotnet watch`.

## Hlavní architektonická změna

Po dokončení má platit:

```text
AgentsHomePage
  vlastní typovaný navigační/workspace stav
  vlastní route-significant výběr a otevření detailu
  používá jeden overview query a jeden catalog controller

AgentCatalogPanel
  pouze vykresluje předaný stav
  emituje typované intenty
  lokálně drží jen search a rozbalení stromu

AgentDetailsDialog
  používá stabilní AgentDetailsSection
  dostává explicitní AgentEditorSession
  veškeré externí load/save/delete operace vede přes IAgentEditorController
```

Tím se odstraní přímé EF dotazy z Razor page, skryté otevírání dialogů z katalogu a
přímé napojení editoru na Workspace, providery, Projects, Secrets a infrastrukturu.

## Proč nejsou přidány další wrappery

Bundle výslovně zakazuje vytvořit `Container -> View -> Presenter -> Service` pyramidu.
Stávající `AgentCatalogPanel` se použije jako controlled view; nevytváří se kolem něj
nová Razor komponenta. Povolené jsou jen tři skutečné workflow hranice:

1. `IAgentsOverviewQuery`;
2. `IAgentCatalogController`;
3. `IAgentEditorController`.

Další produkční interface vyžaduje explicitní architektonické zdůvodnění a schválení.
Čisté mapování, normalizace a redukce stavu mají být obyčejné top-level typy bez DI.

## Testovací dluh v rozsahu bundle

Cílové component testy mají nyní 46 případů:

- `AgentsHomePageTests`: 6;
- `AgentCatalogPanelTests`: 10;
- šest tříd `AgentDetailsDialog*Tests`: 30.

Behaviorální scénáře jsou převážně cenné, ale část harnessů používá reflexi privátních
polí/metod, testovací dědění a `RuntimeHelpers.GetUninitializedObject`. Bundle zachovává
behaviorální krytí, ale přepisuje harness přes veřejný state/controller seam. Nevytváří
nové testy na počet souborů, názvy privátních metod ani jiné náhodné detaily implementace.

Do úklidu patří také případ ve `WorkflowsPageTests`, který reflektuje privátní
`AgentsHomePage.OpenWorkflows`; nahradí se kliknutím na veřejný UI prvek a ověřením URL.

## Rozdělení práce

1. zmrazení aktuálního baseline a discovery counts;
2. typovaný Agents workspace state a overview query;
3. controlled hranice Agent katalogu;
4. stabilní section/session hranice agent editoru;
5. přesun editorových command/I/O operací do controlleru;
6. přepis testů bez privátní reflexe a neinitializovaných služeb;
7. finální focused, stable, portability, architecture a browser closure.

Kroky nejsou bezpečné pro paralelní provádění, protože se postupně dotýkají stejných
Razor a testovacích souborů.

## Co se nesmí změnit

- existující `/agents` URL a query parametry;
- pořadí, názvy a chování hlavních tabů a deseti sekcí agent detailu;
- deep-link otevření konkrétního agenta;
- create/edit/delete a confirmation semantics;
- capability, project, secret, storage, thinking-effort, auto-approval a avatar chování;
- živé sibling reference na Components a FileTools;
- vizuální design a obecné CSS;
- umístění komponent a projektové reference.

## Očekávaný přínos

Tento bundle sám ještě dramaticky nezmenší MSBuild graf, protože komponenty zůstávají ve
stávajícím AgentFramework RCL. Vytvoří ale první ověřenou hranici, díky které bude možné
v dalším kroku přesunout feature UI do menšího projektu a spustit nad ním lehký sandbox
bez plného runtime hosta.
