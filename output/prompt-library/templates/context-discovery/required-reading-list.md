---
            key: required-reading-list
            id: a6a608c1-d9d9-559d-b759-cb928dfe55f5
            name: Context: Required Reading List
            group: context-discovery
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: artifacts, inputs, required-reading
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery
            stackTags: 
            templateTokens: input_path_1, input_path_2, input_path_3, input_path_4
            ---

            ## Inputs to Read
Read the following before you plan or implement:
- {{input_path_1}}
- {{input_path_2}}
- {{input_path_3}}
- {{input_path_4}}

Treat these inputs as authoritative unless the codebase proves they are outdated.
