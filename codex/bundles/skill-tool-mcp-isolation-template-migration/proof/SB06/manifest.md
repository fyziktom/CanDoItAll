# SB06 Proof Manifest

## Scope

- Implemented `Templates/Capabilities` with separate capability descriptor files for skills, tools, MCPs, other capability kinds, policies, and schema guidance.
- Replaced `SandboxWorkspaceSeedBuilder` hardcoded capability catalog creation with `CapabilityTemplatePackLoader` plus `CapabilityTemplateSeedMaterializer`.
- Added structured template diagnostics for duplicate capability keys, missing template files, raw secret fields, invalid MCP allowed tools, unknown access-policy capability selectors, and broken agent skill assignments.
- Added typed compatibility compilation from process `AllowedOperations` to capability access policy operation-classification selectors.

## Production Files

- `Templates/Capabilities/manifest.json`
- `Templates/Capabilities/skills.json`
- `Templates/Capabilities/tools.json`
- `Templates/Capabilities/mcps.json`
- `Templates/Capabilities/other.json`
- `Templates/Capabilities/policies/capability-access-policy.json`
- `Templates/Capabilities/schemas/capability-template.schema.json`
- `src/CanDoItAll.AgentFramework.Persistence/Seeds/CapabilityTemplatePackLoader.cs`
- `src/CanDoItAll.AgentFramework.Persistence/Seeds/CapabilityTemplateSeedMaterializer.cs`
- `src/CanDoItAll.AgentFramework.Persistence/Seeds/CapabilityTemplateSeedPolicyValidator.cs`
- `src/CanDoItAll.AgentFramework.Persistence/Seeds/CapabilityTemplateSeedAssignmentValidator.cs`
- `src/CanDoItAll.AgentFramework.Persistence/Seeds/ProcessAllowedOperationsCapabilityPolicyCompiler.cs`
- `src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`
- `src/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj`
- `src/CanDoItAll.AgentFramework.Persistence/Properties/InternalsVisibleTo.cs`

## Test Files

- `tests/CanDoItAll.Tests.Unit/CapabilityTemplateSeedMaterializationTests.cs`
- `tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`

## Proof Transcripts

- `failing-first-capability-template-seed-tests.txt`
- `passing-capability-template-seed-tests.txt`
- `regression-capability-foundation-contracts.txt`
- `dotnet-build-solution.txt`
- `source-assertions.txt`
- `anti-stub-audit.txt`
- `static-performance-scan.txt`
- `file-size-scan.txt`
- `bundle-validator.txt`
- `changed-file-hashes.txt`

## Validation Result

- Focused SB06 tests passed: 7 tests.
- SB01-SB05 capability foundation regression suite passed: 54 tests.
- `dotnet build CanDoItAll.slnx --no-restore` passed with 0 warnings and 0 errors.
- Bundle validator passed for prepared stage.
- No browser validation required for SB06; future UI checks remain large-screen-only for SB10/SB11.
