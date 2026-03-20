---
            key: file-touch-plan
            id: 34c9c7c3-ac54-5a85-8c1a-0895f242506a
            name: Context: File Touch Plan
            group: context-discovery
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: change-scope, file-plan, impact-analysis
            promptTypes: architecture, plan, implementation, refactor, bugfix, review
            blueprints: architecture-spec, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review
            phases: discovery, planning
            stackTags: 
            templateTokens: likely_file_or_module_1, likely_file_or_module_2, likely_file_or_module_3
            ---

            ## File Touch Plan
Before editing, identify the files or modules most likely to change:
- {{likely_file_or_module_1}}
- {{likely_file_or_module_2}}
- {{likely_file_or_module_3}}

Call out any high-risk files where careful review is required because regressions would be expensive.
