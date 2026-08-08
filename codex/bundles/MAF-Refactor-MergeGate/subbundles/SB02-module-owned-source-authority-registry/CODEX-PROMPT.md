You are Codex GPT-5.6 Sol with xHigh reasoning, acting as a senior C#/.NET architect.

        Execute `SB02 — Module-owned source authority registry` on branch `maf-refactor`.

        Read the bundle root, this README, relevant architecture documents, current callers through
        CodeAnalysis MCP, and the installed C#/.NET architecture skills.

        Goal:

        Turn the source authority SPI into a real DI registry owned by source-publishing modules.

        Required work:

        1. Change the resolver dependency to IEnumerable<IAgentExecutionSourceAuthorityProvider> or an equivalent DI-friendly collection.
2. Remove CreateDefaultProviders and all hard-coded construction from CanonicalAgentExecutionAuthorityResolver.
3. Move Project Structure authority to the module that publishes project-structure context.
4. Move projects portfolio authority to the owning Projects/Workbench integration identified by CodeAnalysis.
5. Move processes/processes-live authority to Modules.Processes.
6. Register providers with TryAddEnumerable and retain duplicate source-key fail-fast validation.
7. Keep unknown source behavior fail-closed and behaviorally unchanged.
8. Add dependency guards so Modules.AgentFramework cannot regain source-kind-specific product/process implementations.

        Acceptance:

        - [ ] Each source authority implementation lives with its owning module.
- [ ] Resolver consumes DI-provided providers and constructs none.
- [ ] Duplicate keys fail deterministically.
- [ ] Missing provider plus workspace claim fails closed.
- [ ] No project/process behavior regression.

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
