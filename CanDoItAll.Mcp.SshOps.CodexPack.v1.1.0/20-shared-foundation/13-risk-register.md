# Shared foundation risk register

| ID | Riziko | Dopad | Pravděpodobnost | Mitigace |
|---|---|---:|---:|---|
| SF-R1 | Přesun envelope rozbije dotnetwatch response shape | vysoký | střední | contract snapshoty před/po |
| SF-R2 | Přesun log/redaction helperů změní log behavior | vysoký | střední | regression log smoke scénáře |
| SF-R3 | Přesun process supervision rozbije stale cleanup | vysoký | střední | LocalRuntime tests + dry-run cleanup |
| SF-R4 | Shared layer začne tahat doménovou logiku | vysoký | střední | ADR-012 + dependency review |
| SF-R5 | SSH implementace si přesto vytvoří lokální kopie helperů | střední | střední | readiness checklist + PR review |
| SF-R6 | Refaktor dotnetwatch bude příliš široký | vysoký | střední | postup po malých PR, bez mixování se SSH implementací |
