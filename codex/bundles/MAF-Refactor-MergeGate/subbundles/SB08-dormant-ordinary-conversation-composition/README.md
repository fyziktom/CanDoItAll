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

        - [x] No production module registers or injects ILlmConversationService.
- [x] The foundation library still builds and composes in isolation.
- [x] No agent or workflow path changes.
- [x] Future activation contract is documented.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.

## Execution contract

- **Owned finding:** MRG-010.
- **Proof tier:** Behavioral.
- **Progression gate:** SB09 unlocks only when no production module registers/consumes the service and isolated library composition remains valid.
- **Reopen trigger:** Any production resolution path exists, a fallback activation is introduced, or library composition/tests are deleted or weakened.

## C# Architecture Impact

Remove premature application composition while preserving the independently testable foundation.

## Boundary Ownership

Llm.Conversations owns opt-in registration; product composition remains dormant until a future profile-fenced product owner exists.

## Dependency Direction

Removing the Modules.AgentFramework-to-Conversations activation must not add any reverse dependency or alter agent/workflow paths.

## Pattern Decision

Use explicit non-registration plus architecture guards; reject a default/fallback profile resolver.

## Testability Contract

Compose the library explicitly in isolation and scan/inspect production module constructors and registrations for absence.

## Partial Class Policy

No partials or placeholder product surface.

## Architecture Proof Required

Positive isolated DI composition, adversarial production registration/consumer guard, source assertions, and documented future activation contract.

## Gate result

- **Status:** Complete
- **Decision:** Pass
- **Evidence:** `proof-manifest.json`, `SESSION-HANDOFF.md`, and `../../proof/SB08`
- **Next subbundle:** SB09 unlocked
