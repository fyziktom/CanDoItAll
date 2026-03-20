---
            key: stack-midi-audio
            id: 25d66d79-46d5-5dae-b03f-f4d37880506e
            name: Stack: MIDI and Audio
            group: stack-profiles
            blockKind: Instruction
            toolboxEligible: false
            recommended: true
            tags: audio, midi, realtime, timing
            promptTypes: embedded, implementation, bugfix, performance, validation, ui
            blueprints: embedded-firmware-iteration, feature-implementation, bugfix-with-regression-lock, validation-audit, performance-hardening
            phases: architecture, planning, implementation, verification
            stackTags: midi, audio
            templateTokens: 
            ---

            ## MIDI and Audio Guidance
For MIDI or audio work:
- make time bases explicit,
- protect event ordering and timing accuracy,
- use fixtures or captured traces when live hardware input is unavailable,
- avoid hand-wavy assumptions around quantization, buffering, or scheduling.
