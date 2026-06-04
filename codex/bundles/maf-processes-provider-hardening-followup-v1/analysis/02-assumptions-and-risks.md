# Assumptions And Risks

## Critical Path Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Starting process-core extraction too early | Huge diff, broken dispatcher/runtime, unclear ownership | This bundle explicitly forbids process-core extraction. |
| Provider seam remains raw `AITool` only | Future driver packs cannot be safely governed or traced | Add provider/tool metadata before more migrations. |
| Product-tool migration silently changes tool names | Existing agents/processes lose capabilities | Exact pre/post tool inventory and parity tests per provider. |
| MAF product references are removed too aggressively | Build/runtime breaks because some references are still legitimate | Use allowed-list and remove references only after source scan proves they are unused. |
| Process provider becomes a new monolith | Decoupling helps MAF but shifts complexity into Processes | Forced refactor SB07 before purpose hardening. |
| Branch bundle churn pollutes merge | Historical evidence/proof removed accidentally | SB01 branch hygiene gate before runtime work. |

## Validation Risks

- Build-only proof is insufficient. Require targeted provider tests, policy tests, hidden dependency scans, and process evidence smoke.
- Count-only tool parity is insufficient. Require exact names, signatures where feasible, approval classification, access behavior, and provider ownership.
- Documentation-only scans can miss source coupling. Pair docs scans with source `rg` and architecture tests.

## Reopen Triggers

Reopen earlier subbundles when:

- A runtime provider returns duplicate names or unnamed tools.
- Any process/project/image tool is lost, renamed, or loses approval classification unexpectedly.
- MAF regains a direct `CanDoItAll.Modules.Processes` source or project dependency.
- Providerizing project/image tools leaves stale attach methods in `MafAgentRuntime.Capabilities.cs`.
- Process provider purpose-aware hardening blocks existing governed process automation that previously had explicit write access.
- Full solution build or process evidence smoke fails after a refactor checkpoint.
