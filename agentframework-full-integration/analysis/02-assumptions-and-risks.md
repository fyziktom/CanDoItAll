# 02 — Assumptions And Risks

## Working Assumptions

- Uživatelka chce po integraci **jedno** CanDoItAll repo bez live compile-time závislosti na externím AgentFramework solution.
- Nové moduly `Collaboration` a `AgentFramework` jsou přijatelné, i když původní formulace mluví primárně o „novém modulu“, protože Collaboration je podle zadání nutné dodat ještě před samotnou integrací agentů.
- Process module může získat nové persistentní entity pro launch/staffing orchestration a messaging policy, aniž by to narušilo jeho roli canonical ownera procesní definice a runtime.
- Stávající `StaffingRequest` v CRM-HR zůstane podpůrný demand model; nebude z něj udělaný vlastník process launch flow.
- Workspace provider master data a Security secret management zůstanou po migraci dostupné, takže není nutné vynalézat druhý secret store uvnitř AgentFrameworku.
- Přijatelný je staged migration přístup, kdy se legacy pole/taby nejprve zmrazí a až následně fyzicky odstraní v cleanup subbundle.
- User story validation se bere jako closure gate, ne jako dokumentační příloha; pokud některý flow neprojde v UI, implementace není hotová.

## Critical Path Risks

- **Split source of truth risk:** Pokud executor ponechá editable provider/runtime/agent data současně ve Workspace, CRM-HR i AgentFrameworku, integrace se rychle rozpadne.
- **God-service risk:** Integrace snadno sklouzne do jednoho obřího service nebo page code-behind souboru. Bundle proto vyžaduje refactor-first rule a menší focused services.
- **Process bypass risk:** Agenti mohou začít komunikovat mimo process messaging policy, pokud nebude komunikace hard-gated jedinou službou a jediným canonical storem.
- **Premature run-start risk:** Když se `StartRunAsync` jen rozšíří drobnými ify místo zavedení explicitního launch planu, vznikne křehký runtime bez auditovatelných staffing decisions.
- **Workspace leakage risk:** Zachování jednoho sandbox root adresáře by míchalo testy, project contexty a process runs.
- **UI duplication risk:** Když se sandbox pages jen „přilepí“ vedle existujících CRM-HR a Settings surfaces, uživatelka dostane dvě cesty ke stejným datům.
- **Migration drag risk:** Backfill provider/agent/binding dat bez kill-switchů a evidence může znečitelnit, odkud se které pole ještě smí číst a kam už se smí zapisovat.
- **Scenario dishonesty risk:** Bez explicitních proof pravidel může executor nasimulovat stav databáze a vydávat to za skutečný end-to-end process run.

## Validation Risks

- Playwright proof může být slabý, pokud se bude testovat jen „page loads“ bez screenshot review, unread badges, routing a transcript detailů.
- Scenario proof může být neúplný, pokud se neuloží artifacts, approvals a run events do execution reportu.
- Story coverage může být zdánlivá, pokud se user story přiřadí modulu, ale neexistuje konkrétní UI flow a route, kde ji lze dokončit.
- Build-only validation nestačí, protože nejrizikovější část integrace je behaviorální: messaging policy, staffing approval chain a artifact governance.
- Ruční lokální ověřování bez DB/runtime evidence je nedostatečné pro auditovatelné closure.
- Manuální scénáře `SC07` a `SC08` musí mít explicitní reason, proč zůstávají manuální; jinak hrozí, že se prostě neprovedou.

## Reopen Triggers

- Objeví se druhý editable source of truth pro provider, agent, notification nebo process messaging policy.
- Jakákoli subbundle zavádí přímou agent-to-agent komunikaci bez centralizované policy/authorizer služby.
- Přibude soubor nebo service, který nekontrolovatelně míchá persistence, orchestration, UI mapping a business rules najednou.
- `StartRunAsync` dál vytváří `Active` run bez explicitního launch plan/approval kroku.
- CRM-HR stránka zapisuje technical agent pole přímo do CRM-HR modelu bez facade/bridge do AgentFrameworku.
- Settings a Agents UI oba dál umožňují aktivně spravovat stejné provider runtime údaje.
- Scenario validation používá ručně seeded assignments nebo obchází process launch UI.
- Browser proof chybí pro subbundle, která mění shell, tabs, inbox nebo process canvas.
