---
            key: stack-arduino-firmware
            id: 3278d0da-073b-51f1-bde1-3a6c73139e66
            name: Stack: Arduino Firmware
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: arduino, embedded, firmware, realtime
            promptTypes: embedded, implementation, bugfix, performance, validation
            blueprints: embedded-firmware-iteration, feature-implementation, validation-audit, performance-hardening
            phases: discovery, planning, implementation, verification
            stackTags: arduino, embedded
            templateTokens: 
            ---

            ## Arduino Firmware Guidance
For Arduino-class firmware work:
- treat memory, timing, and pin ownership as hard constraints,
- prefer deterministic state machines over implicit control flow,
- minimize heap churn in hot paths,
- make calibration, thresholds, and protocol behavior observable and testable.
