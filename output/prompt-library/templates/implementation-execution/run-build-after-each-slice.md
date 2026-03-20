---
            key: run-build-after-each-slice
            id: 81805d81-39fa-582b-af27-09073d7eb484
            name: Implementation: Run Build After Each Slice
            group: implementation-execution
            blockKind: Testing
            toolboxEligible: false
            recommended: true
            tags: build, checkpoints, implementation
            promptTypes: implementation, refactor, bugfix, migration, embedded
            blueprints: feature-implementation, safe-refactor, bugfix-with-regression-lock, embedded-firmware-iteration
            phases: implementation, verification
            stackTags: 
            templateTokens: 
            ---

            ## Build Checkpoints
After each meaningful implementation slice:
- run the fastest relevant build or compile command,
- fix breakages before moving on,
- keep the next slice starting from a working baseline.
