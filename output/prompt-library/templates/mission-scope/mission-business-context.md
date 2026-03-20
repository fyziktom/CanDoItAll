---
            key: mission-business-context
            id: b6859cc5-ad37-56a1-9d41-3c55296bb270
            name: Mission: Business Context
            group: mission-scope
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: business-context, mission, prioritization
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery, architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: business_context
            ---

            ## Why This Matters
This work matters because {{business_context}}.

Optimize for the user or business outcome, not for elegant but irrelevant technical changes.
If two solutions are both correct, prefer the one that better supports {{business_context}}.
