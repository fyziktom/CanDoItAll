# České CTO shrnutí

Tento balíček doplňuje aktuální návrh Cognitive Memory o výslovnou vrstvu **Cognitive Self-Regulation**.

V aktuální architektuře už existují důležité části: attention router, workspace, metamemory answer gate, claim/evidence/belief ledger, prediction error, salience, score geometry a probing calibration. Tyto části jsou správné, ale self-regulation je v nich zatím rozprostřená. Chybí samostatný self-model a orchestrátor, který bude rozhodovat, kdy si systém může věřit, kdy má pochybovat, kdy má odpověď jen označit jako hypotézu, kdy se má ptát, kdy má spustit probing, kdy má eskalovat na větší LLM jako „profesora“ a kdy má raději abstain.

Balíček definuje „ego“ systému technicky jako:

```text
calibrated agency under epistemic uncertainty
```

Tedy ne vědomí ani osobnost, ale schopnost stabilně jednat podle role, zkušeností, důkazů, rizika a vlastní historické kalibrace.

Hlavní doplněné oblasti:

- Cognitive Self-Model,
- Domain Competence Profiles,
- Known Failure Patterns,
- Self-Regulation Assessment,
- Humility Trigger Engine,
- Answer Posture Decision,
- Calibration Health Aggregates,
- Professor Review Escalation,
- post-outcome feedback loop.

Součástí je i upozornění pro Codex, aby před rozšířením contractů provedl běžný audit konzistence enumů, referencí mezi contracty, score-space hodnot a návazností na existující neuro patch contracty. Balíček nepředpokládá konkrétní chybu, pokud ji aktuální zdroj nepotvrdí.
