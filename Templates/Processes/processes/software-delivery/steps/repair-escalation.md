# Escalate unresolved repair findings

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `repair-escalation`  
**Kind:** Approval

## Purpose
Record a no-go or replan decision when the modeled repair pass does not resolve release-blocking findings.

## Inputs
- Post-repair QA evidence.
- Quality repair change set.
- Remaining defect or proof-gap list.

## Outputs
- Repair escalation record with owner, no-go rationale, and required next repair scope.

## Dependencies
- `qa-recheck:repair-escalation`
- `quality-repair`

## Governance
Do not continue to security or release approval while quality remains unresolved. This step exists to keep the process explicit instead of silently stopping or marking unresolved delivery as complete.
