# 04 – Analýza kombinací template + agent + tools

Cílem není jen opravit `.NET solution setup`. Stejný pattern se může projevit v libovolném větším procesu: agent dostane zjednodušený nebo prose-only template, přeskočí konkrétní tool receipt, vrátí `Completed`, runtime to odmítne a recovery vše pošle na managera.

## Kombinace z incidentu

### Parent step

- Process: multi-team software delivery / `.NET implementation slice`
- Step: `prepare-solution-skeleton`
- Charakter: subprocess orchestration
- Launch mode: runtime-owned child subprocess
- Expected child: `dotnet-solution-setup`
- Parent direct MAF run: žádný pro přesný parent step

Parent step je tedy správně bez přímého AgentFramework execution runu. UI a operator packet s tím ale neumí dobře pracovat a hlásí „No AgentFramework result summary“.

### Child step

- Process: `.NET solution setup subprocess`
- Step: `create-dotnet-project`
- Step kind: `Work`
- Role key: `software-engineer`
- Executor: `.NET Application Developer`
- Allowed operations:
  - `MutateProductTarget`,
  - `ReadProcessContext`,
  - `ReadProjectStructure`,
  - `ReadUpstreamArtifacts`,
  - `WriteManagedProcessArtifacts`.
- Target scope: `ExternalProductTargetMutable`
- Required product tool receipts:
  - `template=sln`,
  - `template=blazorwasm`,
  - `workspace_pwsh_run_script`.

### Chování

Agent měl právo mutovat product target a dostupné tooly pro scaffolding. Skutečné selhání nebyla absence obecných práv, ale chybné provedení plánu: přeskočení helper scriptu a nepravdivé tvrzení o solution membership.

## Proč je `.NET Application Developer` nevhodný primární executor pro tento typ kroku

`.NET Application Developer` je vhodný pro vývoj source code a opravy aplikace. `create-dotnet-project` je ale ve skutečnosti „scaffold/wire/verify executor“ s deterministickým checklistem a product side effects.

U takových kroků je rizikové spoléhat na LLM:

- jde o pořadí tool calls,
- každý tool call má přesné argumenty,
- readback je povinný,
- evidence musí být current-run receipt,
- chyby jsou idempotentně opravitelné.

To má dělat runtime plan executor nebo úzký deterministic tool-plan executor, ne obecný software engineer prompt.

## Varianty kombinací a jejich rizika

| Kombinace | Typické riziko | Doporučení |
|---|---|---|
| Runtime-owned subprocess + parent bez MAF runu | UI hledá parent AgentFramework result a hlásí, že žádný není | Parent projection musí ukazovat child diagnostic packet |
| Work step + obecný software engineer + product mutation | Agent přeskočí povinný tool/readback a vrátí Completed | Typed plan + completion gate aggregate + auto-rework |
| Work step + unresolved tool path launch variable | Agent použije placeholder doslova nebo helper přeskočí | Resolve placeholdery před promptem, fail template pokud zůstanou |
| Required tool receipts jen v promptu | Agent považuje text/evidence ref za receipt | Runtime matcher musí vyžadovat current-run tool receipt |
| Child artifact fyzicky existuje, ale runtime jej nepřijal | Parent bridge může přijmout falešné evidence | Bridge přes ledger/slot, ne přes `StatPath` |
| Safe/idempotent diagnostic + generic recovery | Opravitelná chyba eskaluje | SafeRetry/CurrentStepRetry + diagnostic repair packet |
| Missing/denied capability | Rework opakuje stejný agent | Capability-aware assignment repair nebo template repair |
| Branch/validation step | Agent vrátí Blocked místo branch outcome | Branch outcome guard: decision steps completed with branch key |
| Build/test validation | Agent cituje starý log nebo upstream text | Required current-run validation receipts + timestamp/run id |
| Browser/visual QA | Agent vyhodnotí text summary místo asset/screenshotu | Visual proof tool plan + screenshot/readback contract |

## Doporučená klasifikace kroků

Codex má zavést explicitní step execution class, která je nezávislá na `StepKind`:

1. `DeterministicToolPlan` – runtime/tool executor, žádný LLM pro samotné side effects.
2. `AgentWithToolPlanGuard` – LLM smí rozhodovat, ale required receipts/readbacks hlídá plan guard.
3. `AgentReasoningOnly` – analýza, plánování, architektura; žádná product mutation.
4. `BranchDecision` – LLM/validator vybere branch, ale musí doložit current-run evidence.
5. `RuntimeOwnedSubprocess` – parent step řídí runtime, parent agent neřeší child tools.
6. `ExternalApproval` – lidský schvalovací krok.

`create-dotnet-project` patří do `DeterministicToolPlan` nebo minimálně `AgentWithToolPlanGuard`.

## Doporučená role/capability metadata

U agentů nestačí obecný popis. Pro readiness a assignment repair přidejte capability keys:

```json
{
  "capabilities": [
    "dotnet.scaffold.solution",
    "dotnet.scaffold.blazorwasm",
    "dotnet.wire.solution-membership",
    "workspace.script.write-managed-helper",
    "workspace.script.run-pwsh-product-mutation",
    "process.managed-artifact.write",
    "product.readback.verify-file-content"
  ]
}
```

U template step plan pak explicitně vyžadujte:

```json
{
  "requiredCapabilities": [
    "dotnet.scaffold.solution",
    "dotnet.wire.solution-membership",
    "workspace.script.run-pwsh-product-mutation"
  ]
}
```

Assignment repair má poté umět říct:

- agent má tool name, ale nemá step capability,
- tool provider existuje, ale konkrétní path je mimo scope,
- template vyžaduje tool plan, ale step nemá plan executor.

## Co neopravovat jen promptem

Nezvětšovat dál generický prompt v naději, že agent pravidlo nepřehlédne. V incidentu už prompt obsahoval:

- required tool receipt rule,
- current-run helper script ordering rule,
- deterministic execution plan,
- explicitní zákaz psát primary artifact před `workspace_pwsh_run_script`.

Agent to přesto přeskočil. Další prose instrukce má být až doplněk ke structured enforcementu, ne hlavní fix.

## Template schema požadavky

Codex má doplnit nebo připravit migraci template schema tak, aby step mohl nést machine-readable contract:

```json
{
  "executionClass": "AgentWithToolPlanGuard",
  "toolPlan": {
    "planKey": "dotnet.create-project",
    "items": [
      {
        "itemKey": "create-solution",
        "tool": "workspace_dotnet_new",
        "requiredReceipt": "template=sln",
        "idempotent": true
      },
      {
        "itemKey": "create-app",
        "tool": "workspace_dotnet_new",
        "requiredReceipt": "template=blazorwasm",
        "idempotent": true
      },
      {
        "itemKey": "wire-solution-membership",
        "tool": "workspace_pwsh_run_script",
        "scriptRefVariable": "DotNetCreateProjectScriptRef",
        "scriptContentVariable": "DotNetCreateProjectScript",
        "sideEffectManifestVariable": "DotNetCreateProjectSideEffectManifest",
        "idempotent": true
      }
    ]
  },
  "productReadbackGates": [
    {
      "pathCandidatesVariable": "DotNetSolutionFileCandidates",
      "containsAny": [
        "src/Calculator/Calculator.csproj",
        "src\\Calculator\\Calculator.csproj"
      ]
    }
  ]
}
```

Pro začátek nemusí být schema takto finální, ale Codex má přestat generovat jen prose notes.

## Parent/child template contract

Hardcoded mapping v resolveru je dočasně použitelný, ale do budoucna špatný zdroj pravdy. Template parent step má nést:

```json
{
  "subprocessContract": {
    "launchMode": "RuntimeOwned",
    "definitionKey": "dotnet-solution-setup",
    "acceptedChildOutputs": [
      { "stepKey": "setup-handoff", "artifactSlotKey": "setup-handoff" },
      { "stepKey": "setup-handoff-after-repair", "artifactSlotKey": "setup-handoff" }
    ],
    "noGoChildOutputs": [
      { "stepKey": "setup-repair-escalation", "artifactSlotKey": "setup-repair-escalation" }
    ]
  }
}
```

Runtime template validation má ověřit, že child definition tyto step keys a artifact slots skutečně obsahuje.
