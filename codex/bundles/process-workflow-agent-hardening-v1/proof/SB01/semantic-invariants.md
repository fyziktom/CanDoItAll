# SB01 Semantic Invariants

1. Runtime process operation names must remain identical to `Enum.GetNames<ProcessStepOperation>()`.
2. Runtime process target scope names must remain identical to `Enum.GetNames<ProcessStepTargetScope>()`.
3. Production source paths may not introduce `workspace_*`, `browser_*`, `project_structure_*`, `image_generation_*`, or `processes_*` ids unless the id is in a canonical catalog or explicitly classified as an owned prefix.
4. Internal workflow JSON selectors in canonical source paths must be listed in `WorkflowJsonPathContractNames`.
5. Template operations must parse structurally from template JSON and resolve through `ProcessContractCatalog`.
6. External executor ids and test fixtures may be present only in their classified boundary; they cannot be used to hide production runtime ids.
7. SB01 must not introduce Tetris-specific production logic, stubs, or broad dispatch/UI rewrites.

## Shallow-Pass Trap

A test that only checks that catalog classes exist is insufficient. SB01 requires both:

- Adversarial negative proof: an unknown production-style internal id is rejected.
- Semantic positive proof: real scoped repository files and process templates pass after their internal ids are either cataloged or classified.

## Dependency Smoke

SB02-SB06 can import the new descriptors without guessing owner boundaries:

- Process and workflow model contracts live in `CanDoItAll.AgentFramework.Models`.
- Tool ids live in `CanDoItAll.AgentFramework.Core`.
- Process runtime descriptors live in `CanDoItAll.Modules.Processes`.
