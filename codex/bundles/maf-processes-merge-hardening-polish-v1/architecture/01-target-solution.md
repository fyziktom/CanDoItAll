# Target Solution

## Desired end state before merge

The branch should merge as a clean preparation/refactor branch:

- MAF remains independent of Processes.
- Process Core remains a deterministic model/rules package.
- The Processes module may still own the current dispatcher runtime, but the generic dispatcher should not directly contain software-delivery/.NET/Blazor/JavaScript proof heuristics after this polishing pass.
- Domain drivers remain verification-only and read-only.
- The verification gateway remains explicit and typed.
- Temporary Codex work-package artifacts are absent from tracked repo content.
- Tests use semantic names that describe product behavior, not subbundle execution history.

## Boundary model

```text
CanDoItAll.AgentFramework.Maf
  -> AgentFramework.* / Tools / Security / Workspace
  -X-> CanDoItAll.Modules.Processes

CanDoItAll.Processes.Core
  -> CanDoItAll.Processes.Contracts
  -X-> Modules / Drivers / Infrastructure / AgentFramework / EF / UI / Plugins

CanDoItAll.Processes.Drivers.*
  -> Drivers.Abstractions / Core or Contracts only where needed
  -X-> Modules.Processes / Infrastructure / AgentFramework / EF / UI / Plugins / workspace mutation / external calls

CanDoItAll.Processes.Drivers.VerificationGateway
  -> explicit verifier instances and typed methods
  -X-> dynamic dispatch / registry / selector / DI / discovery / manager / scheduler / workflow hooks

CanDoItAll.Modules.Processes
  -> dispatcher runtime + adapters + Process Core + Process Drivers
  -> may map persisted process state into driver request DTOs
  -X-> permanent bundle/subbundle terminology in runtime/test APIs
```

## Software-delivery proof ownership

Preferred merge-safe target:

- Introduce `CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence` as a verification-only domain package, or an equivalent clearly named domain driver if Codex finds an existing better package to extend.
- Move pure software-delivery proof policy there:
  - implementation contract snapshot and stack detection,
  - runnable application signal detection,
  - .NET/Blazor/JavaScript/TypeScript stack detection,
  - concrete product path rules,
  - implementation receipt timeline rules,
  - runnable .NET host evidence rules,
  - carried implementation proof rules if they are currently tied to software-delivery product mutation semantics.
- Keep the Processes module responsible only for adapting `DispatchCandidate`, `ProcessAutomationExecutionRunDetail`, tool receipts, artifacts, and work briefs into domain-driver request DTOs.
- Do not make the driver read files, call Git, call shells, query storage, query the DB, mutate process state, or create artifacts.

Fallback if a new project is too risky before merge:

- Create a clearly named internal domain adapter seam under `CanDoItAll.Modules.Processes`, e.g. `Automation/Dispatch/Domain/SoftwareDelivery/*`, and move all stack/product-proof heuristics behind that seam.
- Add TODO/roadmap notes and tests that identify this seam as the only allowed pre-merge exception.
- This fallback must still remove stack-specific strings from generic dispatcher partials and provide a tracked post-merge task to lift the seam into a driver package.

## Non-goals

- No dispatcher-runtime isolation beyond the small seams required for domain proof ownership.
- No runtime driver host.
- No driver registry.
- No process execution mutation in drivers.
- No UI redesign.
- No live-process redesign.
