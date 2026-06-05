# Assumptions And Risks

## Critical Path Risks

1. **Candidate behavior drift**
   Candidate construction has many subtle defaults: `TechnicalAgentId`, recovery ids, reusable chat session id, branch outcome flags, cooperation metadata, expected artifacts and artifact input lists. A shallow extraction can compile but change runtime semantics.

2. **Side-effect hiding**
   `ProcessDispatchTechnicalAgentBindingCoordinator` legitimately owns `SaveAgentAsync`. The new candidate factory must not hide additional side effects inside something that looks pure.

3. **Driver API premature birth**
   Cooperation metadata and workspace tool profile classification are close to future helper-driver selection. It is tempting to add `IProcessDriverPack` now, but production driver API should remain out of scope.

4. **AgentFramework coupling broadening**
   Current module-local helpers can reference process module types and existing AgentFramework models if already necessary. Do not reintroduce MAF/product coupling or push driver-facing abstractions into MAF/Tooling.

5. **Validation gaps**
   Existing tests can pass if they only cover happy-path candidate creation. Required proof must cover subprocess, workflow, direct-agent, missing binding, read-access grant/no-op, recovery id reuse, artifact input shaping and branch outcome semantics.

## Validation Risks

- Header selection passing does not prove candidate construction parity.
- Build passing does not prove `DispatchCandidate` field parity.
- Missing-binding and project-structure read-access cases need focused tests.
- Cooperation metadata profile selection must preserve software-development / quality-validation / business-analysis behavior.

## Reopen Triggers

Reopen earlier subbundles if:
- Any candidate route drops or renames a field in `DispatchCandidate`.
- `ProjectStructureAccessGrantedAndSaved` is no longer observable.
- `Workflow` or `Subprocess` candidates receive technical agent ids.
- `Process Core`, driver-pack, or production driver registry files appear.
- Any UI/prohibited viewport proof artifacts appear.
