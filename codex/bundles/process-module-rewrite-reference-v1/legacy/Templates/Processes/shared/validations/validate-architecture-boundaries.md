# Architecture boundaries explicit

**Key:** `validate-architecture-boundaries`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `solution-architect`  
**Gate:** Architecture review  
**Failure severity:** Error

## Summary
Prevents implementation from starting while service boundaries, source of truth, or migration ownership remain ambiguous.

## Pass criteria
Boundaries, source of truth, migration owner, and rejected alternatives are explicit.

## Fail criteria
Boundary ownership, canonical source, or migration responsibility is still vague.

## Escalation rule
Stop implementation planning and escalate through architecture governance.
