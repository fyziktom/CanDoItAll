# Validate AI evidence sufficiency

**Key:** `validation-ai-evidence-sufficient`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `ai-evaluation-lead`  
**Gate:** ai-governance  
**Failure severity:** Error

## Summary
Confirms AI-related evaluation evidence is good enough for the intended operating boundary.

## Pass criteria
Evaluation scope, benchmark logic, failure modes, and control assumptions are explicit and decision-grade.

## Fail criteria
Evidence is narrow, cherry-picked, or silent about important failure modes and operational limits.

## Escalation rule
Escalate to model risk approver and AI safety reviewer; do not delegate release acceptance.
