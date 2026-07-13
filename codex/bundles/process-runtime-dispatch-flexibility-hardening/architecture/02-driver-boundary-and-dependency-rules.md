# Driver Boundary And Dependency Rules

## Dependency Direction

Allowed direction:

```text
Composition/App/Modules
    -> Processes application/runtime/contracts

Composition/App/Modules
    -> MAF-owned AgentFramework process driver implementation

MAF-owned AgentFramework process driver implementation
    -> CanDoItAll.Processes.Drivers.Abstractions

MAF-owned AgentFramework process driver implementation
    -> CanDoItAll.Processes.Application contracts or ports when SB01 approves that contract dependency

MAF-owned AgentFramework process driver implementation
    -> AgentFramework/MAF services

CanDoItAll.Processes.*
    -> CanDoItAll.Processes.Drivers.Abstractions
    -> other Processes/Foundation dependencies only
```

Composition is the join point that may know both generic Processes and the concrete MAF/AgentFramework driver. It must not be implemented as `CanDoItAll.Processes.* -> MAF-owned AgentFramework process driver implementation`.

Forbidden direction:

```text
CanDoItAll.Processes.* -> CanDoItAll.AgentFramework.*
CanDoItAll.Processes.* -> CanDoItAll.Modules.AgentFramework
CanDoItAll.Processes.* -> MAF-owned driver implementation
CanDoItAll.Processes.Drivers.Standard -> MAF/AgentFramework implementation
```

## Generic Runtime Ownership

Processes owns:

- immutable process plans and strategy bindings
- runtime state machine and event rules
- scheduling and claim lifecycle
- queue request contracts
- branch signal application as process state logic
- driver catalog and selected-driver invocation through abstractions
- generic process brief contract and domain-neutral fallback brief behavior

Processes does not own:

- AgentFramework/MAF agent invocation
- provider/model prompt fragments
- tool receipt interpretation beyond typed driver results
- product mutation completion policy
- managed artifact body sanitization policy
- MAF execution recovery heuristics
- project-structure/.NET/software-delivery launch enrichment

## Driver-Owned Ports To Introduce Or Confirm

SB01 must decide exact names and placement, but the implementation must provide typed equivalents for these ports:

- `IProcessStepDispatchDriver`: selected by immutable strategy binding; executes a ready step and returns a `StrategyResultEnvelope` or typed driver result.
- `IProcessPromptCompositionDriver`: builds driver-specific step prompts/fragments from a typed process brief request.
- `IProcessCompletionEvidenceDriver`: validates and materializes driver-specific completion evidence, receipts, product paths, artifact bodies, and grounded references.
- `IProcessDriverRecoveryPolicy`: classifies driver-specific transient failures, output-contract failures, and recoverable evidence states.
- `IProcessDriverTelemetryReader`: maps driver runtime observations to process projection observations without leaking MAF types into Processes.

Existing `IProcessStrategyFactory`, `IProcessStrategy`, `IProcessExecutionAdapter`, and `ProcessDriverPackage` may be extended or wrapped instead of introducing all names literally, but the shipped architecture must preserve these responsibilities.

## AgentFramework/MAF Driver Placement Rule

Preferred placement:

- An MAF-owned project such as `CanDoItAll.AgentFramework.Processes.Driver` or equivalent, outside `src/Processes`, references `CanDoItAll.Processes.Drivers.Abstractions` and AgentFramework/MAF services.

Rejected placement:

- A new `src/Processes/Drivers/*AgentFramework*` project that references MAF assemblies.
- Moving MAF/AgentFramework prompt/evidence/dispatch policy into `CanDoItAll.Processes.Application` or `CanDoItAll.Processes.Runtime`.

Fallback:

- If app composition cannot wire an MAF-owned driver project yet, implementation must stop and record the blocker. A temporary module-owned adapter is allowed only as an explicit transitional composition shim that implements process driver abstractions and is isolated from generic process projects.

## Dispatch Boundary

Generic dispatcher may:

- load state and plan
- expire, create, mark, defer, and release claims
- enforce retry budgets
- schedule ready work
- invoke a selected driver by immutable binding
- submit a `StrategyResultEnvelope`
- apply branch signals and projection catchup

Driver must own:

- actual step execution dispatch
- prompt composition and provider/model-specific prompt fragments
- completion evidence policy
- tool receipt and managed artifact interpretation
- subprocess adapter policy and parent outcome synthesis
- driver-specific retry/recovery classification

This split keeps runtime liveness generic while making execution behavior swappable.

## Proof Gate

SB01 and SB07 must capture source/project-reference scans proving:

- no `src/Processes/*` project references MAF or AgentFramework projects;
- no Processes source file imports `CanDoItAll.AgentFramework.*` except allowed projection display text if explicitly retained and justified;
- any AgentFramework/MAF process driver implementation references Processes abstractions from below;
- composition wiring is the only layer that knows both the generic process runtime and the concrete MAF driver.
