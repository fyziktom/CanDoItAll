# Generic Process Core Boundary

The process runtime must avoid embedding software-specific assumptions in core decisions.

## Allowed In Core

- operation classes
- target scopes
- artifact expectation modes
- branch/disposition policies
- retry/no-progress policies
- provenance and lineage
- storage-backed validation
- linter issue ids
- generic path safety

## Belongs In Skills/Templates/Process Definitions

- .NET scaffolding rules
- Blazor render mode advice
- JavaScript package launch rules
- browser MCP quirks
- PowerShell helper-script recipes
- software QA test conventions
- business plan section templates
- legal approval language
- manufacturing SOP evidence rules

## Implementation Rule

Core code can use software examples in tests, but production decisions should be based on generic operation/target/artifact/disposition contracts.
