# 2026-04-29 Universal Process-Core Correction

## Raw Feedback

The previous repair direction was wrong because it moved calculator, Blazor, and .NET-specific recovery rules into the core process dispatch code. The core of process execution and agent cooperation must be universal enough to handle documents, spreadsheets, applications, and other deliverables.

Domain-specific guidance may live in agent instructions, task skills, tools, or tool descriptions. It must not live in process orchestration, retry routing, completion proof, or universal dispatch guards.

The final generated app must not be manually repaired as the process fix. If an app is broken, the governed agents must repair it through the process using the proper task skills and tools. The process must keep running or recover predictably when the goal is clear, instead of stopping after repeated failed attempts.

## Required Correction

- Find all calculator, Blazor, and .NET hardcoding in process-core code.
- Remove sample-specific repair logic from process dispatch, retry, and proof code.
- Keep technology-specific guidance in appropriate seeded agents, skills, or tool capabilities.
- Generalize reusable seeded examples so they do not bias unrelated runs toward the calculator sample.
- Validate with source scans, focused tests, build proof, and bundle closure proof.
