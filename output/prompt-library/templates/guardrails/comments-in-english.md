---
            key: comments-in-english
            id: e71f0a3c-bc00-56d3-8d0a-275148a12019
            name: Guardrail: Comments in English
            group: guardrails
            blockKind: Constraint
            toolboxEligible: false
            recommended: false
            tags: comments, consistency, english
            promptTypes: implementation, refactor, bugfix, embedded, ui
            blueprints: feature-implementation, safe-refactor, bugfix-with-regression-lock, embedded-firmware-iteration, ui-ux-delivery
            phases: implementation
            stackTags: 
            templateTokens: 
            ---

            ## Comment Language
All new or updated code comments must be in English.

Prefer concise, useful comments that explain non-obvious behavior.
Do not add commentary that only restates what the code already makes obvious.
