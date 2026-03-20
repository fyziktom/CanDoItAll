---
            key: branch-and-status-tracking
            id: 8af48794-afd8-5f7b-8206-a5ec2c2c4bfa
            name: Workflow: Branch and Status Tracking
            group: workflow-orchestration
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: branch, git, status, workflow
            promptTypes: implementation, refactor, bugfix, review, migration, embedded
            blueprints: feature-implementation, safe-refactor, bugfix-with-regression-lock, senior-code-review, embedded-firmware-iteration
            phases: discovery, planning, implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Branch and Status Tracking
Keep branch and session status explicit:
- current branch or branch plan,
- current milestone or phase,
- whether the working tree is clean or contains relevant in-flight changes.

If the work depends on dirty state, describe how you will avoid clobbering unrelated changes.
