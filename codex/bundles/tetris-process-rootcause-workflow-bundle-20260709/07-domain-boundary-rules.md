# Domain boundary rules

## Generic process runtime may contain

- Process ids, step ids, branch outcome keys as data.
- Generic branch signal application.
- Generic completion issue codes.
- Generic receipt matching.
- Generic file/readback/content-check abstractions.
- Generic retry/manager policies.
- Generic diagnostics and trace records.

## Generic process runtime must not contain

- .NET tool names.
- Blazor scaffold words.
- Tetris/game words.
- Software-delivery step keys hardcoded in logic.
- Branch keys such as `quality-accepted` hardcoded in generic logic.

## .NET/software-delivery layer may contain

- `workspace_dotnet_restore`, `workspace_dotnet_build`, `workspace_dotnet_test`, `workspace_dotnet_run`, `workspace_dotnet_stop`.
- Blazor scaffold checks.
- Software-delivery template names and branches.
- DotNet project/solution target rules.
- Project-structure-driven .NET acceptance criteria.

## Current violations to fix

| Current location | Violation | Target |
|---|---|---|
| `ProcessStepRecoveryInstructionBuilder` | hardcoded QA step keys and .NET tool names | Move to provider/template metadata. |
| `definition.json CapabilityScope.RequiredReceipts` | browser acceptance receipts as unconditional process capability receipts | Move to branch-aware product completion rules or add branch applicability. |
| `ProcessLaunchApplicationService.FormatProductCompletionRequiredStringList` | assumes only strings | Structured generic parser. |
| `ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts` | assumes string array | Structured generic parser. |

## Acceptable compromise during migration

Codex may keep temporary compatibility shims, but every temporary generic-domain bridge must have:

- TODO with target provider name,
- unit test proving no new domain terms are added,
- removal issue/sub-bundle.

Do not add a quick `if stepKey == "qa-validation"` fix in the adapter. That would solve this incident while making the runtime less generic.
