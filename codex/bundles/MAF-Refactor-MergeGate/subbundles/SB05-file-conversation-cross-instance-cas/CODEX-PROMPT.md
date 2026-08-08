You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB05 — File conversation cross-instance CAS` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Make file-store compare-and-swap true across all scoped instances sharing a root.

        Required work:

        1. Introduce a process-wide canonical-path keyed coordinator or equivalently tested lock-file design.
2. Protect Create, Replace, and Delete read-check-write sequences across separate instances.
3. Use reference-counted cleanup or another bounded lock lifecycle.
4. Preserve atomic replacement and remove temporary files in finally blocks.
5. Document whether guarantees are process-wide or cross-process and do not overclaim.

        Acceptance:

        - [ ] Two store instances racing one revision admit exactly one winner.
- [ ] Concurrent create admits one creator.
- [ ] Replace/delete race does not corrupt storage.
- [ ] Injected failure/cancellation leaves no temp file.
- [ ] Existing round-trip/corruption tests remain green.

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
