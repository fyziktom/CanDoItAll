---
            key: stack-blazor-webapp
            id: 66474013-fdc5-52bf-9d30-a21e945e790d
            name: Stack: Blazor
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: blazor, components, js-interop
            promptTypes: architecture, implementation, refactor, bugfix, ui, testing, validation
            blueprints: architecture-spec, feature-implementation, safe-refactor, bugfix-with-regression-lock, ui-ux-delivery, validation-audit
            phases: architecture, planning, implementation, verification
            stackTags: blazor, .net
            templateTokens: 
            ---

            ## Blazor Guidance
For Blazor work:
- keep business logic out of page-only code,
- respect render mode boundaries and lifecycle realities,
- keep component state, services, and JS interop responsibilities explicit,
- preserve the existing component system and routing conventions.

If a behavior depends on browser state or JS interop, document the contract on both sides of the boundary.
