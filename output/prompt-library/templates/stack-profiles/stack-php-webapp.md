---
            key: stack-php-webapp
            id: 150cb427-a26c-56b9-bf96-8e7e5ff964c8
            name: Stack: PHP Web App
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: legacy-modernization, php, webapp
            promptTypes: architecture, implementation, refactor, bugfix, migration, ui
            blueprints: architecture-spec, feature-implementation, safe-refactor, bugfix-with-regression-lock, ui-ux-delivery
            phases: discovery, planning, implementation, verification
            stackTags: php
            templateTokens: 
            ---

            ## PHP Web App Guidance
For PHP-based web apps:
- inspect the real runtime structure before assuming framework boundaries,
- preserve working server-side rendering, routing, and data flow unless the prompt calls for migration,
- keep new JavaScript and CSS changes compatible with the existing PHP entry points,
- prefer incremental modernization over hidden framework rewrites.
