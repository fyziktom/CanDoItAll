# Final Done Checklist

Až budou všechny položky splněné, teprve pak je dovolené říct „hotovo“.

## Delivery

- [ ] Subbundles 01–12 jsou skutečně closed
- [ ] AgentFramework source je lokálně integrovaný do CanDoItAll
- [ ] Provider ownership má jediného canonical ownera
- [ ] CRM-HR má binding na technickou agent doménu
- [ ] Process launch flow je staged a auditovatelný
- [ ] Default HR a Main Manager fallback fungují bez AI
- [ ] Project-specific manager / human substitution funguje
- [ ] Agent execution běží přes selected resources v process runu
- [ ] `/agents` je reálný modul s tabs, ne placeholder
- [ ] ScenarioHarness / scenarios jsou přenesené a běží přes integrovaný flow

## QA and proof

- [ ] Reálné browser artifacts jsou v repu
- [ ] Playwright proof je reprodukovatelný
- [ ] Scenario proof je reprodukovatelný
- [ ] Negative paths jsou otestované
- [ ] Known bug `Unknown role` je opravený nebo formálně schválený jako residual risk

## Truthfulness

- [ ] `Execution state: Completed` odpovídá skutečnosti
- [ ] V repu už nejsou `Pending implementation`
- [ ] V repu už nejsou `To be filled`
- [ ] V `/agents` už nejsou placeholder texts
- [ ] Nikdo netvrdí completion bez auditovatelných důkazů
