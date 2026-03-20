---
            key: safe-ambiguity-handling
            id: f63817c2-d85d-5867-aaf7-c341cebdbdc7
            name: Guardrail: Safe Ambiguity Handling
            group: guardrails
            blockKind: Constraint
            toolboxEligible: false
            recommended: true
            tags: ambiguity, assumptions, safety
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, test-strategy-and-automation, validation-audit, performance-hardening, security-hardening, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery, architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Ambiguity Handling
When the prompt or repository is ambiguous:
- prefer the simplest behavior that stays consistent with the existing system,
- document the assumption you chose,
- add tests or notes that lock the decision in.

Do not invent hidden requirements or silently make risky assumptions.
