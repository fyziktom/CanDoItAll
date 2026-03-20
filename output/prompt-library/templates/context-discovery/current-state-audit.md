---
            key: current-state-audit
            id: 2bbf946e-ca78-5522-a682-34fe62cb3b12
            name: Context: Current State Audit
            group: context-discovery
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: audit, baseline, discovery
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, validation
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, validation-audit, ui-ux-delivery, embedded-firmware-iteration
            phases: discovery
            stackTags: 
            templateTokens: target_area
            ---

            ## Current State Audit
Start by auditing what already exists for {{target_area}}.

Confirm:
- what is already implemented,
- what is partial or inconsistent,
- what is missing,
- what existing tests or fixtures already cover.

Do not propose new architecture or new files before you know the real baseline.
