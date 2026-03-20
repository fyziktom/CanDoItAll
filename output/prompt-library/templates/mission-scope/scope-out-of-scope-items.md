---
            key: scope-out-of-scope-items
            id: 4e43011f-af01-5e2b-9055-5a248d2ce6d5
            name: Scope: Out-of-Scope Items
            group: mission-scope
            blockKind: Constraint
            toolboxEligible: false
            recommended: true
            tags: focus, out-of-scope, scope
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery, architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: out_of_scope_item_1, out_of_scope_item_2, out_of_scope_item_3
            ---

            ## Out of Scope
Do not spend time on the following unless they become mandatory blockers:
- {{out_of_scope_item_1}}
- {{out_of_scope_item_2}}
- {{out_of_scope_item_3}}

If you encounter one of these areas, acknowledge it and return to the main objective.
