# SB02 — Module-owned source authority registry

        **Depends on:** SB01  
        **Required before merge:** Yes

        ## Goal

        Turn the source authority SPI into a real DI registry owned by source-publishing modules.

        ## Required work

        1. Change the resolver dependency to IEnumerable<IAgentExecutionSourceAuthorityProvider> or an equivalent DI-friendly collection.
2. Remove CreateDefaultProviders and all hard-coded construction from CanonicalAgentExecutionAuthorityResolver.
3. Move Project Structure authority to the module that publishes project-structure context.
4. Move projects portfolio authority to the owning Projects/Workbench integration identified by CodeAnalysis.
5. Move processes/processes-live authority to Modules.Processes.
6. Register providers with TryAddEnumerable and retain duplicate source-key fail-fast validation.
7. Keep unknown source behavior fail-closed and behaviorally unchanged.
8. Add dependency guards so Modules.AgentFramework cannot regain source-kind-specific product/process implementations.

        ## Primary files

        - `src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentExecutionSourceAuthority.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentExecutionAuthorityComposition.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentExecutionSourceAuthorityProviders.cs`
- `src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs`
- `src/Modules/CanDoItAll.Modules.Projects/`

        ## Acceptance

        - [ ] Each source authority implementation lives with its owning module.
- [ ] Resolver consumes DI-provided providers and constructs none.
- [ ] Duplicate keys fail deterministically.
- [ ] Missing provider plus workspace claim fails closed.
- [ ] No project/process behavior regression.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.

## Execution contract

- **Owned finding:** MRG-002.
- **Proof tier:** Behavioral.
- **Progression gate:** SB03 unlocks only after implementations and registrations are module-owned, resolver construction is provider-agnostic, and dependency guards pass.
- **Reopen trigger:** Any source-kind implementation remains in or returns to Modules.AgentFramework, or a new reference/cycle is needed.

## C# Architecture Impact

Convert a hard-coded integration-module catalog into a real module-owned provider registry.

## Boundary Ownership

Core owns the SPI; Workbench/Projects/Processes own their source semantics; Modules.AgentFramework owns only canonical resolution and composition.

## Dependency Direction

Publishing modules depend on Core contracts; Core and MAF never depend on publishing modules. Stop on a project cycle.

## Pattern Decision

Use an injected provider strategy collection with duplicate-key validation; reject a hard-coded factory or service locator.

## Testability Contract

Instantiate the resolver with explicit providers, test registrations in owning modules, and prove unknown/duplicate behavior independently.

## Partial Class Policy

Use top-level providers in owning modules; no nested or partial provider containers.

## Architecture Proof Required

Pre/post CodeAnalytics inventory and dependency/cycle proof, direct provider tests, composition smoke, and forbidden-ownership source guard.
