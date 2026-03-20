---
            key: feature-flag-rollout
            id: 7dc2bc48-d79e-5c1b-9156-be7b45952f20
            name: Implementation: Feature Flag Rollout
            group: implementation-execution
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: feature-flag, risk, rollout
            promptTypes: implementation, migration, performance, security, embedded
            blueprints: feature-implementation, performance-hardening, security-hardening, embedded-firmware-iteration
            phases: implementation, verification, delivery
            stackTags: 
            templateTokens: target_change
            ---

            ## Feature Flag or Staged Rollout
If {{target_change}} is risky, introduce it behind a feature flag, configuration switch, or staged default.

Prefer:
- additive wiring first,
- test coverage on the new path,
- flipping defaults only after validation.
