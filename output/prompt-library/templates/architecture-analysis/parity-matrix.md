---
            key: parity-matrix
            id: e496be3c-fd6b-524f-bcc8-ee3ada56a2ab
            name: Architecture: Parity Matrix
            group: architecture-analysis
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: mapping, migration, parity
            promptTypes: architecture, audit, plan, migration
            blueprints: architecture-spec, repository-audit, implementation-plan
            phases: discovery, architecture, planning
            stackTags: 
            templateTokens: source_system, target_system
            ---

            ## Parity Matrix
Build a parity matrix for {{source_system}} to {{target_system}}.

For each in-scope page, route, module, or workflow, map:
- the current implementation owner,
- the target implementation owner,
- reusable assets or contracts,
- missing work,
- required tests.
