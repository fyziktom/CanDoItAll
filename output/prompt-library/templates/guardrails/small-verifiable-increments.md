---
            key: small-verifiable-increments
            id: bf5d9a9b-13a5-5778-8c65-cbf9cb8c505d
            name: Guardrail: Small Verifiable Increments
            group: guardrails
            blockKind: Constraint
            toolboxEligible: false
            recommended: true
            tags: increments, risk-reduction, verification
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, test-strategy-and-automation, validation-audit, performance-hardening, security-hardening, ui-ux-delivery, embedded-firmware-iteration
            phases: planning, implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Increment Size
Work in small, verifiable increments.

For each slice:
- change only the minimum coherent surface,
- run the closest relevant verification,
- keep the system buildable and testable before moving on.

If the work starts turning into a large refactor, split it into smaller steps with explicit checkpoints.
