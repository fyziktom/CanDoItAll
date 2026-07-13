# C# Boundary Map

## Project Ownership

| Project or area | Owns | Must not own |
|---|---|---|
| `CanDoItAll.Processes.Core` | Value objects, identifiers, artifact definitions, requirement modes, generic process vocabulary. | Runtime state machines, persistence, host integration, AgentFramework, UI, software-development policy. |
| `CanDoItAll.Processes.Builder` | Template and plan compilation into generic process plans. | Runtime mutation, manager decisions, adapter-specific evidence. |
| `CanDoItAll.Processes.Runtime` | Runtime state transitions, scheduling eligibility, artifact lineage ledger, finalization gate, recovery route classification, handoff state. | Application orchestration, persistence transactions, Module/AgentFramework integration, domain-specific driver policy. |
| `CanDoItAll.Processes.Application` | Launch/dispatch/manager orchestration, persistence unit-of-work coordination, projection service boundaries. | Generic taxonomy rules that should be unit-tested in Runtime, AgentFramework-specific materialization, software-delivery policy. |
| `CanDoItAll.Processes.Persistence` | Storage mappings for runtime state, lineage, finalization receipts, handoff state, and projections. | Runtime decision logic. |
| `CanDoItAll.Processes.Drivers.Abstractions` | Driver contracts for execution, evidence policy, bounded context packaging, and domain-specific finalization advice. | Concrete AgentFramework or project-structure code. |
| `CanDoItAll.Processes.Drivers.Standard` | Generic driver implementations and reusable standard policies. | Software-development-specific policy unless explicitly generic. |
| `CanDoItAll.Modules.Processes` | Host, UI, AgentFramework, managed artifact, tool receipt, and app-service integration. | Generic runtime rules that should live in Runtime/Core/Driver abstractions. |

## Boundary Rules

- Runtime may depend on Core, Driver Abstractions only when the abstraction is generic, and internal runtime helpers.
- Runtime must not depend on Application, Persistence, Projections, Templates, Modules, AgentFramework, MAF, Blazor, or project-structure services.
- Application may compose Runtime, Builder, Persistence, Projections, Templates, and Driver abstractions.
- Module integration may depend outward to Application and driver implementations, but generic logic extracted from Module must land in generic packages only if it contains no domain-specific concepts.
- Driver-specific policy must be injected through explicit contracts; no runtime type checks against concrete driver names.

## Boundary Acceptance Rules

- Every new service has an owner project and a reason it belongs there.
- Every new contract is either generic runtime vocabulary or driver-specific policy. Mixed contracts are rejected.
- Tests for Runtime contracts do not instantiate Module integration or AgentFramework types.
- Tests for Module adapter behavior do not require direct mutation of runtime internals unless validating integration wiring.
