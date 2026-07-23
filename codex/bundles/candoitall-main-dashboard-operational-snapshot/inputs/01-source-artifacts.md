# Source Artifacts And Operator Refinements

## Input Artifacts

| ID | Artifact | Role |
| --- | --- | --- |
| `N000` | `bundle://inputs/00-original-request.md` | Literal preparation and product request captured 2026-07-22. |
| `O001` | Architecture refinement below | Supersedes only the original workflow implementation suggestion. |
| `O002` | Performance refinement below | Supersedes loading the full Agent Overview for two totals. |
| `E001` | Direct repository inspection recorded in `bundle://analysis/01-current-state.md` | Current-state and source ownership evidence. |
| `E002` | CanDoItAll Components MCP catalog/recommendation evidence recorded in `bundle://inventories/01-scope-inventory.md` | Shared component selection evidence. |

## O001 — Workflow Activity Refinement

```text
Architecture refinement from data/performance inventory: use a dedicated typed bounded workflow dashboard activity query service/store method (active execution states Running/WaitingForInput, otherwise latest five) rather than extending the aggregate WorkflowOverview snapshot; this avoids full aggregate group-by work on each dashboard refresh. Update bundle architecture/requirements accordingly. Recent projects stays a dedicated bounded Projects query. Process stays dedicated lightweight dashboard query over projection snapshots.

Architecture gate correction: process projection snapshots are not the canonical source for active-run selection because the newest-500 projection window can omit an older active run. Select active-or-recent IDs from canonical runtime state, then load projection data for only those rows as optional display enrichment and surface projection lag. Do not run or trust one bounded catch-up batch as proof of full freshness.
```

Decision: authoritative for implementation. The raw original wording remains unchanged, but R003 and the architecture records require the dedicated path.

## O002 — Agent Usage Refinement

```text
Second performance refinement: add a narrow typed IAgentUsageTotalsQueryService/implementation over the existing profile-scoped ISandboxWorkspaceStore.LoadUsageProjectionAsync, returning TotalTokens/KnownCostUsd/UpdatedAt (same source as Agents overview), rather than loading/mapping the full AgentOverview. Register through existing AgentFramework composition paths. Dashboard loader must not inject the file store directly. Update bundle docs.
```

Decision: authoritative for implementation. R005 owns this boundary and prevents a full Agent Overview load.

## Evidence Limit

CodeAnalytics MCP was unavailable during preparation. No snapshot ID, automated findings set, or automated dependency/cycle report exists. This is a declared evidence gap, not a claim that the graph is clean; direct `.csproj`, DI, contract, store, page, and test inspection is recorded instead.
