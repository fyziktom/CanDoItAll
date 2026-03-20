---
            key: dependency-inventory
            id: 7092f49d-41dc-58a5-9695-10894cc84bbc
            name: Context: Dependency Inventory
            group: context-discovery
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: dependencies, integration, stack
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery, planning
            stackTags: 
            templateTokens: 
            ---

            ## Dependency Inventory
List the important dependencies that shape this work:
- frameworks and runtime versions,
- external services or protocols,
- build, test, and deployment dependencies,
- hardware or browser capabilities if relevant.

If a dependency version or capability is uncertain, verify it before designing around it.
