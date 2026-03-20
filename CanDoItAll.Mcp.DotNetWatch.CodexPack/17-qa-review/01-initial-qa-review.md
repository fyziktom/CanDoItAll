# Initial QA review

## Role and posture

Tento review je psaný z pohledu přísné QA senior manažerky, která má rozhodnout, zda je balík dostatečný jako zadání pro implementaci.

## Executive verdict

**Verdict: Not approved yet for execution without remediation.**

Původní balík byl silný v těchto oblastech:
- dobře popsaná orchestrace session a operací,
- správně rozpoznaný problém s build/test kolizemi,
- rozumný tool contract,
- dobrý důraz na wait a log cursory.

Ale před schválením chybělo nebo bylo nedostatečně rozpracované několik kritických oblastí.

## Kritické mezery

### 1. Chyběl explicitní threat model
Bez threat modelu by mohl implementátor podcenit:
- path traversal,
- command injection,
- nechtěné ukončení cizích procesů,
- SSRF-like zneužití health probe,
- únik tajných dat do logů.

**Severity:** High

### 2. Chyběla observability a redaction politika
Bylo popsáno, že logy mají být korelované a redigované, ale chyběla:
- konkrétní redaction pravidla,
- retenční politika,
- požadavky na audit trail cleanup akcí.

**Severity:** High

### 3. Chyběl failure injection plan
Bez něj hrozí, že se otestují jen happy path scénáře a ne:
- neočekávané exity,
- stuck health,
- port conflicts,
- stale procesy,
- watch restart edge cases.

**Severity:** High

### 4. Chyběl compatibility matrix
Procesní chování a file watcher nuance se liší na:
- Windows,
- Linux,
- macOS,
- WSL,
- kontejnerech.

Bez compatibility matrix by mohly vzniknout nerealistické předpoklady.

**Severity:** Medium-High

### 5. Chyběl ops runbook
Pro lokální používání serveru nestačí architektura.  
Je potřeba:
- troubleshooting postup,
- log locations,
- cleanup flow,
- recovery flow po pádu.

**Severity:** Medium

### 6. Nebyl explicitní risk register / open questions list
Chyběl seznam neuzavřených předpokladů proti skutečnému repozitáři:
- startup project path,
- health endpoint,
- package management model,
- browser tooling choice.

**Severity:** Medium

## Podmínky schválení

Před finálním schválením požaduji doplnit:

1. threat model
2. observability + redaction plan
3. failure injection plan
4. compatibility matrix
5. ops runbook
6. risk register a open questions
7. checklist, který potvrzuje, že tyto položky byly opravdu doplněny

## Požadovaný QA standard

Finální balík musí:
- být použitelný nejen jako architektura, ale i jako provozní a validační podklad,
- mít jasnou hranici bezpečnosti,
- pokrývat recovery a troubleshooting,
- umět odlišit blocker od follow-upu.

## Remediation status
Viz:
- `02-remediation-checklist.md`
- `03-remediation-summary.md`
