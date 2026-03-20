---
            key: environment-and-commands
            id: 9bbecb42-f575-53c2-b910-a8230a594e07
            name: Context: Environment and Commands
            group: context-discovery
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: commands, environment, verification
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery, implementation, verification
            stackTags: 
            templateTokens: build_command, integration_test_command, run_command, ui_test_command, unit_test_command
            ---

            ## Local Build and Verification Commands
Use and update the real commands for this workspace:
- build: {{build_command}}
- unit tests: {{unit_test_command}}
- integration tests: {{integration_test_command}}
- UI tests: {{ui_test_command}}
- run app or service: {{run_command}}

If these commands are wrong for the current workspace, correct them early and keep the corrected commands visible in the session.
