---
            key: non-negotiable-rules
            id: 998d9e68-fa3d-5ee3-a0ca-7fffa850ca94
            name: Guardrail: Non-Negotiable Rules
            group: guardrails
            blockKind: Constraint
            toolboxEligible: false
            recommended: true
            tags: constraints, guardrails, hard-rules
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, test-strategy-and-automation, validation-audit, performance-hardening, security-hardening, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery, architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: rule_1, rule_2, rule_3, rule_4
            ---

            ## Non-Negotiable Rules
The following rules are mandatory:
- {{rule_1}}
- {{rule_2}}
- {{rule_3}}
- {{rule_4}}

If a rule conflicts with a proposed solution, change the solution instead of relaxing the rule silently.
