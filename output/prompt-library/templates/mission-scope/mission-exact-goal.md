---
            key: mission-exact-goal
            id: 67c3d515-1309-582f-bdf4-602c3527be0f
            name: Mission: Exact Goal
            group: mission-scope
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: goal, mission, scope
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery, architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: exact_goal
            ---

            ## Mission
Your exact goal is to {{exact_goal}}.

Treat this as the primary objective for the session.
Do not drift into adjacent improvements unless they are required to make {{exact_goal}} work correctly.
If you discover a prerequisite, state it briefly and complete it before returning to the main objective.
