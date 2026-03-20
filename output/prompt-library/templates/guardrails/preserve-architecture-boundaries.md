---
            key: preserve-architecture-boundaries
            id: 55f8886d-3bf9-5fea-810e-2900b76c87ad
            name: Guardrail: Preserve Architecture Boundaries
            group: guardrails
            blockKind: Constraint
            toolboxEligible: false
            recommended: true
            tags: architecture, boundaries, maintainability
            promptTypes: architecture, implementation, refactor, review, security, migration
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, test-strategy-and-automation, validation-audit, performance-hardening, security-hardening, ui-ux-delivery, embedded-firmware-iteration
            phases: architecture, planning, implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Architecture Boundaries
Preserve the existing architectural boundaries unless the prompt explicitly authorizes a redesign.

Do not:
- move business logic into UI-only code,
- bypass shared contracts or data access patterns,
- introduce cross-module coupling just to finish faster.

If a boundary is wrong, document the issue and fix it deliberately rather than by accident.
