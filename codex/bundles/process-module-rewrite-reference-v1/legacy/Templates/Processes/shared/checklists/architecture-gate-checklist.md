# Architecture gate checklist

**Key:** `architecture-gate-checklist`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `solution-architect`  
**Phase:** architecture

## Summary
Proportional architecture review gate before irreversible implementation starts.

## Entry criteria
A change affects system boundaries, integration contracts, or other design-sensitive seams.

## Exit criteria
Architecture impact is either explicitly accepted as low-risk or captured in a decision record with follow-up actions.

## Checks
- Impacted components, boundaries, and ownership areas are identified.
- Alternative approaches were considered to the extent justified by risk.
- Integration, operability, and observability consequences were reviewed.
- Irreversible decisions are documented in an ADR.
- Known technical debt or exception implications are made explicit.
- Appropriate domain owners or specialists participated where needed.

## Evidence expectations
- Architecture decision record or low-risk rationale note.
- Updated implementation plan reflecting approved design direction.
