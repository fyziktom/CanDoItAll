# QA evidence checklist

**Key:** `qa-evidence-checklist`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `qa-lead`  
**Phase:** verification

## Summary
Risk-based evidence completeness checklist for QA sign-off or recommendation.

## Entry criteria
Implementation evidence is available and QA review is underway.

## Exit criteria
Evidence pack is decision-ready with open defects and residual risk clearly stated.

## Checks
- Covered behaviors and uncovered behaviors are both explicit.
- QA directly inspected the inherited implementation artifact paths before making the quality disposition.
- QA recommendation cites the exact implementation artifacts, build outputs, screenshots, or test records it accepted or rejected.
- Automated evidence is reproducible and linked to the reviewed build or artifact set.
- Manual or exploratory findings are captured with severity and follow-up.
- Environment limitations affecting confidence are disclosed.
- Residual quality risk is written in go/no-go language.
- Defects are triaged, not just listed.

## Evidence expectations
- Test evidence pack.
- Defect triage note.
- QA recommendation or sign-off statement.
