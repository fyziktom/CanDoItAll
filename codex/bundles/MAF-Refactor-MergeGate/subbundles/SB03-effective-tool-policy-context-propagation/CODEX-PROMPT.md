You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB03 — Effective tool-policy context propagation` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Return and use the exact contributor-enriched policy context together with its decision.

        Required work:

        1. Introduce a pipeline result containing EffectiveContext and Decision.
2. Use EffectiveContext for block guard, recoverable denial mapping, telemetry, logging, approval-path checks, and diagnostics.
3. Remove or isolate the contributor-bypassing IAgentToolInvocationPolicy implementation from the pipeline.
4. Replace ReferenceEquals contributor detection with explicit process enrichment validation against audit identity.
5. Require exact process run/step identity and required restriction fields for governed process evaluation.
6. Add end-to-end policy tests proving a process denial becomes the intended recoverable result.

        Acceptance:

        - [ ] Downstream policy handling observes process run/step and process restrictions.
- [ ] Governed recoverable denials remain recoverable.
- [ ] An unrelated cloning contributor cannot satisfy the process contributor requirement.
- [ ] The MAF adapter remains process-semantic-free.
- [ ] Existing interactive and process tool-policy tests remain green.

        Constraints:

        - Add a failing characterization test before production changes.
        - Preserve completed MAF boundaries.
        - Make the smallest cohesive owner-boundary fix.
        - Keep source comments in English.
        - Do not add ordinary-chat product features.
        - Do not weaken security, process, approval, workspace, or regression tests.
        - Stop on a failed gate.
        - Run focused tests, neighboring tests, Release build, and relevant guards.
        - Write proof and session handoff before closure.
