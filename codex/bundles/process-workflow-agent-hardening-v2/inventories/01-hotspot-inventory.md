# Hotspot Inventory

## P0 Runtime/Policy Hotspots

| Surface | Current concern | Owning follow-up |
| --- | --- | --- |
| `ProcessToolOperationAuthorizer` | Missing allowed operations returns no denial. | SB01 |
| `ProcessStepOperationContractState` | Missing operations + missing target scope can normalize without issue. | SB01 |
| `ToolContractCatalog` + `AgentToolInvocationPolicyMetadata` | Known tool catalog and registered metadata can drift. | SB02 |
| `AgentToolInvocationPolicy` | Mixed responsibilities, default-read fallback, path/script/browser/process checks in one policy. | SB02, SB06 |
| `MafAgentRuntime` | Usage observation creation does not normalize detailed raw usage; finalizer responses still set aggregate tokens zero. | SB03 |
| `ProviderPricingModels` | Cost model has input/cached/output only; reasoning/total stored in summary but not priced separately. | SB03 |
| `ProcessRunAutomationDispatchService.Costing` | Good ledger-first direction, but depends on complete observation normalization. | SB03 |
| `ProcessRunAutomationDispatchService.GovernedRules` | Text/regex-heavy required-tool/browser/artifact heuristics. | SB06 |
| `run_sb08_multidomain_e2e.ps1` | Manual transitions, harness-generated apps, no provider execution runs. | SB04, SB05 |

## Template/Skill Hotspots

| Surface | Concern | Owning follow-up |
| --- | --- | --- |
| `Templates/Processes/processes/software-delivery/definition.json` | Must declare strict operation contracts for all automation-relevant steps. | SB01, SB07 |
| `Templates/Agents/.../instructions.md` | Agents must know operation contracts and proof obligations. | SB07 |
| `codex/skills/candoitall-api-*` | Skills must align with stricter proof and tool registry. | SB07 |
| `codex/skills/bundles/*` | Completed-stage validation must reject proof-path bypasses. | SB05, SB07 |

## UI Hotspots

| Surface | Concern | Owning follow-up |
| --- | --- | --- |
| `/processes/live` | Must show blocked contract state and unknown usage clearly. | SB08 |
| Process run detail | Must show agent execution runs, tool receipts, usage observations, and proof validity status. | SB08 |
| Workflow editor/executor status | Must show side-effect descriptor and idempotency state. | SB08 |
