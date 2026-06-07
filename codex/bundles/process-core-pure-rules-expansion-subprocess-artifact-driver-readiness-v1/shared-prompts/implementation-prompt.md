# Implementation Prompt

You are implementing `process-core-pure-rules-expansion-subprocess-artifact-driver-readiness-v1`.

Rules:
- Move only pure deterministic rules/read models into `CanDoItAll.Processes.Core`.
- Keep EF, workspace, storage, filesystem, claims, transitions, AgentFramework, finalizer application, process mutation, and driver runtime out of Core.
- Do not create production driver APIs.
- Do not change UI.
- Do not collapse subbundle rows.
- Prove behavior with build, unit, focused integration, source scans, and completed validator.
