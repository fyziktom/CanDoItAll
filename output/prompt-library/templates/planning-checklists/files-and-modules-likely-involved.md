---
            key: files-and-modules-likely-involved
            id: 5a190018-e765-5ea4-9fd0-cee2166474a4
            name: Planning: Files and Modules Likely Involved
            group: planning-checklists
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: files, modules, planning
            promptTypes: plan, implementation, refactor, bugfix, migration
            blueprints: implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, embedded-firmware-iteration
            phases: planning
            stackTags: 
            templateTokens: 
            ---

            ## Files and Modules Likely Involved
For each planned step, name the files, directories, modules, or services most likely to be touched.

Avoid vague plans such as "update the backend" or "fix the UI".
The goal is to reduce rediscovery work for the implementer.
