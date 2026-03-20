---
            key: rollback-and-recovery-plan
            id: c2f937fc-c72d-5364-ae73-78fede0ff23a
            name: Planning: Rollback and Recovery Plan
            group: planning-checklists
            blockKind: Validation
            toolboxEligible: false
            recommended: false
            tags: recovery, risk, rollback
            promptTypes: plan, implementation, migration, performance, security
            blueprints: implementation-plan, feature-implementation, security-hardening, performance-hardening, embedded-firmware-iteration
            phases: planning, verification, delivery
            stackTags: 
            templateTokens: target_feature_or_change
            ---

            ## Rollback and Recovery
Define the rollback or recovery path if {{target_feature_or_change}} fails in validation or rollout.

Include:
- what can be reverted safely,
- what data or schema risk exists,
- what fallback behavior should remain available,
- what evidence would trigger rollback.
