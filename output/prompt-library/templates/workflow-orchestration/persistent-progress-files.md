---
            key: persistent-progress-files
            id: 86034ca5-549f-54e8-a9cb-7984d895cd1e
            name: Workflow: Persistent Progress Files
            group: workflow-orchestration
            blockKind: Delivery
            toolboxEligible: false
            recommended: false
            tags: continuity, handoff, progress
            promptTypes: implementation, refactor, bugfix, migration, embedded
            blueprints: feature-implementation, safe-refactor, bugfix-with-regression-lock, embedded-firmware-iteration, validation-audit
            phases: planning, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Persistent Progress
Keep persistent progress artifacts in-repo so later sessions can resume cleanly.

Maintain:
- a status file,
- a short decisions log,
- a known-gaps or postponed-items log,
- a next-prompt or next-step pointer.
