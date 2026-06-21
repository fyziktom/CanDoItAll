# Run final security review and go/no-go approval

**Process:** `release-readiness-and-deployment` / Release readiness and deployment control  
**Step key:** `security-and-go-no-go`  
**Step kind:** Approval  
**Target lead hours:** 2

## Summary
The last gate decides under explicit conditions

## Notes
Combine the readiness package, cutover rehearsal, rollback proof, and open exceptions into the final release decision.

## Contracts
- Input contract: Readiness package, rehearsal output, rollback plan, and any exception requests.
- Output contract: Approved or held release decision with explicit conditions.
- Evidence contract: Release approval record and exception note if applicable.

## Governance
- Decision rights: Release approver owns the final decision; security reviewer may hold the release for unresolved control gaps.
- Exception policy: No release may proceed on informal approval.
- Requires approval: True
- Requires decision record: True

## Dependencies
- cutover-rehearsal

## Role assignments
- `release-approver` / Release approver => Approver; required=True; fallback-order=0; rebind=Final go/no-go authority remains with the release approver.
- `security-reviewer` / Security reviewer => Reviewer; required=True; fallback-order=0; rebind=Security review remains explicit.
- `service-owner` / Service owner => Reviewer; required=True; fallback-order=1; rebind=Operational risk owner reviews live conditions.

## Artifact expectations
- `security-and-go-no-go-security-review-note` -> `security-review-note` / Security review note | kind= | trust= | sensitivity= | validation=Must state reviewed scope, identified risks, required controls, and explicit approval or exception status.
- `security-and-go-no-go-release-readiness-report` -> `release-readiness-report` / Final go/no-go decision | kind=Decision | trust=HumanApproved | sensitivity= | validation=Must identify unresolved conditions, rollback posture, support coverage, and explicit decision recommendation.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- `go` / Go — Release is approved under the recorded conditions.
- `hold` / Hold — Release is held pending gap closure or explicit exception resolution.

## Checklists
- `security-review-checklist`
- `release-go-live-checklist`

## Validations
- `validation-security-clear`
- `validation-release-authorized`
- `validation-rollback-ready`

## Prompts
- `prompt-release-decision`
- `prompt-security-review`
