---
            key: definition-of-done
            id: c2349bef-f2ae-5b8b-ab28-d666839b0087
            name: Planning: Definition of Done
            group: planning-checklists
            blockKind: Validation
            toolboxEligible: false
            recommended: true
            tags: acceptance, definition-of-done, quality-bar
            promptTypes: plan, architecture, validation, implementation
            blueprints: implementation-plan, architecture-spec, validation-audit, test-strategy-and-automation
            phases: planning, verification, delivery
            stackTags: 
            templateTokens: target_feature_or_change
            ---

            ## Definition of Done
Define what "done" means for {{target_feature_or_change}}.

Cover:
- functional correctness,
- testing and evidence,
- documentation or artifact updates,
- performance, accessibility, or security expectations where relevant,
- any environment-specific proof that must exist.
