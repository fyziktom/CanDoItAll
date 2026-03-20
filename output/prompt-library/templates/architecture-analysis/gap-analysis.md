---
            key: gap-analysis
            id: 49c35a4c-3f03-5a13-85ee-d2eeaedac4b2
            name: Architecture: Gap Analysis
            group: architecture-analysis
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: audit, gap-analysis, planning
            promptTypes: architecture, audit, plan, review, migration
            blueprints: architecture-spec, repository-audit, implementation-plan, validation-audit
            phases: discovery, architecture, planning
            stackTags: 
            templateTokens: target_feature_or_system
            ---

            ## Gap Analysis
Compare the current implementation with the target outcome for {{target_feature_or_system}}.

Identify:
- what is already done,
- what is partial or inconsistent,
- what is missing entirely,
- what creates the largest delivery or quality risk.

Focus on actionable gaps rather than vague observations.
