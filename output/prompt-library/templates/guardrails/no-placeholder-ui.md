---
            key: no-placeholder-ui
            id: d44f955c-d2b0-5809-b550-6d0d9fa7a042
            name: Guardrail: No Placeholder-Only UI
            group: guardrails
            blockKind: Constraint
            toolboxEligible: false
            recommended: false
            tags: quality-bar, real-behavior, ui
            promptTypes: ui, implementation, review
            blueprints: ui-ux-delivery, feature-implementation, validation-audit, senior-code-review
            phases: implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## No Placeholder-Only UI
Do not leave placeholder-only UI in the finished result.

New screens or controls must:
- bind to real state and real workflows,
- handle empty, loading, and error states where relevant,
- use the actual component system and styling approach in the repo.
