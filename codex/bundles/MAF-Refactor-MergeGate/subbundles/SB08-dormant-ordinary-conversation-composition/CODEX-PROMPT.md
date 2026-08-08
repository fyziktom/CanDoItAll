You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB08 — Dormant ordinary-conversation production composition` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Keep SB15 as a tested foundation but remove premature product registration.

        Required work:

        1. Remove AddLlmConversations from AgentFrameworkModuleServiceCollectionExtensions.
2. Keep projects, contracts, implementations, solution entries, and tests.
3. Add isolated DI composition tests for the library.
4. Add a guard proving App/product modules do not consume or register ILlmConversationService.
5. Document future activation requirements including profile id/generation and switch fencing.
6. Do not implement a product surface.

        Acceptance:

        - [ ] No production module registers or injects ILlmConversationService.
- [ ] The foundation library still builds and composes in isolation.
- [ ] No agent or workflow path changes.
- [ ] Future activation contract is documented.

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
