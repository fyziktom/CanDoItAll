# SB07 Proof Manifest

## Status

Completed.

## Semantic invariant

See `proof/SB07/semantic-invariants.md`.

## Failing-first or adversarial proof

`proof/SB07/transcripts/failing-first.txt`

## Passing proof

`proof/SB07/transcripts/passing.txt`

## Production-path coverage

- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` registers project-structure read and mutation tool names as first-class policy metadata.
- Project-structure mutation tools now require `ExecuteExternalAction`; project-structure read tools remain read-only and do not require external-action authorization.
- Unregistered `project_structure_*` tools classify as `Unknown` instead of inheriting generic read-tool behavior.
- `repo://Templates/Processes/processes/app-page-screenshot/definition.json`, `repo://Templates/Processes/processes/app-pages-screenshot-set/definition.json`, and `repo://Templates/Processes/processes/app-layout-image-generation/definition.json` declare typed operation contracts for project-structure read, runtime proof, and writeback steps.
- Focused unit and integration tests exercise policy denial/allowance, tool inventory classification, and projected template contracts.

## Source assertions

`proof/SB07/transcripts/source-assertions.txt`

## Anti-stub audit

`proof/SB07/transcripts/anti-stub-audit.txt`

## Changed-file hashes

`proof/SB07/transcripts/changed-file-hashes.txt`

- `F4062287850377984FE36215A9841563EFA0786D75DA0F07CEA58E78030ED2F8` `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
