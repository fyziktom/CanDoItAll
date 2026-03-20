---
            key: evidence-output-required
            id: 6aa6264a-bd57-5832-870a-157894c73d41
            name: Validation: Evidence Output Required
            group: validation-review
            blockKind: Delivery
            toolboxEligible: false
            recommended: true
            tags: commands, evidence, proof
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: validation-audit, feature-implementation, safe-refactor, bugfix-with-regression-lock, test-strategy-and-automation, embedded-firmware-iteration
            phases: verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Evidence Output
In the final response, include the evidence that supports the result:
- exact commands executed,
- test or build outcome summary,
- screenshots, traces, or logs if relevant,
- what could not be verified in this environment.
