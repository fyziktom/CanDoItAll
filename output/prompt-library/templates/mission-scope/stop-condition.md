---
            key: stop-condition
            id: bafa66a9-ba1a-5bea-8c0b-d951ec6425cb
            name: Stop Condition
            group: mission-scope
            blockKind: Constraint
            toolboxEligible: false
            recommended: false
            tags: handoff, phase-gate, stop-condition
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: stop_condition
            ---

            ## Stop Condition
Stop when {{stop_condition}}.

Do not silently continue into the next milestone once this point is reached.
When you stop, summarize what is complete, what remains, and the recommended next prompt or next agent.
