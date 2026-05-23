# Requirement Traceability

## Requirement Matrix

| Requirement | Inputs | Analysis | Architecture | Subbundle | Planned proof |
| --- | --- | --- | --- | --- | --- |
| `R001` | `inputs/00-original-request.md#raw-note-ids`, `inputs/01-source-artifacts.md#development-db` | `analysis/01-current-state.md#gaps` | `architecture/01-target-solution.md#proposed-shape` | `subbundles/01-browser-evidence-contract-and-storage` | Integration test plus process artifact record query |
| `R002` | `inputs/01-source-artifacts.md#workspace-evidence` | `analysis/01-current-state.md#failure-chain` | `architecture/01-target-solution.md#proposed-shape` | `subbundles/01-browser-evidence-contract-and-storage` | Provider-native MCP projection test with empty chat history |
| `R003` | `inputs/00-original-request.md#raw-note-ids` | `analysis/02-assumptions-and-risks.md#reopen-triggers` | `architecture/01-target-solution.md#production-behavior-artifact-matrix` | `subbundles/02-generic-runtime-proof-gates` | Failing-first process transition test and conformance observation assertion |
| `R004` | `inputs/01-source-artifacts.md#workspace-evidence` | `analysis/02-assumptions-and-risks.md#validation-risks` | `architecture/01-target-solution.md#production-behavior-artifact-matrix` | `subbundles/02-generic-runtime-proof-gates` | Console phase unit tests and integration proof |
| `R005` | `inputs/00-original-request.md#raw-note-ids` | `analysis/01-current-state.md#behavioral-findings` | `architecture/01-target-solution.md#boundaries` | `subbundles/02-generic-runtime-proof-gates` | Interactive proof tests and browser analytics review |
| `R006` | `inputs/00-original-request.md#raw-note-ids` | `analysis/02-assumptions-and-risks.md#risks` | `architecture/01-target-solution.md#boundaries` | `subbundles/03-process-definition-agent-instruction-contracts` | Source assertions and anti-hardcoding audit |
| `R007` | `inputs/01-source-artifacts.md#development-db` | `analysis/01-current-state.md#failure-chain` | `architecture/01-target-solution.md#proposed-shape` | `subbundles/03-process-definition-agent-instruction-contracts` | Process definition/seed tests |
| `R008` | `inputs/00-original-request.md#raw-note-ids` | `analysis/01-current-state.md#repository-findings` | `architecture/01-target-solution.md#boundaries` | `subbundles/03-process-definition-agent-instruction-contracts` | Prompt tests and instruction diff review |
| `R009` | `inputs/01-source-artifacts.md#development-db` | `analysis/01-current-state.md#failure-chain` | `architecture/01-target-solution.md#validation-strategy` | `subbundles/04-regression-and-demo-readiness-proof` | Regression fixture matching run `4f218d64-...` |
| `R010` | `inputs/00-original-request.md#raw-note-ids` | `analysis/02-assumptions-and-risks.md#critical-path-risks` | `architecture/01-target-solution.md#validation-strategy` | `subbundles/04-regression-and-demo-readiness-proof` | Clean development DB live run |
| `R011` | `inputs/00-original-request.md#raw-note-ids` | `analysis/02-assumptions-and-risks.md#validation-risks` | `architecture/01-target-solution.md#validation-strategy` | `subbundles/04-regression-and-demo-readiness-proof` | Execution report browser analytics |
| `R012` | `inputs/01-source-artifacts.md#source-references` | `analysis/01-current-state.md#gaps` | `architecture/01-target-solution.md#production-behavior-artifact-matrix` | `subbundles/01-browser-evidence-contract-and-storage`, `subbundles/02-generic-runtime-proof-gates` | Log/conformance source assertions |

## Raw Note Closure Plan

| Raw note | Literal scope preserved | Owning subbundle | Planned proof | Exception status |
| --- | --- | --- | --- | --- |
| `N001` | "final app was not properly tested" | `SB02`, `SB04` | Runtime proof gates plus clean-DB browser validation | No exception |
| `N002` | "there are not screenshots evidences" | `SB01`, `SB03`, `SB04` | Process artifact records include screenshot files | No exception |
| `N003` | "items in tetris are not comming ... not visible" | `SB02`, `SB03` | Generic representative interaction proof from project structure hints | No Tetris hardcoding in process core |
| `N004` | "js trouble in console output" | `SB02`, `SB04` | Console log capture and active/post-stop classification | No exception |
| `N005` | "this should not happen when I run complicated process" | `SB02`, `SB04` | Release-readiness cannot pass original failure fixture | No exception |
| `N006` | "processes core still must remain generic" | `SB03` | Anti-hardcoding audit | No exception |
| `N007` | "detail should be in project strucure info ... skills ... process steps definitions" | `SB03` | Prompt/definition/project-structure guidance tests | No exception |
