# CanDoItAll.Mcp.SshOps – Codex podkladový balík

Verze balíku: **1.1.0**  
Datum: **2026-03-20**

Tato revize nahrazuje původní čistě SSH-centric návrh tím, že před implementaci `CanDoItAll.Mcp.SshOps` vkládá **povinnou shared foundation fázi** pro celé MCP portfolio v solution `CanDoItAll`.

## Co se změnilo oproti verzi 1.0.0

Po analýze tří vstupů:

1. původního balíku `CanDoItAll.Mcp.DotNetWatch.CodexPack`,
2. původního balíku `CanDoItAll.Mcp.SshOps.CodexPack`,
3. aktuálního stavu solution `CanDoItAll`, kde už je `CanDoItAll.Mcp.DotNetWatch` ve vysokém stupni implementace,

je zřejmé, že další MCP server se nesmí stavět izolovaně.

`CanDoItAll.Mcp.DotNetWatch` už dnes obsahuje robustní MCP host, tool envelope, log buffer, log persistence, redaction, operation tracking, wait flow, security guardy, process supervision a stale process cleanup.  
SSH DevOps server by bez shared foundation velmi pravděpodobně znovu vytvářel stejné nebo velmi podobné stavební bloky.

Proto tato verze balíku zavádí **novou sekci `20-shared-foundation/`** a aktualizuje roadmapu, backlog, prompty, class mapu, project tree i QA gates.

## Nejkratší správná interpretace

**Nejdřív se má vytvořit shared MCP foundation.**  
**Pak se má do ní migrovat existující `CanDoItAll.Mcp.DotNetWatch`.**  
**Teprve potom se má implementovat `CanDoItAll.Mcp.SshOps`.**

To není kosmetika. Je to architektonický předpoklad, aby:

- se nesdílené věci nekopírovaly mezi servery,
- se neopakovala bezpečnostní logika a observability helpery,
- další budoucí MCP servery v solution nevznikaly jako izolované ostrovy,
- regresní riziko bylo odhalené už při refaktoru `CanDoItAll.Mcp.DotNetWatch`, ne až při třetím serveru.

## Povinné nové čtení

Nově je před implementací SSH serveru povinné projít i tuto sekci:

- `20-shared-foundation/00-overview.md`
- `20-shared-foundation/01-current-state-analysis.md`
- `20-shared-foundation/02-shared-library-catalog.md`
- `20-shared-foundation/03-extraction-matrix.csv`
- `20-shared-foundation/05-dotnetwatch-migration-plan.md`
- `20-shared-foundation/06-sshops-consumption-plan.md`
- `20-shared-foundation/09-prompts/*`
- `20-shared-foundation/10-checklists/*`

## Doporučené pořadí čtení

1. `01-executive-summary.md`
2. `20-shared-foundation/00-overview.md`
3. `20-shared-foundation/01-current-state-analysis.md`
4. `20-shared-foundation/02-shared-library-catalog.md`
5. `04-architecture.md`
6. `06-tool-contracts.md`
7. `09-implementation-roadmap.md`
8. `10-backlog.csv`
9. `11-validation-strategy.md`
10. `13-checklists/*`
11. `14-prompts/*`
12. `17-qa-review/*`
13. `19-technology-knowledge/*`

## Co je v této revizi už rozhodnuté

### A. Shared foundation se bude vytvářet jako samostatná MCP vrstva
Preferovaná struktura:

- `CanDoItAll.Mcp.Core`
- `CanDoItAll.Mcp.LocalRuntime`

`CanDoItAll.Mcp.SshOps` se bude opírat primárně o `CanDoItAll.Mcp.Core`.  
`CanDoItAll.Mcp.DotNetWatch` se bude opírat o `CanDoItAll.Mcp.Core` a `CanDoItAll.Mcp.LocalRuntime`.

### B. Nesmí vzniknout „God library“
Do shared vrstvy patří jen stabilní cross-server primitives:

- response envelope a common error model,
- correlation / operation / server instance identity helpery,
- mutation gate / resource lock,
- log buffer, log persistence, redaction,
- generické async operation primitives,
- common HTTP/TLS probe helpers,
- local process runtime pouze jako samostatná optional knihovna.

Naopak tam **teď** nepatří:

- `dotnet watch` doménová logika,
- test runner autodetection,
- SSH transport,
- host key verifier,
- Docker / Traefik / PostgreSQL / IPFS doménové služby.

### C. DotNetWatch je referenční donor
Shared foundation se má navrhovat z reálné implementace `CanDoItAll.Mcp.DotNetWatch`, ne z hypotetického čistého designu.

## Co má Codex dělat jako první

1. analyzovat aktuální `src/CanDoItAll.Mcp.DotNetWatch`,
2. potvrdit shared candidates podle `20-shared-foundation/03-extraction-matrix.csv`,
3. vytvořit nové shared projekty,
4. přesunout do nich pouze schválené common komponenty,
5. rozchodit a zregresnit `CanDoItAll.Mcp.DotNetWatch`,
6. až potom scaffoldovat `CanDoItAll.Mcp.SshOps`.

## Co je v balíku nově doplněné

- detailní analýza aktuálního `CanDoItAll.Mcp.DotNetWatch`,
- katalog sdílených knihoven a přesné boundary rules,
- extrakční matice typu → projekt → důvod → riziko,
- migrační plán pro `CanDoItAll.Mcp.DotNetWatch`,
- consumption plán pro `CanDoItAll.Mcp.SshOps`,
- nové prompty pro shared foundation a dotnetwatch refaktor,
- nové QA review dokumenty pro shared foundation gate.

## Praktický závěr

Tato revize už není jen „jak postavit SSH MCP server“.  
Je to plán, jak v `CanDoItAll` zavést **udržitelnou MCP platform layer**, na které bude stát SSH server i další budoucí MCP servery.
