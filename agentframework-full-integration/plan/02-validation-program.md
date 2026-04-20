# 02 — Validation Program

## Validation Layers

- **Architecture guardrails**
  - no external project references,
  - no duplicate source-of-truth write path,
  - no direct messaging bypass.
- **Unit / component**
  - canvas link handling,
  - policy evaluation,
  - scoring,
  - mapping,
  - tabs and page state.
- **Integration**
  - provider bridge,
  - CRM-HR agent binding,
  - launch plan lifecycle,
  - approval return path,
  - outbox -> agent runtime -> artifact bridge.
- **Browser / visual**
  - shell tabs,
  - collaboration inbox,
  - CRM-HR/Agents deep links,
  - process launch UX,
  - process run transcript.
- **Scenario / workflow**
  - existing SC01–SC08,
  - new SC09–SC11,
  - explicit no-fake evidence requirement.
- **Closure review**
  - story coverage matrix,
  - raw note closure table,
  - QA/manager/architect sign-off.

## Evidence Recording Rules

- Každá subbundle zapisuje commands, screenshots, result a issues do `reviews/01-execution-report.md`.
- Browser analytics musí mít route, viewport, Playwright steps, screenshot path a stručný visual finding.
- Manuální scénáře musí mít explicitní důvod, proč zůstaly manuální, a co přesně bylo zkontrolováno.
