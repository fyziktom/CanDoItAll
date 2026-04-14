# Staffing summary prompt

**Key:** `prompt-staffing-summary`  
**Scope:** local  
**Process:** customer-onboarding  
**Audience role key:** `staffing-manager`  
**Phase:** Staffing review

## Summary
Produces a structured staffing readiness note from the onboarding brief and current capacity picture.

## Required inputs
- Customer onboarding brief.
- Current capacity or named specialists.
- Known constraints or target dates.

## Output schema
- Named roles and specialists.
- Coverage gaps.
- Kickoff recommendation.

## Refusal conditions
- Refuse if critical ownership or target date information is missing.
- Do not fabricate specialist availability.
