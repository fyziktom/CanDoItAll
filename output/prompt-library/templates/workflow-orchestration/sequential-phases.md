---
            key: sequential-phases
            id: f1519441-ed97-54f9-863a-aff95c4a3e1a
            name: Workflow: Sequential Phases
            group: workflow-orchestration
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: phases, sequencing, workflow
            promptTypes: architecture, audit, plan, implementation, refactor, bugfix, review, testing, validation, performance, security, migration, embedded, ui
            blueprints: architecture-spec, repository-audit, implementation-plan, feature-implementation, safe-refactor, validation-audit, embedded-firmware-iteration
            phases: discovery, architecture, planning, implementation, verification, delivery
            stackTags: 
            templateTokens: 
            ---

            ## Phase Order
Execute this work in order:
1. discovery and audit
2. architecture or plan
3. implementation
4. verification and review
5. delivery or handoff

Do not collapse all phases into a single response if phase-specific proof is required.
