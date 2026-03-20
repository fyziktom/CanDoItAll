---
            key: observability-validation-pass
            id: c96c62ed-d8c5-5c05-b9c1-256eefe30514
            name: Validation: Observability Validation Pass
            group: validation-review
            blockKind: Validation
            toolboxEligible: false
            recommended: false
            tags: diagnostics, logging, observability
            promptTypes: implementation, validation, performance, security, embedded
            blueprints: feature-implementation, validation-audit, performance-hardening, security-hardening, embedded-firmware-iteration
            phases: verification, delivery
            stackTags: 
            templateTokens: target_change
            ---

            ## Observability Validation
Confirm that {{target_change}} is observable enough to debug and support.

Cover:
- logs or traces for failure paths,
- debug surfaces or diagnostics if needed,
- redaction of sensitive values,
- proof that the new behavior can be inspected when it fails.
