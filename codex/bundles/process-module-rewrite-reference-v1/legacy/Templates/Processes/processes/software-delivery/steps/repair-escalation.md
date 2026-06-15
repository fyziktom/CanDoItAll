# Escalate unresolved repair findings

Capture unresolved post-repair findings and make the delivery no-go decision explicit instead of silently closing the process.

## Contract
- Inputs: Post-repair QA escalation, repair notes, and remaining release-blocking evidence.
- Outputs: Explicit no-go, scope reset, or replan decision with accountable owner.
- Evidence: Escalation decision, unresolved defect list, required next repair scope, and owner.
- Operation target scope: `ExternalProductTargetReadOnly`
