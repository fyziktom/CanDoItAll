---
            key: repo-map-confirmation
            id: f00f62d2-ae55-5a6b-b802-dc79777a1df7
            name: Context: Repository Map Confirmation
            group: context-discovery
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: context, paths, repository-map
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery
            stackTags: 
            templateTokens: docs_or_artifact_paths, primary_projects_or_modules, solution_or_workspace_root, tests_and_validation_projects
            ---

            ## Repository Map
Confirm the repository structure before making changes.

At minimum, verify:
- {{solution_or_workspace_root}}
- {{primary_projects_or_modules}}
- {{tests_and_validation_projects}}
- {{docs_or_artifact_paths}}

If the working tree differs from the prompt, resolve the mismatch explicitly instead of ignoring it.
