# MAF 1.13 Update Evidence

## Scope

Conservative Microsoft Agent Framework package update from the existing 1.8-era references to the 1.13 line.

## Baseline

- Branch: `memory-providers`.
- Baseline restore: passed before package edits.
- Baseline Release build: passed before package edits.
- Pre-existing validation noise: `Microsoft.OpenApi` 2.0.0 NU1903 warning.
- CodeAnalytics baseline snapshot: `snap-20260708002602-f2b77ff7`.

## Package Decisions

| Project | Package | Before | After | Reason |
| --- | --- | --- | --- | --- |
| `CanDoItAll.AgentFramework.Maf` | `Microsoft.Agents.AI` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `CanDoItAll.AgentFramework.Maf` | `Microsoft.Agents.AI.OpenAI` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `CanDoItAll.AgentFramework.Maf` | `Microsoft.Agents.AI.Workflows` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `CanDoItAll.AgentFramework.Maf` | `Microsoft.Agents.AI.A2A` | `1.8.0-preview.260528.1` | `1.13.0-preview.260703.1` | Current NuGet CLI preview. |
| `CanDoItAll.AgentFramework.Maf` | `Microsoft.Agents.AI.Mem0` | `1.0.0-preview.251028.1` | `1.0.0-preview.251028.1` | Current NuGet CLI still reports latest as not found. |
| `CanDoItAll.AgentFramework.Workflows.MafAdapter` | `Microsoft.Agents.AI` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `CanDoItAll.AgentFramework.Workflows.MafAdapter` | `Microsoft.Agents.AI.Workflows` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `CanDoItAll.AgentFramework.Workflows.MafAdapter` | `Microsoft.Extensions.AI.Abstractions` | `10.5.1` | `10.6.0` | Restore-proven MAF floor. |
| `CanDoItAll.AgentFramework.Workflows.MafAdapter` | `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.7` | `10.0.9` | Restore-proven MAF floor. |
| `CanDoItAll.AgentFramework.Hosting` | `Microsoft.Agents.AI.Hosting.A2A` | `1.8.0-preview.260528.1` | `1.13.0-preview.260703.1` | Current NuGet CLI preview. |
| `CanDoItAll.AgentFramework.Hosting` | `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.7` | `10.0.9` | Restore-proven Hosting A2A floor. |
| `CanDoItAll.AgentFramework.Tooling` | `Microsoft.Extensions.AI.Abstractions` | `10.5.1` | `10.5.1` | No restore floor required in this project. |

## Validation

- `SB01` baseline restore/build passed before package edits.
- `SB02` restore passed after package update and dependency-floor correction.
- `SB03` failing-first build isolated the package-induced removal of `AgentSkillsProviderBuilder.UseScriptApproval`.
- `SB03` Release build passed after switching to MAF 1.13 `AgentSkillsProviderOptions`.
- `SB03` focused unit proof passed: 35 MAF composition tests and 330 MAF/process regression tests.
- `SB04` architecture drift gate passed with CodeAnalytics snapshot `snap-20260708010020-ca7eff1f`.
- Focused unit validation passed after readiness/provider fixes: `161/161`.
- Focused integration validation passed after readiness/provider fixes: `58/58`.
- Final Release build passed after readiness/provider fixes with known `Microsoft.OpenApi` NU1903 warning only.
- Live 5032 validation passed: rebuilt the app through dotnetwatch, opened `QuotationPDFs Tests`, deleted the stale workbook, used the project-structure floating chat with `Financial Strategist`, read the quotation PDF asset, generated a new XLSX workbook, attached it as a project-structure File asset, and inspected the workbook contents from disk.
- Final verifier attempt passed restore, Release build, and the prepared focused-unit filter (`321/321`). Its broad integration filter stalled in local vstest infrastructure; this is documented in the bundle closure notes and does not replace the completed targeted integration proof (`58/58`).
