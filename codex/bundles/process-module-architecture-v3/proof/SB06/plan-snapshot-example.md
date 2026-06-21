# SB06 Golden Plan Snapshot Example

```text
schema: template/1.0
definition: 4f56eddf-1057-4c7d-9211-2f960d82a874 / f04c8792-74ab-4e7c-b0a7-d3ed4d4c29fd
definitionHash: sha256:definition
driverStack: driver.generic@1.0.0
steps:
  - start: Start, not executable
  - activity: Activity, strategy.execute@1.0.0
  - branch: Branch, strategy.branch@1.0.0
  - end: End, not executable
artifacts:
  - slot.brief, required, local, initial ledger sha256:artifact
branches:
  - outcome.complete -> CompleteRun
  - outcome.repeat -> PreviousStep, loop budget 2, escalation Escalate
manager:
  - policy sha256:manager-policy
  - strategy.manager@1.0.0
monitoring: enabled, sha256:monitoring
security: sha256:security, approval.architect, approval.security
planHash: sha256:* stable across identical requests; changes when security policy changes
```
Evidence: `ProcessInstancePlanCompilerTests.Compile_creates_golden_immutable_plan` and `ProcessInstancePlanCompilerTests.Plan_hash_changes_when_security_policy_changes`.
