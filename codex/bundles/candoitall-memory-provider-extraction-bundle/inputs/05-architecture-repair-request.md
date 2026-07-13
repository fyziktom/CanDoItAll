# Memory Provider Architecture Repair Request

## Request date

- 2026-07-12

## Raw request preservation

The user reported that Cognitive Memory has moved to its own repository at `C:\repositories\CanDoItAll.CognitiveMemory`. The main `CanDoItAll` application now contains a Memory Providers module intended to connect agents to any external memory implementation. That implementation was created from this bundle, but the user does not accept its current architecture.

The user specifically identified excessive partial classes and missing separation of helpers and responsibilities into cohesive projects or, at minimum, folders and namespaces. The user asked for an architecture-led analysis and repair using the C# architecture skills, including `csharp-architecture-governor` and `csharp-modular-refactoring`, plus any other applicable C# boundary, provider-isolation, composition, dependency, and testability skills.

The user also asked for runtime validation of how agents consume memory. One agent must be able to use multiple configured memory providers. Provider selection must be available in agent settings. Agent memory invocation must support a typed automatic mode and a prompt-forced mode with an explicit directive such as `/mem:memory1`.

The requested outcome is not another planning-only pass. Analyze the live implementation in both repositories, repair and improve it, and prove the result with relevant builds, tests, dependency analysis, and end-to-end runtime evidence.

## Normalized repair objective

- Reopen the previously completed bundle because its historical closure evidence does not establish that the live implementation meets the requested architecture or runtime behavior.
- Preserve SB01-SB34 as historical implementation records; do not treat their old pass labels as proof for the current code.
- Establish and pass a C# architecture gate before changing production code.
- Replace capability-grouping partial classes with cohesive types owned by explicit project, folder, and namespace boundaries.
- Make agent memory configuration strongly typed, including invocation mode, one-to-many provider bindings, aliases, and explicit directive authorization.
- Make provider routing deterministic and fail closed: no implicit first-provider fallback and no cross-agent operation status or cancellation access.
- Propagate real agent, session, workspace, project, process, and workflow identity through the protocol instead of magic tags or discarded context.
- Preserve transport configuration safely, register supported drivers explicitly, and advertise only capabilities that work across the real provider seam.
- Secure and verify the external Cognitive Memory service with authentication, access policy, project isolation, and main-driver conformance tests.
- Complete the repair only after architecture, unit, integration, browser/runtime, and dependency-direction proof is current and reproducible.

## Superseding scope statement

The earlier `inputs/04-live-reentry-request.md` limited that re-entry to bundle preparation. This request explicitly authorizes production implementation and testing after the new SB35 architecture gate passes. It does not erase the prior request or historical SB01-SB34 records; it supersedes only their claim that the live implementation is complete and release-ready.
