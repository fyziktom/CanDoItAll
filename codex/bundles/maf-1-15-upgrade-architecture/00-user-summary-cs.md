# Shrnutí pro vlastníka projektu

## Doporučení

Upgrade na MAF 1.15 je vhodný, ale neměl by být proveden jako prostá změna čísel NuGet balíčků. Největší riziko neleží ve FileTools, ale v kombinaci nového approval middleware, persistovaného stavu ze staré verze a vlastního handoff/streaming wrapperu.

## Co jsem potvrdila v aktuální větvi

- Hlavní MAF adapter odkazuje na stabilní balíčky 1.13.0, zatímco A2A používá samostatný preview build.
- CanDoItAll používá vlastní workspace file, command a artifact služby. Nejde o Harness `FileAccessProvider`.
- Runtime vytváří živé agenty, nástroje, context providery a handoff účastníky pro konkrétní execution a následně je uvolňuje. Tuto izolaci je nutné zachovat.
- Pending approvals se ukládají dvojím způsobem: jako živé MAF request objekty v procesové cache a jako vlastní serializovaný záznam pro restart.
- Při pokračování se z vlastního záznamu rekonstruuje `ToolApprovalRequestContent`, ale modelu se posílá pouze approval response.
- Handoff workflow je obalené vlastním depth guardem. Jeho non-streaming metoda ve skutečnosti spouští streaming a výslednou odpověď sama složí přes `ToAgentResponse()`.
- Obecný runtime také skládá finální odpověď ze streamovaných updates.

## Největší dopady 1.15

### 1. Approval binding

MAF 1.15 při použití standardní `ChatClientAgent` middleware pipeline ukládá model-originated approval request do `AgentSession.StateBag` a při pokračování sváže odpověď s přesným původním tool callem. Tím blokuje podvržené jméno nástroje, argumenty, cizí request ID i opakované použití stejného approval.

Session uložená pod 1.13 tento nový stav nemá. Přímé pokračování po deploymentu proto může být ignorováno. Bezpečné řešení je buď otevřené approval před upgradem vyčerpat/zrušit a znovu vystavit, nebo vytvořit krátkodobý, integritně chráněný migrační bridge. Binding se nesmí globálně vypnout.

### 2. Změna výchozího chování smíšených tool callů

Ve 1.13 bylo automatické obcházení approval requestů pro nástroje, které approval nepotřebují, opt-in. Ve 1.15 je zapnuté automaticky. To mění počet a skladbu `PendingApprovals`.

Doporučený postup:

1. při prvním compile/parity upgradu nastavit `DisableApprovalNotRequiredFunctionBypassing = true`;
2. stabilizovat binding a persistovaný stav;
3. teprve v samostatné fázi nové chování zapnout, upravit UI/API na rozhodnutí podle konkrétního approval ID a otestovat smíšené tool cally.

### 3. Handoff terminální výstup

MAF opravilo preferenci explicitního terminálního workflow outputu v non-streaming odpovědi. Váš `HandoffDepthGuardAgent.RunCoreAsync` však tuto cestu obchází a odpověď znovu skládá ze streamu. Stejný problém může zůstat i v hlavním runtime, který stream používá kvůli průběžné aktivitě.

Proto je nutné porovnat:

- přímé `workflowAgent.RunAsync`;
- přímé `workflowAgent.RunStreamingAsync`;
- odpověď přes depth guard;
- odpověď přes celý `MafAgentRuntime`;
- persistovanou historii po stejném běhu.

Až poté lze rozhodnout, zda přesunout depth limit na workflow/tool invokaci, přidat explicitní terminal-output projector, nebo oddělit activity stream od autoritativní non-streaming odpovědi.

### 4. FileTools

Harness změna `FileAccessStore` opt-in se na potvrzenou cestu CanDoItAll přímo nevztahuje. Vlastní file tools, workspace scope, external aliases, read-only pravidla, approval wrappery a script policy musí zůstat.

Codex musí pouze prokázat, že se nikde ve větvi nevytváří `HarnessAgent` nebo nepoužívá staré `DisableFileAccess`.

### 5. Sessions a checkpointy

Vlastní attachment scrubber, governed-step izolace a rozlišení provider/framework historie nejsou nahrazeny MAF 1.15. Opravy session deserializace a workflow type identity mohou odstranit některé problémy, ale musí se ověřit na 1.13 fixtures.

## Co bundle obsahuje

Bundle je připravený v angličtině pro Codex a obsahuje:

- osm přesně navazujících subbundles;
- fázi pro zachycení 1.13 session/checkpoint fixtures ještě před změnou balíčků;
- detailní approval threat model a cross-version migrační postup;
- analýzu handoff/streaming merge;
- regresní plán pro vlastní FileTools;
- přesné stabilní a preview verze balíčků;
- PowerShell a Bash discovery/validation skripty;
- JSON task graph a CSV impact matrix;
- rollout, rollback a telemetry plán;
- explicitní seznam workaroundů: zachovat, přepsat, odstranit až po důkazu.
