# Subbundle 00 — Mandatory Reopen And Proof Discipline

## Objective

Zastavit falešný completion claim, vrátit initiative do pravdivého stavu a udělat proof auditovatelný.

## Why this subbundle is mandatory

Aktuální repo obsahuje contradiction:

- někdo tvrdí, že je hotovo,
- ale execution report v repu říká, že je hotových jen 01–03 a 04–12 zůstávají pending.

Než začne další feature work, musí se tenhle rozpor odstranit.

## Inputs

- `agentframework-full-integration/reviews/01-execution-report.md`
- `src/CanDoItAll.Modules.AgentFramework/*`
- audit tohoto follow-up bundle

## Tasks

1. **Reopen truthfully**
   - upravit `agentframework-full-integration/README.md`
   - upravit `agentframework-full-integration/reviews/01-execution-report.md`
   - explicitně zapsat, že initiative je reopennutá po auditu a completion claim byl předčasný

2. **Repair evidence discipline**
   - pokud jsou screenshot proof artifacts skutečně k dispozici, commitnout je do `agentframework-full-integration/reviews/artifacts/`
   - pokud nejsou k dispozici, odstranit tvrzení, která je předpokládají
   - ke každému browser proof přidat markdown log se steps, observed result, timestamp, route a viewportem

3. **Add reproducible proof surfaces**
   - založit nebo doplnit automatizované Playwright tests pro každý critical route/flow, který se bude používat jako closure evidence
   - evidence nesmí být jen „provedla jsem MCP session“

4. **Add hard fail closure script**
   - přidat script do `codex/scripts/` nebo podobného místa
   - script musí failnout, pokud najde `Pending implementation`, `To be filled`, placeholder copy nebo chybějící artifacts pro claimed completed subbundles

## Acceptance

- stav initiative v dokumentaci je pravdivý,
- screenshoty a logs jsou buď opravdu přítomné, nebo nejsou tvrzené,
- existuje reprodukovatelný proof discipline pro zbytek integrace.

## Do not continue if

- execution report zůstane ve lži nebo neurčitosti,
- artifacts nejsou auditovatelné,
- closure script neexistuje.
