# Codex workflow plan

Níže je navržené pořadí práce. Nedoporučuji začít úpravou template textu, protože ta pouze zmírní symptomy. Nejprve je potřeba opravit contract/evaluator/routing.

## Phase 0: Baseline and incident regression fixture

- Importovat tento incident jako synthetic test data bez LLM.
- Vytvořit test fixture pro `qa-validation` se čtyřmi attempt scénáři.
- Nejprve testy označit jako failing/skip s jasným TODO, aby bylo vidět cílové chování.

Acceptance:

- Failing tests reprodukují `quality-accepted + scaffold content => repair-required branch route`.
- Failing tests reprodukují `repair-required + deterministic defect + missing acceptance-only browser receipts => Succeeded + repair branch signal`.

## Phase 1: Extract completion gate evaluator

- Extrahovat pure services z partial adapteru.
- Nezměnit behavior kromě přidání trace.
- Adapter má jen MAF execution, output parsing, managed artifact materialization a převod evaluation resultu na strategy result.

Acceptance:

- Existing tests stále prochází.
- Nové unit tests mohou volat evaluator bez MAF runtime.

## Phase 2: Branch-aware receipt rule model

- Přidat structured receipt rules s backwards compatibility.
- Parser musí podporovat:
  - legacy newline/string array,
  - JSON string array,
  - JSON object array,
  - by-step map s object arrays.
- `ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts` musí číst tool names i z object rules.
- `ProcessLaunchApplicationService` nesmí zahazovat object rules.

Acceptance:

- DotNet contributor tests ověří branch applicability.
- Legacy tests se string arrays pořád prochází.

## Phase 3: Apply branch-aware receipt enforcement and deduplication

- Product receipt gate vynutí jen rules applicable pro aktuální `BranchOutcomeKey`.
- Capability/process receipt gate buď dostane branch-aware filtering, nebo se QA browser receipts odstraní z capability scope a zůstanou jako product completion rules.
- Deduplikovat diagnostics, aby stejný missing tool nevznikal jako product i process issue, pokud reprezentuje stejný requirement.

Acceptance:

- `repair-required` na QA nevyžaduje browser acceptance proof, pokud existuje deterministic defect evidence.
- `quality-accepted` dál vyžaduje browser proof.

## Phase 4: Branch-routable completion issues

- Přidat `ProcessCompletionIssueRouter`.
- Product content/readback failure při acceptance branch se routuje na template-defined repair branch.
- Retry budget se nesmí spotřebovat branch-routable issue.

Acceptance:

- Attempt 3 z incidentu vede do `repair-required`, ne `SafeRetry`.
- Attempt 4 z incidentu vede do `repair-required`, ne `ManagerRequired`.

## Phase 5: Move domain recovery advice out of generic application

- Zavést provider model pro recovery advice.
- Přesunout .NET/software-delivery hardcoded části z `ProcessStepRecoveryInstructionBuilder`.
- Generic builder nesmí obsahovat konkrétní .NET tool names ani QA branch names.

Acceptance:

- Architecture test zakáže `.NET`, `Blazor`, `Tetris`, `qa-validation`, `repair-required` v generic runtime/application files mimo povolené adapters/providers/templates.

## Phase 6: Harden software-delivery templates

- Přidat branch evidence matrix do `qa-validation.md` a `qa-recheck.md`.
- Opravit prompt builder wording: missing tool/environment failure => `Blocked`; repair branch jen po product defect evidence.
- `quality-repair.md` musí pracovat s runtime gate findings.

Acceptance:

- Agent prompt jasně odlišuje QA omission vs product defect.
- Recovery prompt vychází z applicable branch-aware rules, ne z hardcoded receipt listu.

## Phase 7: Acceptance criteria matrix from project structure

- Feature intake nebo architecture review musí vytvořit `acceptance-criteria-matrix` artifact.
- Implementation musí mapovat změny/testy/proof na criteria ids.
- QA musí ověřit criteria ids, ne pouze shell screenshot.

Acceptance:

- Tetris-like project structure by odhalila chybějící game loop, keyboard input, IndexedDB score, next-piece UI jako repair defects.
- Calculator-like project structure zůstane jednoduchá a projde bez zbytečné complexity.

## Phase 8: .NET runtime lifecycle hardening

- Prověřit `workspace_dotnet_run/stop` ownership, startup receipt, port cleanup a orphan process handling.
- Přidat idempotent cleanup nástroj nebo explicitní orphan cleanup režim.
- Nepřidávat doménovou logiku do process core.

Acceptance:

- Druhý QA/recheck step nemůže selhat jen proto, že předchozí execution zanechala běžící proces bez srozumitelné evidence.

## Phase 9: Observability and operator UX

- V diagnostics zobrazovat applicable/skipped gates.
- UI má říct: „routed to repair because acceptance content gate failed“, ne jen „No AgentFramework result summary“ nebo generic `NeedsManager`.

Acceptance:

- Operator bez čtení raw MAF logů vidí přesný gate, branch a missing/applicable rules.
