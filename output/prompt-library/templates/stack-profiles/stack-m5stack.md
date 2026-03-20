---
            key: stack-m5stack
            id: 9b38e985-cc1d-538a-b810-6411c7a981bf
            name: Stack: M5Stack
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: false
            tags: embedded, hardware, m5stack
            promptTypes: embedded, implementation, bugfix, review, validation
            blueprints: embedded-firmware-iteration, validation-audit, feature-implementation
            phases: discovery, planning, implementation, verification
            stackTags: m5stack, embedded
            templateTokens: 
            ---

            ## M5Stack Guidance
For M5Stack or M5Stick-class work:
- verify the exact board model and pin assignments,
- be deliberate about PMU, battery telemetry, and peripheral initialization,
- do not repurpose reserved pins without proving the impact,
- connect firmware behavior to any host-side telemetry or UI surfaces that depend on it.
