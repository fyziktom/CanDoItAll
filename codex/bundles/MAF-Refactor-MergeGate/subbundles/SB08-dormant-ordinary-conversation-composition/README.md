# SB08 — Dormant ordinary-conversation production composition

        **Depends on:** SB07  
        **Required before merge:** Yes

        ## Goal

        Keep SB15 as a tested foundation but remove premature product registration.

        ## Required work

        1. Remove AddLlmConversations from AgentFrameworkModuleServiceCollectionExtensions.
2. Keep projects, contracts, implementations, solution entries, and tests.
3. Add isolated DI composition tests for the library.
4. Add a guard proving App/product modules do not consume or register ILlmConversationService.
5. Document future activation requirements including profile id/generation and switch fencing.
6. Do not implement a product surface.

        ## Primary files

        - `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationServiceCollectionExtensions.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/LlmConversationServiceTests.cs`

        ## Acceptance

        - [ ] No production module registers or injects ILlmConversationService.
- [ ] The foundation library still builds and composes in isolation.
- [ ] No agent or workflow path changes.
- [ ] Future activation contract is documented.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.
