---
            key: honest-blocker-reporting
            id: da026203-117c-564c-9f92-9f33a7b7b02a
            name: Guardrail: Honest Blocker Reporting
            group: guardrails
            blockKind: Delivery
            toolboxEligible: false
            recommended: true
            tags: blockers, honesty, verification
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, test-strategy-and-automation, validation-audit, performance-hardening, security-hardening, ui-ux-delivery, embedded-firmware-iteration
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Blocker Reporting
If you are blocked by the environment, missing hardware, unavailable credentials, or failing infrastructure:
- say exactly what is blocked,
- describe what you verified before concluding it is blocked,
- give the closest fallback or next action.

Do not imply that blocked verification passed.
