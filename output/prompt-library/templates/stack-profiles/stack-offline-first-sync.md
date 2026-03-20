---
            key: stack-offline-first-sync
            id: 73de5d03-d635-5bee-a7cc-b1b99711fe12
            name: Stack: Offline-First Sync
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: local-state, offline-first, sync
            promptTypes: architecture, plan, implementation, migration, validation, ui
            blueprints: architecture-spec, implementation-plan, feature-implementation, validation-audit, ui-ux-delivery
            phases: architecture, planning, implementation, verification
            stackTags: offline-first, sync
            templateTokens: 
            ---

            ## Offline-First Guidance
For offline-first or sync-heavy work:
- keep local state ownership explicit,
- model outbox, retries, idempotency, and conflict handling deliberately,
- make online and offline states visible in the UX,
- prove behavior across reconnect and partial failure paths.
