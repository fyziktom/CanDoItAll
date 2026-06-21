# Validate SBOM and provenance readiness

**Key:** `validation-sbom-ready`  
**Scope:** shared  
**Process:** shared  
**Owner role key:** `compliance-steward`  
**Gate:** oss-intake  
**Failure severity:** Error

## Summary
Confirms component metadata and provenance are sufficient for governed reuse and later response.

## Pass criteria
Component identity, source, and major gaps are explicit enough for compliance and vulnerability response.

## Fail criteria
Component metadata is materially incomplete or provenance is missing without declared restriction.

## Escalation rule
Escalate to compliance steward and supply-chain owner; restrict reuse until corrected.
