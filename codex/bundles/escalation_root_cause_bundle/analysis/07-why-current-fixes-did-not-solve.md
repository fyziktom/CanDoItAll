# 07 – Proč aktuální Codex změny problém ještě nevyřešily

Z aktuálního kódu je vidět, že předchozí změny posílily několik věcí správným směrem:

- product completion gates umí odhalit prázdnou solution,
- template/contributor generuje helper script a deterministic execution plan,
- required tool receipts obsahují `workspace_pwsh_run_script`,
- prompt obsahuje explicitní current-run receipt pravidla,
- runtime-owned subprocess guidance existuje,
- preflight umí ověřit existenci required tool names.

Tyto změny ale opravují převážně detekci a instrukce. Neopravují recovery a deterministické provedení.

## Co fungovalo

Runtime správně odmítl nepravdivé `Completed`. To je důležité. Kdyby product gate neexistovala, proces by pokračoval s prázdnou solution a chyba by se projevila později mnohem hůř.

Konkrétně zafungovalo:

```text
process.adapter.product_required_file_content_missing
```

## Co nefungovalo

### 1. Detekce se nepřeměnila na opravu

Diagnostika byla safe/idempotent, ale runtime ji routeoval do manager escalation.

### 2. Agent dostal plán jen jako text

Plan byl v promptu a launch variables, ale agent ho nedodržel. Přidat další text by bylo málo.

### 3. Missing helper receipt se schoval za první file-content failure

Protože validace short-circuituje, receipt packet neměl kompletní obraz.

### 4. Placeholder path zůstala unresolved

Tests i runtime stále tolerují `artifacts/process-runs/{CurrentProcessRunId}/scripts/...` jako tool path instrukci.

### 5. Parent neukázal child root cause

Parent `prepare-solution-skeleton` je runtime-owned subprocess step. Přesto operator message vedla uživatele k tomu, že „No AgentFramework result summary“ je problém, místo aby přímo zobrazila child diagnostic.

### 6. Rework byl obecný

Assignment repair neviděl důvod přepnout agenta, protože agent měl obecná práva. Rework ale nedostal přesnou instrukci „run missing helper script and read back solution membership“.

## Důsledek

Systém je nyní v mezistavu:

- umí chytit nevalidní výstup,
- neumí jej bezpečně a cíleně opravit,
- neumí dobře vysvětlit child příčinu parent kroku,
- stále předává některé deterministické povinnosti LLM promptem.

Proto i jednoduchý calculator process padá do eskalace.
